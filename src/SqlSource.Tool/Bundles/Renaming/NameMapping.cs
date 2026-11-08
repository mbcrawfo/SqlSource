using System;
using System.Collections.Generic;
using System.Text;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The mapping of one renaming: one name has one replacement throughout, whatever class it is found in, and its class
/// is that of the first place it is found.
/// </summary>
/// <remarks>
/// A name is what the engine's <c>fold</c> takes for one; a spelling is how it is written, as <c>spelled</c> gives
/// it.  A replacement is the class's prefix and the HMAC-SHA256 of the class word and the folded spelling, keyed by
/// the run's key and cut to eight lower-case hex digits; a replacement that is already given lengthens by two digits
/// until it is free.  Two spellings of one name that <c>spelled</c> tells apart get replacements that differ by case
/// alone, so that they stay apart where the engine's pipeline compares by ordinal and are one name where it ignores
/// case: the first has the lower-case replacement, and the k-th has the letters of it, the prefix's and each hex
/// letter <c>a</c> to <c>f</c>, left to right, upper-cased where bit i of k is set.  When the letters cannot tell
/// another spelling apart, it gets a replacement of its own, made from the class word, the folded spelling and the
/// spelling, which differs from the rest ignoring case too, and <see cref="FellBack" /> is true: such a replacement
/// may change an answer where the pipeline ignores case, so a refused renaming whose mapping fell back is tried again
/// with another key.  A spelling that is kept maps to the spelling first kept with its <c>spelled</c>, wins over a
/// later <see cref="Name" /> and carries no class; one that already has a replacement keeps it.  Only a spelling kept
/// from a row of the engine's is kept for the token check, which trusts no other.  The renaming starts at eight
/// digits; a test gives a shorter <c>firstLength</c> to force a collision.
/// </remarks>
internal sealed class NameMapping(
    RenamingKey key,
    Func<string, bool, string> fold,
    Func<string, bool, string> spelled,
    int firstLength
)
{
    private const int FirstLength = 8;
    private const string HexDigits = "0123456789abcdef";

    // The spellings of one name: the first's name, which gives the class, and each spelling's replacement.
    private sealed class Spellings(MappedName first)
    {
        public MappedName First { get; } = first;

        public Dictionary<string, string> Replacements { get; } = [with(StringComparer.Ordinal)];
    }

    private readonly Dictionary<string, Spellings> _byKey = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, Dictionary<string, string>> _kept = [with(StringComparer.Ordinal)];
    private readonly HashSet<string> _keptFromEngineRows = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, MappedName> _literals = [with(StringComparer.Ordinal)];
    private readonly HashSet<string> _replacements = [with(StringComparer.Ordinal)];
    private readonly List<MappedName> _names = [];

    /// <summary>A mapping whose replacements start at eight hex digits.</summary>
    public NameMapping(RenamingKey key, Func<string, bool, string> fold, Func<string, bool, string> spelled)
        : this(key, fold, spelled, FirstLength) { }

    /// <summary>The names made, in the order they were made; a kept name is left out.</summary>
    public IReadOnlyList<MappedName> Names => _names;

    /// <summary>
    /// Whether a spelling got a replacement of its own because the letters of its name's first replacement could not
    /// tell it apart by case.
    /// </summary>
    public bool FellBack { get; private set; }

    /// <summary>The replacement of a spelling, made in the class given if it has none yet.</summary>
    public string Name(string spelling, bool quoted, NameClass cls)
    {
        var folded = fold(spelling, quoted);
        var exact = spelled(spelling, quoted);
        if (_kept.TryGetValue(folded, out var kept))
        {
            if (!kept.TryGetValue(exact, out var keptSpelling))
            {
                keptSpelling = spelling;
                kept[exact] = spelling;
            }

            return keptSpelling;
        }

        if (!_byKey.TryGetValue(folded, out var found))
        {
            var first = new MappedName(Make(cls, folded), cls, spelling);
            found = new Spellings(first);
            found.Replacements[exact] = first.Replacement;
            _byKey[folded] = found;
            _names.Add(first);
            return first.Replacement;
        }

        if (found.Replacements.TryGetValue(exact, out var replacement))
        {
            return replacement;
        }

        var variant = Variant(found, folded, exact);
        found.Replacements[exact] = variant;
        _names.Add(new MappedName(variant, found.First.Class, spelling));
        return variant;
    }

    /// <summary>
    /// Keeps a spelling as it is: the engine's own name, which maps to itself.  Nothing changes for a spelling that
    /// already has a replacement, and a spelling kept already maps to the spelling first kept.  The token check does
    /// not trust a spelling kept here, which <see cref="IsKeptFromEngineRow" /> does not answer for.
    /// </summary>
    public void Keep(string spelling, bool quoted) =>
        _ = Store(fold(spelling, quoted), spelled(spelling, quoted), spelling);

    /// <summary>
    /// Keeps a spelling as <see cref="Keep" /> does, as a name of a row that the engine's part says is its own, which
    /// <see cref="IsKeptFromEngineRow" /> then answers for.
    /// </summary>
    public void KeepFromEngineRow(string spelling, bool quoted)
    {
        var folded = fold(spelling, quoted);
        if (Store(folded, spelled(spelling, quoted), spelling))
        {
            _ = _keptFromEngineRows.Add(folded);
        }
    }

    /// <summary>
    /// Whether a spelling is of a name that has a replacement or is kept, whatever case it is spelled in.
    /// </summary>
    public bool IsMapped(string spelling, bool quoted)
    {
        var folded = fold(spelling, quoted);
        return _kept.ContainsKey(folded) || _byKey.ContainsKey(folded);
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

    // Keeps a spelling unless its name has a replacement: whether it is kept now.
    private bool Store(string folded, string exact, string spelling)
    {
        if (_byKey.ContainsKey(folded))
        {
            return false;
        }

        if (!_kept.TryGetValue(folded, out var kept))
        {
            kept = [with(StringComparer.Ordinal)];
            _kept[folded] = kept;
        }

        _ = kept.TryAdd(exact, spelling);
        return true;
    }

    // The replacement of a spelling of a name that has others: the next case of the first's letters, or when they are
    // all given, one of its own.
    private string Variant(Spellings found, string folded, string exact)
    {
        var first = found.First.Replacement;
        var prefix = NameClasses.PrefixOf(found.First.Class);
        var letters = new List<int> { 0 };
        for (var i = prefix.Length; i < first.Length; i++)
        {
            if (first[i] is >= 'a' and <= 'f')
            {
                letters.Add(i);
            }
        }

        var variant = found.Replacements.Count;
        if (variant >= 1L << letters.Count)
        {
            FellBack = true;
            return Make(found.First.Class, folded + ":" + exact);
        }

        var text = first.ToCharArray();
        for (var bit = 0; bit < letters.Count; bit++)
        {
            if (((variant >> bit) & 1) != 0)
            {
                text[letters[bit]] = char.ToUpperInvariant(text[letters[bit]]);
            }
        }

        var made = new string(text);
        _ = _replacements.Add(made);
        return made;
    }

    /// <summary>
    /// Whether a text is shaped as a replacement: a lower-case ASCII letter, <c>_</c>, then eight or more lower-case
    /// hex digits, what <c>Make</c> gives at the renaming's first length.
    /// </summary>
    /// <remarks>
    /// A variant that differs by case is not, and PostgreSQL, whose spelled form is its fold, never has one; a test's
    /// shorter <c>firstLength</c> is not.
    /// </remarks>
    /// <param name="text">The text.</param>
    internal static bool IsShapedAsReplacement(ReadOnlySpan<char> text)
    {
        if (text.Length < 2 + FirstLength || !char.IsAsciiLetterLower(text[0]) || text[1] != '_')
        {
            return false;
        }

        foreach (var character in text[2..])
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }

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
