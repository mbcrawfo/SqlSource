using System.Collections.Immutable;
using System.Linq;
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
    public static ParsedSqlFile Read(AdditionalText file, string normalizedPath, CancellationToken cancellationToken)
    {
        // A file that cannot be read is parsed as empty, which reports that the query has no SQL.
        var text = file.GetText(cancellationToken) ?? SourceText.From(string.Empty);
        var fileName = SqlPath.GetFileName(file.Path);
        var result = SqlFileParser.Parse(text.ToString(), fileName);

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
            // TODO: Phase 3 of the SQL queries epic emits a method for a block that has tokens.  Until then it gets
            // no member.
            if (block.Segments.Any(static segment => segment.Kind == SqlSegmentKind.Token))
            {
                continue;
            }

            queries.Add(
                new SqlQuery(
                    block.Name,
                    LocationInfo.From(file.Path, text, block.NameSpan),
                    block.Summary,
                    block.Segments[0].Text
                )
            );
        }

        return new ParsedSqlFile(
            normalizedPath,
            fileName,
            new EquatableArray<SqlQuery>(queries.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(errors.ToImmutable())
        );
    }
}
