using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Tests;

// What the tests of a capture describe, record and replay.
internal static class CaptureInputs
{
    // A query of no parameter.
    public static DescribeRequest GetUser { get; } =
        new("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty);

    // SQL that tells the escapes apart: a quote and a line end of two characters, which JSON escapes, and characters
    // of HTML and a letter that is not ASCII, which a capture keeps as they are.
    public const string AwkwardSql = "SELECT '<é>' AS \"a\"\r\nFROM t";
}
