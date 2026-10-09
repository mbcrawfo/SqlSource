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
    /// Parses <paramref name="file" />, whose path a type claims, with what MSBuild gives it.  A
    /// <c>-- dialect:</c> marker in the file replaces the dialect.
    /// </summary>
    public static ParsedSqlFile Read(FileParseInput file, CancellationToken cancellationToken) =>
        Read(
            file.File,
            file.NormalizedPath ?? string.Empty,
            file.Dialect,
            file.InvalidDialect,
            file.CommentsWanted,
            cancellationToken
        );

    /// <summary>
    /// Parses <paramref name="file" />.  <paramref name="commentsWanted" /> says that a level the parser cannot see
    /// may ask for <c>keep-comments</c>; <see cref="SqlFileParser.Parse" /> says what the parser then builds.
    /// </summary>
    public static ParsedSqlFile Read(
        AdditionalText file,
        string normalizedPath,
        SqlDialectChoice dialect,
        string? invalidDialect,
        bool commentsWanted,
        CancellationToken cancellationToken
    )
    {
        // A file that cannot be read is parsed as empty, which reports that the query has no SQL.
        var text = file.GetText(cancellationToken) ?? SourceText.From(string.Empty);
        var fileName = SqlPath.GetFileName(file.Path);
        var result = SqlFileParser.Parse(text.ToString(), fileName, dialect, commentsWanted);

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
                    block.Shape,
                    block.Segments,
                    block.KeptSegments,
                    block.Tokens,
                    block.Parameters,
                    block.Markers
                )
            );
        }

        return new ParsedSqlFile(
            normalizedPath,
            fileName,
            new EquatableArray<SqlQuery>(queries.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(errors.ToImmutable()),
            result.Dialect,
            invalidDialect
        );
    }
}
