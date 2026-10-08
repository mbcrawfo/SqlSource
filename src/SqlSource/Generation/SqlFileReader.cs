using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// Parses one <c>.sql</c> file and turns the result into the generator's models.
/// </summary>
internal static class SqlFileReader
{
    /// <summary>
    /// Parses <paramref name="file" /> with the dialect that MSBuild gives it.  A <c>-- dialect:</c> marker in the
    /// file replaces that dialect.
    /// </summary>
    public static ParsedSqlFile Read(FileDialect file, string normalizedPath, CancellationToken cancellationToken) =>
        Read(file.File, normalizedPath, file.Dialect, file.InvalidValue, cancellationToken);

    public static ParsedSqlFile Read(
        AdditionalText file,
        string normalizedPath,
        SqlDialectChoice dialect,
        string? invalidDialect,
        CancellationToken cancellationToken
    )
    {
        // A file that cannot be read is parsed as empty, which reports that the query has no SQL.
        var text = file.GetText(cancellationToken) ?? SourceText.From(string.Empty);
        var fileName = SqlPath.GetFileName(file.Path);
        var result = SqlFileParser.Parse(text.ToString(), fileName, dialect);

        var errors = ImmutableArray.CreateBuilder<DiagnosticInfo>(result.Errors.Count);
        foreach (var error in result.Errors)
        {
            errors.Add(
                new DiagnosticInfo(
                    SqlDiagnostics.ForParseError(error.Kind),
                    LocationInfo.From(file.Path, text, error.Span),
                    error.Arguments
                )
            );
        }

        var queries = ImmutableArray.CreateBuilder<SqlQuery>(result.Blocks.Count);
        foreach (var block in result.Blocks)
        {
            queries.Add(
                new SqlQuery(
                    block.Name,
                    LocationInfo.From(file.Path, text, block.NameSpan),
                    block.Summary,
                    block.Segments,
                    block.Tokens,
                    block.TokenValidation,
                    block.Parameters
                )
            );
        }

        return new ParsedSqlFile(
            normalizedPath,
            fileName,
            new EquatableArray<SqlQuery>(queries.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(errors.ToImmutable()),
            invalidDialect
        );
    }
}
