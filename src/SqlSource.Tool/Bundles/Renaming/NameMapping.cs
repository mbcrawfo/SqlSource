using System;
using System.Collections.Generic;
using System.Text;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The mapping of one renaming: one spelling has one replacement throughout, whatever class it is found in, and its
/// class is that of the first place it is found.
/// </summary>
/// <remarks>
/// A replacement is the class's prefix and the HMAC-SHA256 of the class word and the folded spelling, keyed by the
/// run's key and cut to eight lower-case hex digits; a replacement that is already given lengthens by two digits
/// until it is free.  A spelling that is kept maps to the spelling first kept, wins over a later <see cref="Name" />
/// and carries no class; one that already has a replacement keeps it.  Only a spelling kept from a row of the
/// engine's is kept for the token check, which trusts no other.  The renaming starts at eight digits; a test gives a
/// shorter <c>firstLength</c> to force a collision.
/// </remarks>
internal sealed class NameMapping(RenamingKey key, Func<string, bool, string> fold, int firstLength)
{
    private const int FirstLength = 8;
    private const string HexDigits = "0123456789abcdef";

    private readonly Dictionary<string, MappedName> _byKey = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, string> _kept = [with(StringComparer.Ordinal)];
    private readonly HashSet<string> _keptFromEngineRows = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, MappedName> _literals = [with(StringComparer.Ordinal)];
    private readonly HashSet<string> _replacements = [with(StringComparer.Ordinal)];
    private readonly List<MappedName> _names = [];

    /// <summary>A mapping whose replacements start at eight hex digits.</summary>
    public NameMapping(RenamingKey key, Func<string, bool, string> fold)
        : this(key, fold, FirstLength) { }

    /// <summary>The names made, in the order they were made; a kept name is left out.</summary>
    public IReadOnlyList<MappedName> Names => _names;

    /// <summary>The replacement of a spelling, made in the class given if it has none yet.</summary>
    public string Name(string spelling, bool quoted, NameClass cls)
    {
        var folded = fold(spelling, quoted);
        if (_kept.TryGetValue(folded, out var kept))
        {
            return kept;
        }

        if (_byKey.TryGetValue(folded, out var found))
        {
            return found.Replacement;
        }

        var made = new MappedName(Make(cls, folded), cls, spelling);
        _byKey[folded] = made;
        _names.Add(made);
        return made.Replacement;
    }

    /// <summary>
    /// Keeps a spelling as it is: the engine's own name, which maps to itself.  Nothing changes for a spelling that
    /// already has a replacement, and a spelling kept already maps to the spelling first kept.  The token check does
    /// not trust a spelling kept here, which <see cref="IsKeptFromEngineRow" /> does not answer for.
    /// </summary>
    public void Keep(string spelling, bool quoted) => _ = Store(fold(spelling, quoted), spelling);

    /// <summary>
    /// Keeps a spelling as <see cref="Keep" /> does, as a name of a row that the engine's part says is its own, which
    /// <see cref="IsKeptFromEngineRow" /> then answers for.
    /// </summary>
    public void KeepFromEngineRow(string spelling, bool quoted)
    {
        var folded = fold(spelling, quoted);
        if (Store(folded, spelling))
        {
            _ = _keptFromEngineRows.Add(folded);
        }
    }

    /// <summary>Whether a spelling has a replacement, and what it is; a kept spelling is its own.</summary>
    public bool TryGet(string spelling, bool quoted, out string replacement)
    {
        var folded = fold(spelling, quoted);
        if (_kept.TryGetValue(folded, out var kept))
        {
            replacement = kept;
            return true;
        }

        if (_byKey.TryGetValue(folded, out var found))
        {
            replacement = found.Replacement;
            return true;
        }

        replacement = string.Empty;
        return false;
    }

    /// <summary>The replacement of a literal's content, of class <see cref="NameClass.Literal" />.</summary>
    public string Literal(string content)
    {
        if (_literals.TryGetValue(content, out var found))
        {
            return found.Replacement;
        }

        var made = new MappedName(Make(NameClass.Literal, content), NameClass.Literal, content);
        _literals[content] = made;
        _names.Add(made);
        return made.Replacement;
    }

    /// <summary>
    /// Whether a spelling was kept from a row of the engine's, by <see cref="KeepFromEngineRow" />, compared as the
    /// engine folds it: the one kept spelling the token check trusts.  A spelling that has a replacement is not kept,
    /// and one kept by <see cref="Keep" /> alone is not kept from a row.
    /// </summary>
    public bool IsKeptFromEngineRow(string spelling, bool quoted) =>
        _keptFromEngineRows.Contains(fold(spelling, quoted));

    /// <summary>Whether a text is a replacement this mapping gave.</summary>
    public bool IsReplacement(string text) => _replacements.Contains(text);

    /// <summary>The first <paramref name="length" /> digits of a hash.</summary>
    internal static string Cut(string hex, int length) => hex[..length];

    // Keeps a folded spelling unless it has a replacement: whether it is kept now.
    private bool Store(string folded, string spelling)
    {
        if (_byKey.ContainsKey(folded))
        {
            return false;
        }

        _ = _kept.TryAdd(folded, spelling);
        return true;
    }

    private string Make(NameClass cls, string folded)
    {
        var hex = HexOf(key.Hash(Encoding.UTF8.GetBytes(NameClasses.WordOf(cls) + ":" + folded)));
        var prefix = NameClasses.PrefixOf(cls);
        for (var length = firstLength; length <= hex.Length; length += 2)
        {
            var candidate = prefix + Cut(hex, length);
            if (_replacements.Add(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("The hash of a name collides at every length.");
    }

    private static string HexOf(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            _ = text.Append(HexDigits[value >> 4]).Append(HexDigits[value & 0xF]);
        }

        return text.ToString();
    }
}
