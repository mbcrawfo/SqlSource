using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;

namespace SqlSource.Tests.Parsing;

// What the parser gives for a text, read as the tests of SqlFileParser compare it.
internal static class ParseOf
{
    public static SqlBlock[] Blocks(string text, string fileName = "Query.sql", SqlDialectChoice dialect = default)
    {
        var result = SqlFileParser.Parse(text, fileName, dialect);
        result.Errors.ShouldBeEmpty();
        return [.. result.Blocks];
    }

    public static SqlParseError[] Errors(string text, string fileName = "Query.sql", SqlDialectChoice dialect = default)
    {
        var result = SqlFileParser.Parse(text, fileName, dialect);
        result.Blocks.ShouldBeEmpty();
        return [.. result.Errors];
    }

    public static string? Kept(SqlBlock block) =>
        block.KeptSegments is { } segments
            ? string.Concat(
                segments.Select(static segment =>
                    segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
                )
            )
            : null;

    public static string Sql(SqlBlock block) =>
        string.Concat(
            block.Segments.Select(static segment =>
                segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
            )
        );
}
