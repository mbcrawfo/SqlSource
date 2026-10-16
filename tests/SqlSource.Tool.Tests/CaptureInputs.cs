using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Tests;

// What the tests of a capture describe, record and replay.
internal static class CaptureInputs
{
    // A query of no parameter, by the name of the fixtures' first.
    public static DescribeRequest GetUser { get; } =
        new("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty);

    // SQL that JSON has to escape: a quote, characters of HTML, a letter that is not ASCII and a line end of two
    // characters.
    public const string AwkwardSql = "SELECT '<é>' AS \"a\"\r\nFROM t";
}
