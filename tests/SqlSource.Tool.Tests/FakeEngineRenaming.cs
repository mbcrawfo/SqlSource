using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SqlSource.Parsing;
using SqlSource.Parsing.Quoting;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

/// <summary>
/// The engine part of the renaming for <see cref="FakeDescriber" />: PostgreSQL's lexer and word rule, or SQL Server's
/// for a test of brackets and prefixes, a small vocabulary, and a copy of a reused parameter as <c>&lt;name&gt;_&lt;n&gt;</c>.
/// </summary>
/// <remarks>The members the SQL renamer does not ask answer simply: no rule, no row of the engine's, nothing derived.</remarks>
internal sealed class FakeEngineRenaming(SqlDialect dialect = SqlDialect.PostgreSql) : IEngineRenaming
{
    /// <summary>The words the fake keeps, folded: a few keywords, a function, and the schema <c>public</c>.</summary>
    public static readonly IReadOnlySet<string> Vocabulary = new HashSet<string>(
        ["select", "from", "where", "join", "on", "as", "and", "or", "not", "null", "left", "count", "public"],
        StringComparer.Ordinal
    );

    public SqlDialect Dialect => dialect;

    public bool IsWordStart(char character) =>
        dialect == SqlDialect.SqlServer
            ? QuoteReader.IsNameCharacterOfSqlServer(character) && !char.IsDigit(character)
            : PostgresIdentifiers.IsWordStart(character);

    public bool IsWordPart(char character) =>
        dialect == SqlDialect.SqlServer
            ? QuoteReader.IsNameCharacterOfSqlServer(character)
            : PostgresIdentifiers.IsWordPart(character);

    // Character by character, as PostgreSQL folds: CA1308 rejects string.ToLowerInvariant.
    public string Fold(string spelling, bool quoted) =>
        quoted ? spelling : string.Concat(spelling.Select(char.ToLowerInvariant));

    public bool IsEngineWord(string spelling, bool quoted) => Vocabulary.Contains(Fold(spelling, quoted));

    public FieldRule? RuleOf(string method, string path) => null;

    public bool IsEngineRow(string method, string path, JsonObject row) => false;

    public (string Parameter, string Suffix)? CopyOf(string name, IReadOnlySet<string> queryParameters)
    {
        var separator = name.LastIndexOf('_');
        if (
            separator <= 0
            || separator == name.Length - 1
            || !name[(separator + 1)..].All(char.IsAsciiDigit)
            || !queryParameters.Contains(name[..separator])
        )
        {
            return null;
        }

        return (name[..separator], name[separator..]);
    }

    public void Register(RenamingCall call, IRenamingScope scope) { }

    public string RenameOwn(RenamingField field, IRenamingScope scope) => field.Value;

    public IEnumerable<string> WordsOf(RenamingField field) => [];

    public void Derive(RenamingCall call, IRenamingScope scope) { }
}
