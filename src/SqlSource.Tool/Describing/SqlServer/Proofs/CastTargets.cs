using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The target types of <c>CAST</c> and <c>CONVERT</c> whose conversion of a value that is not null is not null.
/// </summary>
/// <remarks>
/// A CLR type, <c>geometry</c>, <c>geography</c>, <c>hierarchyid</c> or a user's, converts a string through its
/// <c>Parse</c>, which may answer its Null: <c>CAST(N'NULL' AS geometry)</c> is <c>NULL</c>.  So only a built-in type
/// that is no CLR type, named as one word and without a schema, is a target, its facets <c>(n)</c>, <c>(max)</c> or
/// <c>(p, s)</c>.  A bracketed name, a name with a schema, a synonym such as <c>integer</c>, a type whose conversion
/// is not known, as <c>json</c>, and anything else is none.  A user's type cannot be named as a system type: the
/// server refuses <c>CREATE TYPE dbo.[date]</c>, so <c>date</c> is always the system's.
/// </remarks>
internal static class CastTargets
{
    private static readonly HashSet<string> BuiltIn =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "bigint",
        "int",
        "smallint",
        "tinyint",
        "bit",
        "decimal",
        "numeric",
        "money",
        "smallmoney",
        "float",
        "real",
        "date",
        "time",
        "datetime",
        "datetime2",
        "datetimeoffset",
        "smalldatetime",
        "char",
        "varchar",
        "nchar",
        "nvarchar",
        "binary",
        "varbinary",
        "uniqueidentifier",
        "xml",
        "sql_variant",
        "sysname",
    ];

    /// <summary>
    /// True when the range is <c>name</c>, <c>name(n)</c>, <c>name(max)</c> or <c>name(p, s)</c>, the name one of
    /// the built-in types that are no CLR type, as one word.
    /// </summary>
    public static bool IsBuiltIn(IReadOnlyList<TsqlToken> tokens, TokenRange range)
    {
        if (
            range.IsEmpty
            || tokens[range.Start].Kind != TsqlTokenKind.Word
            || !BuiltIn.Contains(tokens[range.Start].Text)
        )
        {
            return false;
        }

        var at = range.Start + 1;
        if (at == range.End)
        {
            return true;
        }

        if (
            at + 1 >= range.End
            || !tokens[at].IsSymbol("(")
            || !(tokens[at + 1].Is("MAX") || IsInteger(tokens[at + 1]))
        )
        {
            return false;
        }

        at += 2;
        if (at + 1 < range.End && tokens[at].IsSymbol(",") && IsInteger(tokens[at + 1]))
        {
            at += 2;
        }

        return at + 1 == range.End && tokens[at].IsSymbol(")");
    }

    private static bool IsInteger(TsqlToken token) =>
        token.Kind == TsqlTokenKind.Number && token.Text.AsSpan().IndexOfAnyExceptInRange('0', '9') < 0;
}
