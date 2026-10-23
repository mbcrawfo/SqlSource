using System;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>What a <see cref="TsqlToken" /> is.</summary>
internal enum TsqlTokenKind
{
    /// <summary>
    /// A name or a keyword as written, unquoted: <c>SELECT</c>, <c>dbo</c>, <c>@@ROWCOUNT</c>, <c>#t</c>.
    /// </summary>
    Word,

    /// <summary>A number or a binary literal: <c>1</c>, <c>1.5e-3</c>, <c>0x1F</c>.</summary>
    Number,

    /// <summary>A string literal, its <c>N</c> included: <c>'a'</c>, <c>N'a'</c>.</summary>
    String,

    /// <summary>A bracketed or double-quoted name: <c>[a b]</c>, <c>"a"</c>.</summary>
    QuotedName,

    /// <summary>A parameter or a variable, its <c>@</c> included.</summary>
    Parameter,

    /// <summary>An operator or a punctuation mark: <c>,</c>, <c>(</c>, <c>&lt;=</c>.</summary>
    Symbol,
}

/// <summary>One token of a T-SQL text.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">Its text as written.</param>
/// <param name="Start">The offset of its first character in the SQL.</param>
/// <param name="End">The offset after its last character in the SQL.</param>
internal readonly record struct TsqlToken(TsqlTokenKind Kind, string Text, int Start, int End)
{
    /// <summary>
    /// True for a <see cref="TsqlTokenKind.Word" /> equal to <paramref name="word" />, ignoring case.
    /// </summary>
    public bool Is(string word) =>
        Kind == TsqlTokenKind.Word && string.Equals(Text, word, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for the <see cref="TsqlTokenKind.Symbol" /> <paramref name="symbol" />.</summary>
    public bool IsSymbol(string symbol) => Kind == TsqlTokenKind.Symbol && Text == symbol;

    /// <summary>
    /// True for a token that can be a part of an object's or a column's name: a quoted name, or a word that does not
    /// start with <c>@</c>, which <c>@@ROWCOUNT</c> does.
    /// </summary>
    public bool IsName => Kind == TsqlTokenKind.QuotedName || (Kind == TsqlTokenKind.Word && Text[0] != '@');

    /// <summary>
    /// The name this token writes, without its quotes, or null for a token that writes no name.  A word is itself,
    /// <c>[a]]b]</c> is <c>a]b</c>, <c>"a""b"</c> is <c>a"b</c>, and a string, the alias of <c>'a' = expr</c>, is
    /// read the same way, <c>N'a''b'</c> as <c>a'b</c>.
    /// </summary>
    public string? Name =>
        Kind switch
        {
            TsqlTokenKind.Word => Text,
            TsqlTokenKind.QuotedName when Text[0] == '[' => Text[1..^1].Replace("]]", "]", StringComparison.Ordinal),
            TsqlTokenKind.QuotedName => Text[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal),
            TsqlTokenKind.String => Text[(Text.IndexOf('\'', StringComparison.Ordinal) + 1)..^1]
                .Replace("''", "'", StringComparison.Ordinal),
            TsqlTokenKind.Number or TsqlTokenKind.Parameter or TsqlTokenKind.Symbol => null,
            _ => null,
        };
}
