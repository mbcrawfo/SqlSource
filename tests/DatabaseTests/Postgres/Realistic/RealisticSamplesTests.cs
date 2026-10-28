using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic;

// The sample values of a case put in its SQL.  Their checks against the query's parameters are tested with
// RealisticSampleChecks in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticSamplesTests
{
    [Fact]
    public void Substitute_ParameterInAStringACommentOrAQuotedName_IsLeftAlone() =>
        RealisticSamples
            .Substitute("SELECT '@x', \"@x\", @x -- @x", Values(("x", "1")), Values(("x", "integer")))
            .ShouldBe("SELECT '@x', \"@x\", (1)::integer -- @x");

    [Fact]
    public void Substitute_TwoParameters_ReplacesEachWithItsCastLiteral() =>
        RealisticSamples
            .Substitute(
                "WHERE a = @a AND b = @B",
                Values(("a", "'x'"), ("b", "NULL")),
                Values(("a", "text"), ("b", "bigint"))
            )
            .ShouldBe("WHERE a = ('x')::text AND b = (NULL)::bigint");

    [Fact]
    public void Substitute_ParameterNotInTheSample_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                RealisticSamples.Substitute(
                    "WHERE a = @a AND c = @c",
                    Values(("a", "1")),
                    Values(("a", "integer"), ("c", "integer"))
                )
            )
            .Message.ShouldContain("@c");

    private static Dictionary<string, string> Values(params (string Name, string Value)[] values)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            found.Add(name, value);
        }

        return found;
    }
}
