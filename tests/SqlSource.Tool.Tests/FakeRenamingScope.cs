using System;
using System.Collections.Generic;
using SqlSource.Tool.Bundles.Renaming;

namespace SqlSource.Tool.Tests;

/// <summary>
/// A scope of the renaming for testing an engine's part alone: a name is its class's prefix and its spelling, a
/// literal <c>l_</c> and its content, a parameter <c>p_</c> and its name, and a refusal is recorded.
/// </summary>
/// <remarks>
/// A spelling kept through <see cref="Keep" />, or one that <c>isEngineWord</c> says is the engine's, is its own.
/// Spellings are compared as given, without the engine's fold.
/// </remarks>
internal sealed class FakeRenamingScope(Func<string, bool, bool>? isEngineWord = null) : IRenamingScope
{
    private readonly HashSet<(string Spelling, bool Quoted)> _kept = [];
    private readonly HashSet<(string Spelling, bool Quoted)> _named = [];

    /// <summary>The refusals made, in order.</summary>
    public List<RenamingRefusal> Refusals { get; } = [];

    public string Name(string spelling, bool quoted, NameClass cls)
    {
        if (_kept.Contains((spelling, quoted)) || (isEngineWord?.Invoke(spelling, quoted) ?? false))
        {
            return spelling;
        }

        _ = _named.Add((spelling, quoted));
        return NameClasses.PrefixOf(cls) + spelling;
    }

    public void Keep(string spelling, bool quoted) => _ = _kept.Add((spelling, quoted));

    public bool IsMapped(string spelling, bool quoted) =>
        _kept.Contains((spelling, quoted)) || _named.Contains((spelling, quoted));

    public string Literal(string content) => NameClasses.PrefixOf(NameClass.Literal) + content;

    public RenamedText Sql(string sql, NameClass newWords) =>
        throw new NotSupportedException("The fake scope renames no SQL.");

    public string Parameter(string name) => NameClasses.PrefixOf(NameClass.Parameter) + name;

    public void Refuse(RenamingRefusal refusal) => Refusals.Add(refusal);
}
