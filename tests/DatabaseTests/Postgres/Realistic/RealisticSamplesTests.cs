using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic;

// The sample values of a case put in its SQL.  Their checks against the query's parameters are tested with
// RealisticSampleChecks in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticSamplesTests
{
    [Fact]
    public void Substitute_ParameterInAStringACommentOrAQuotedName_IsLeftAlone() =>
        RealisticSamples
            .Substitute("SELECT '@x', \"@x\", @x -- @x", RealisticSample.Of(("x", "1")), [Described("x", "integer")])
            .ShouldBe("SELECT '@x', \"@x\", (1)::integer -- @x");

    // Over a sample that compares by ordinal, so that it is Substitute that finds @B's value ignoring case, and not
    // the dictionary.
    [Fact]
    public void Substitute_TwoParameters_ReplacesEachWithItsCastLiteral() =>
        RealisticSamples
            .Substitute(
                "WHERE a = @a AND b = @B",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "'x'", ["b"] = "NULL" },
                [Described("a", "text"), Described("b", "bigint")]
            )
            .ShouldBe("WHERE a = ('x')::text AND b = (NULL)::bigint");

    [Fact]
    public void Substitute_ParameterNotInTheSample_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                RealisticSamples.Substitute(
                    "WHERE a = @a AND c = @c",
                    RealisticSample.Of(("a", "1")),
                    [Described("a", "integer"), Described("c", "integer")]
                )
            )
            .Message.ShouldContain("@c");

    private static DescribedParameter Described(string name, string type) => new(name, new OtherEngineType(type), null);
}
