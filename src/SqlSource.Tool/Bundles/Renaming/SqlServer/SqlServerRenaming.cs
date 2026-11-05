using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Parsing;
using SqlSource.Parsing.Quoting;
using SqlSource.Tool.Describing.SqlServer;

namespace SqlSource.Tool.Bundles.Renaming.SqlServer;

/// <summary>
/// SQL Server's part of the renaming: its word rule, a fold that ignores case, the words of
/// <see cref="SqlServerVocabulary" />, the rules of <see cref="SqlServerFields" />, the rows of <c>sys</c>,
/// <c>INFORMATION_SCHEMA</c> and the system databases, the copies of a reused parameter, and the renaming of a
/// message by <see cref="SqlServerMessages" />.
/// </summary>
/// <remarks>
/// A name folds ignoring case whatever the database's collation, which no member is given.  SQL Server has no plan
/// and the describer asks no list in name order, so it registers and derives nothing: the tool's <c>AS [1]</c> is
/// kept as its word, and a copy of a reused parameter comes through <see cref="CopyOf" />.
/// </remarks>
internal sealed class SqlServerRenaming : IEngineRenaming
{
    // The system CLR types' assembly-qualified names, folded, around their version.
    private const string ClrHead = "MICROSOFT.SQLSERVER.TYPES.SQL";
    private const string ClrAssembly = ", MICROSOFT.SQLSERVER.TYPES, VERSION=";
    private const string ClrTail = ", CULTURE=NEUTRAL, PUBLICKEYTOKEN=89845DCD8080CC91";

    private static readonly string[] ClrTypes = ["GEOMETRY", "GEOGRAPHY", "HIERARCHYID"];

    // The methods whose rows are the columns of a result set, with their sources.
    private static readonly string[] ColumnMethods =
    [
        SqlServerCallNames.Columns,
        SqlServerCallNames.Operands,
        SqlServerCallNames.Table,
    ];

    // The schemas whose relations are the server's own, folded.
    private static readonly string[] SystemSchemas = ["SYS", "INFORMATION_SCHEMA"];

    private SqlServerRenaming() { }

    /// <summary>The one part, which holds nothing.</summary>
    public static SqlServerRenaming Instance { get; } = new();

    public SqlDialect Dialect => SqlDialect.SqlServer;

    public bool IsWordStart(char character) => QuoteReader.IsNameCharacterOfSqlServer(character);

    public bool IsWordPart(char character) => QuoteReader.IsNameCharacterOfSqlServer(character);

    public string Fold(string spelling, bool quoted) => SqlServerVocabulary.Fold(spelling);

    /// <summary>
    /// A word of the vocabulary; the ASCII digits of the tool's <c>AS [1]</c>; or the assembly-qualified name of a
    /// system CLR type, <c>geometry</c>, <c>geography</c> or <c>hierarchyid</c>, of any version.
    /// </summary>
    public bool IsEngineWord(string spelling, bool quoted) =>
        SqlServerVocabulary.Contains(spelling)
        || (spelling.Length > 0 && spelling.All(char.IsAsciiDigit))
        || IsSystemClrType(SqlServerVocabulary.Fold(spelling));

    public FieldRule? RuleOf(string method, string path) => SqlServerFields.RuleOf(method, path);

    /// <summary>
    /// A column of a result set whose source is of <c>sys</c>, of <c>INFORMATION_SCHEMA</c> or of a system database,
    /// or the request of <c>table</c> of a relation of <c>sys</c> or <c>INFORMATION_SCHEMA</c>.
    /// </summary>
    public bool IsEngineRow(string method, string path, JsonObject row)
    {
        if (!ColumnMethods.Contains(method, StringComparer.Ordinal))
        {
            return false;
        }

        return path switch
        {
            "answer.rows[]" => IsSystemSchema(row, "sourceSchema") || IsSystemDatabase(row, "sourceDatabase"),
            CaptureRenamer.Request => method == SqlServerCallNames.Table && IsSystemSchema(row, "schema"),
            _ => false,
        };
    }

    /// <summary>
    /// A copy that <c>ParameterCopies</c> makes, <c>&lt;parameter&gt;&lt;one or more _&gt;&lt;digits&gt;</c>, as the
    /// parameter and <c>_</c> with the digits: the renamed query's copies need one <c>_</c>, since a renamed name is
    /// its class's prefix and hex digits.  The shortest parameter that the query holds is taken.
    /// </summary>
    public (string Parameter, string Suffix)? CopyOf(string name, IReadOnlySet<string> queryParameters)
    {
        var digits = name.Length;
        while (digits > 0 && char.IsAsciiDigit(name[digits - 1]))
        {
            digits--;
        }

        if (digits == name.Length)
        {
            return null;
        }

        var stem = digits;
        while (stem > 0 && name[stem - 1] == '_')
        {
            stem--;
        }

        for (var end = stem; end < digits; end++)
        {
            var parameter = name[..end];
            if (parameter.Length > 0 && queryParameters.Contains(parameter))
            {
                return (parameter, "_" + name[digits..]);
            }
        }

        return null;
    }

    public void Register(RenamingCall call, IRenamingScope scope) { }

    public string RenameOwn(RenamingField field, IRenamingScope scope) =>
        SqlServerMessages.Rename(field.Value, scope, spelling => IsEngineWord(spelling, quoted: false));

    public IEnumerable<string> WordsOf(RenamingField field) => SqlServerMessages.WordsOf(field.Value);

    public void Derive(RenamingCall call, IRenamingScope scope) { }

    // A folded name of geometry, geography or hierarchyid, whose version is digits parted by dots.
    private static bool IsSystemClrType(string folded)
    {
        foreach (var type in ClrTypes)
        {
            var head = ClrHead + type + ClrAssembly;
            if (
                folded.Length > head.Length + ClrTail.Length
                && folded.StartsWith(head, StringComparison.Ordinal)
                && folded.EndsWith(ClrTail, StringComparison.Ordinal)
            )
            {
                return IsVersion(folded[head.Length..^ClrTail.Length]);
            }
        }

        return false;
    }

    // Groups of ASCII digits parted by single dots.
    private static bool IsVersion(string version) =>
        version.Split('.').All(static group => group.Length > 0 && group.All(char.IsAsciiDigit));

    private static bool IsSystemSchema(JsonObject row, string member) =>
        StringOf(row, member) is { } schema
        && SystemSchemas.Contains(SqlServerVocabulary.Fold(schema), StringComparer.Ordinal);

    private static bool IsSystemDatabase(JsonObject row, string member) =>
        StringOf(row, member) is { } database
        && SqlServerVocabulary.SystemDatabases.Any(system =>
            string.Equals(
                SqlServerVocabulary.Fold(system),
                SqlServerVocabulary.Fold(database),
                StringComparison.Ordinal
            )
        );

    private static string? StringOf(JsonObject row, string member) =>
        row[member] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : null;
}
