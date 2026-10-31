using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// Parses one <c>.sql</c> file and turns the result into the generator's models.
/// </summary>
internal static class SqlFileReader
{
    /// <summary>
    /// Parses <paramref name="file" />, whose path a type claims, with what its input gives it.  A
    /// <c>-- dialect:</c> marker in the file replaces the dialect.
    /// </summary>
    public static ParsedSqlFile Read(FileParseInput file, CancellationToken cancellationToken) =>
        Read(file.File, file.NormalizedPath, file.Dialect, file.CommentsWanted, cancellationToken);

    /// <summary>
    /// Parses <paramref name="file" />, whose path <see cref="SqlPath.Normalize" /> gives as
    /// <paramref name="normalizedPath" />; a claimed file always has one.  <paramref name="commentsWanted" /> says
    /// that a level the parser cannot see may ask for <c>keep-comments</c>; <see cref="SqlFileParser.Parse" /> says
    /// what the parser then builds.
    /// </summary>
    public static ParsedSqlFile Read(
        AdditionalText file,
        string? normalizedPath,
        SqlDialectChoice dialect,
        bool commentsWanted,
        CancellationToken cancellationToken
    )
    {
        // A file that cannot be read is parsed as empty, which reports that the query has no SQL.
        var text = file.GetText(cancellationToken) ?? SourceText.From(string.Empty);
        var fileName = SqlPath.GetFileName(file.Path);
        var result = SqlFileParser.Parse(text.ToString(), fileName, dialect, commentsWanted);

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
                    block.Markers,
                    ModelMarkerOf(file.Path, text, block.InputModel),
                    ModelMarkerOf(file.Path, text, block.OutputModel),
                    ColumnMarkersOf(file.Path, text, block.ColumnMarkers),
                    DiagnosticsOf(file.Path, text, block.KeptErrors)
                )
            );
        }

        return new ParsedSqlFile(
            normalizedPath ?? string.Empty,
            fileName,
            new EquatableArray<SqlQuery>(queries.ToImmutable()),
            DiagnosticsOf(file.Path, text, result.Errors),
            DiagnosticsOf(file.Path, text, result.Warnings),
            result.Dialect
        );
    }

    // The array is made only for problems: a query's kept form has none but in a rare file, and a file has no warning
    // but in a rare one.
    private static EquatableArray<DiagnosticInfo> DiagnosticsOf(
        string path,
        SourceText text,
        EquatableArray<SqlParseError> errors
    )
    {
        if (errors.Count == 0)
        {
            return EquatableArray<DiagnosticInfo>.Empty;
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>(errors.Count);
        foreach (var error in errors)
        {
            diagnostics.Add(
                new DiagnosticInfo(
                    SqlDiagnostics.ForParseError(error.Kind),
                    LocationInfo.From(path, text, error.Span),
                    error.Arguments
                )
            );
        }

        return new EquatableArray<DiagnosticInfo>(diagnostics.MoveToImmutable());
    }

    private static EquatableArray<ColumnMarker> ColumnMarkersOf(
        string path,
        SourceText text,
        EquatableArray<SqlColumnMarker> markers
    )
    {
        if (markers.Count == 0)
        {
            return EquatableArray<ColumnMarker>.Empty;
        }

        var columns = ImmutableArray.CreateBuilder<ColumnMarker>(markers.Count);
        foreach (var marker in markers)
        {
            columns.Add(
                new ColumnMarker(
                    marker.Name,
                    marker.IsNullable,
                    marker.ClrType,
                    marker.ResetsType,
                    LocationInfo.From(path, text, marker.Span),
                    marker.IsJson
                )
            );
        }

        return new EquatableArray<ColumnMarker>(columns.MoveToImmutable());
    }

    private static ModelMarker? ModelMarkerOf(string path, SourceText text, SqlModelName? model) =>
        model is null ? null : new ModelMarker(model.Name, LocationInfo.From(path, text, model.Span));
}
