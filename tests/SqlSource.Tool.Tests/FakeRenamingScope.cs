using System;
using System.Collections.Generic;
using SqlSource.Tool.Bundles.Renaming;

namespace SqlSource.Tool.Tests;

/// <summary>
/// A scope of the renaming for testing an engine's part alone: a name is the prefix of the class it is first named in
/// and its spelling, a literal <c>l_</c> and its content, a parameter <c>p_</c> and its name, and a refusal is
/// recorded.
/// </summary>
/// <remarks>
/// A spelling kept through <see cref="Keep" />, or one that <c>isEngineWord</c> says is the engine's, is its own.
/// Spellings are compared as given, each with its quoting, or as <c>fold</c> folds them when it is given, and a name
/// is then the prefix and the folded spelling.  SQL is renamed word by word through <see cref="Name" />, unquoted and
/// in the class given: a word is a letter or an underscore and the letters, digits and underscores after it.
/// </remarks>
internal sealed class FakeRenamingScope(
    Func<string, bool, bool>? isEngineWord = null,
    Func<string, bool, string>? fold = null
) : IRenamingScope
{
    private readonly HashSet<(string Spelling, bool Quoted)> _kept = [];
    private readonly Dictionary<(string Spelling, bool Quoted), string> _named = [];

    /// <summary>The refusals made, in order.</summary>
    public List<RenamingRefusal> Refusals { get; } = [];

    public string Name(string spelling, bool quoted, NameClass cls)
    {
        var key = KeyOf(spelling, quoted);
        if (_kept.Contains(key) || (isEngineWord?.Invoke(spelling, quoted) ?? false))
        {
            return spelling;
        }

        if (!_named.TryGetValue(key, out var replacement))
        {
            replacement = NameClasses.PrefixOf(cls) + key.Spelling;
            _named.Add(key, replacement);
        }

        return replacement;
    }

    public void Keep(string spelling, bool quoted) => _ = _kept.Add(KeyOf(spelling, quoted));

    public bool IsMapped(string spelling, bool quoted)
    {
        var key = KeyOf(spelling, quoted);
        return _kept.Contains(key) || _named.ContainsKey(key);
    }

    public string Literal(string content) => NameClasses.PrefixOf(NameClass.Literal) + content;

    public RenamedText Sql(string sql, NameClass newWords)
    {
        var builder = new RenamedTextBuilder(sql);
        var index = 0;
        while (index < sql.Length)
        {
            if (!(char.IsLetter(sql[index]) || sql[index] == '_'))
            {
                index++;
                continue;
            }

            var end = index + 1;
            while (end < sql.Length && (char.IsLetterOrDigit(sql[end]) || sql[end] == '_'))
            {
                end++;
            }

            builder.CopyTo(index);
            builder.ReplaceTo(end, Name(sql[index..end], quoted: false, newWords));
            index = end;
        }

        return builder.Build();
    }

    public string Parameter(string name) => NameClasses.PrefixOf(NameClass.Parameter) + name;

    public void Refuse(RenamingRefusal refusal) => Refusals.Add(refusal);

    // A folded spelling is one name whatever its quoting, as the engine compares it.
    private (string Spelling, bool Quoted) KeyOf(string spelling, bool quoted) =>
        fold is null ? (spelling, quoted) : (fold(spelling, quoted), true);
}
