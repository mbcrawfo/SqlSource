using System;
using System.Collections.Generic;
using System.Globalization;
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
/// <c>INFORMATION_SCHEMA</c> and the system resource, the copies of a reused parameter, and the renaming of a message
/// by <see cref="SqlServerMessages" />.
/// </summary>
/// <remarks>
/// A name folds ignoring case whatever the database's collation, and each spelling keeps a case of its own through
/// <see cref="Spelled" />, so the pipeline's comparisons by ordinal and ignoring case both keep their answers, unless
/// the first replacement's letters cannot tell the spellings apart (<see cref="NameMapping.FellBack" />), when a
/// changed answer is refused and the renaming tried again with a new key.  SQL Server has no plan and the describer
/// asks no list in name order, so it registers and derives nothing: the tool's <c>AS [1]</c> is kept as its word, and
/// a copy of a reused parameter comes through <see cref="CopiesIn" />.
/// </remarks>
internal sealed class SqlServerRenaming : IEngineRenaming
{
    // The system CLR types' assembly-qualified names, folded, around their version.
    private const string ClrHead = "MICROSOFT.SQLSERVER.TYPES.SQL";
    private const string ClrAssembly = ", MICROSOFT.SQLSERVER.TYPES, VERSION=";
    private const string ClrTail = ", CULTURE=NEUTRAL, PUBLICKEYTOKEN=89845DCD8080CC91";

    // The database whose relations are the server's own, folded: the system resource, which the describer names as
    // the source of a system view's columns.  A user's tables can be in master, msdb, model or tempdb, so no other
    // database says that a row is the engine's.
    private const string SystemResource = "MSSQLSYSTEMRESOURCE";

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

    /// <summary>
    /// An ASCII digit, or a <c>$</c> at the end or before an ASCII digit or a character that is no word part: the
    /// server reads <c>$</c>, <c>$12.50</c>, <c>$ 12</c>, <c>$-12</c> and <c>$.5</c> as money, and <c>$action</c> as
    /// a word.
    /// </summary>
    public bool IsNumberStart(string text, int at, int end) =>
        char.IsAsciiDigit(text[at])
        || (text[at] == '$' && (at + 1 == end || char.IsAsciiDigit(text[at + 1]) || !IsWordPart(text[at + 1])));

    public bool IsWordStart(char character) => QuoteReader.IsNameCharacterOfSqlServer(character);

    public bool IsWordPart(char character) => QuoteReader.IsNameCharacterOfSqlServer(character);

    public string Fold(string spelling, bool quoted) => SqlServerVocabulary.Fold(spelling);

    public string Spelled(string spelling, bool quoted) => spelling;

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
    /// A column of a result set whose source is of <c>sys</c>, of <c>INFORMATION_SCHEMA</c> or of the system resource,
    /// <c>mssqlsystemresource</c>, of the server itself and under its source column's own name, or the request of
    /// <c>table</c> of a relation of <c>sys</c> or <c>INFORMATION_SCHEMA</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row of <c>dbo</c> or another schema of <c>master</c>, <c>msdb</c>, <c>model</c> or <c>tempdb</c> is the
    /// user's, since a user's tables are there when the connection names that database.
    /// </para>
    /// <para>
    /// A row's <c>name</c> is the column's alias, which is the user's, and the renaming keeps every name of a row of
    /// the engine's, an alias too, and every spelling of it in the bundle.  So a column of the server's is the
    /// engine's only when its name, without a nullability mark and folded, is its <c>sourceColumn</c> folded, is the
    /// ASCII digits of the tool's <c>AS [n]</c>, or is null.  A row's <c>sourceServer</c> is the name of a linked
    /// server, which is the user's too, so a column read through one is not the engine's either.  Such a column is
    /// renamed whole, which is safe, since a renamed object of <c>sys</c> still replays.
    /// </para>
    /// </remarks>
    public bool IsEngineRow(string method, string path, JsonObject row)
    {
        if (!ColumnMethods.Contains(method, StringComparer.Ordinal))
        {
            return false;
        }

        return path switch
        {
            "answer.rows[]" => (IsSystemSchema(row, "sourceSchema") || IsSystemResource(row, "sourceDatabase"))
                && row["sourceServer"] is null
                && IsUnaliased(row),
            CaptureRenamer.Request => method == SqlServerCallNames.Table && IsSystemSchema(row, "schema"),
            _ => false,
        };
    }

    /// <summary>
    /// The copies that a <c>parameters</c> request records in <c>reused</c>: each copy of an entry is a copy of the
    /// entry's parameter, whose suffix is <c>_</c> and the copy's position among the entry's copies, from one, since
    /// that is what <c>ParameterCopies</c> makes from the renamed query, whose names a cut or a longer separator never
    /// needs.  Any other call and a request with no <c>reused</c> record none, nor does an entry with a member of
    /// another JSON kind.
    /// </summary>
    public IEnumerable<ParameterCopy> CopiesIn(string method, JsonObject call)
    {
        if (
            method != SqlServerCallNames.Parameters
            || call[CaptureRenamer.Request] is not JsonObject request
            || request["reused"] is not JsonArray reused
        )
        {
            yield break;
        }

        foreach (var entry in reused)
        {
            if (
                entry is not JsonObject found
                || StringOf(found, "name") is not { } parameter
                || found["copies"] is not JsonArray copies
                || !copies.All(static copy => copy is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            )
            {
                continue;
            }

            for (var index = 0; index < copies.Count; index++)
            {
                yield return new ParameterCopy(
                    copies[index]!.GetValue<string>(),
                    parameter,
                    "_" + (index + 1).ToString(CultureInfo.InvariantCulture)
                );
            }
        }
    }

    public void Register(RenamingCall call, IRenamingScope scope) { }

    public string RenameOwn(RenamingField field, IRenamingScope scope) => SqlServerMessages.Rename(field.Value, scope);

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

    // A column's name is its source column's, without a nullability mark and folded, the tool's AS [n], or null.
    private static bool IsUnaliased(JsonObject row)
    {
        if (row["name"] is null)
        {
            return true;
        }

        if (StringOf(row, "name") is not { } name)
        {
            return false;
        }

        if (name.Length > 0 && name.All(char.IsAsciiDigit))
        {
            return true;
        }

        return StringOf(row, "sourceColumn") is { } column
            && string.Equals(
                SqlServerVocabulary.Fold(RenamingRun.MarkOf(name).Stem),
                SqlServerVocabulary.Fold(column),
                StringComparison.Ordinal
            );
    }

    private static bool IsSystemResource(JsonObject row, string member) =>
        StringOf(row, member) is { } database
        && string.Equals(SqlServerVocabulary.Fold(database), SystemResource, StringComparison.Ordinal);

    private static string? StringOf(JsonObject row, string member) =>
        row[member] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : null;
}
