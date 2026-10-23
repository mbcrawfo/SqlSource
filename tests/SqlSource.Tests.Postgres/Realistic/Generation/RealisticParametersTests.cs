using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The parameters of the run through Dapper: a sample's literals read as the server reads them.  It needs no server.
// The building of the input model and of Dapper's arguments from it is tested in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticParametersTests
{
    private static readonly SidecarParameter[] Parameters =
    [
        new("limit", 0, new OtherEngineType("bigint"), null, "inferred"),
        new("q", 1, new OtherEngineType("text"), true, "marker"),
    ];

    [Fact]
    public void SelectOf_Sample_CastsEachLiteralToItsType() =>
        RealisticParameters
            .SelectOf(Sample(("q", "'a'"), ("limit", "10")), Parameters)
            .ShouldBe("SELECT (10)::bigint, ('a')::text");

    [Fact]
    public void SelectOf_SampleWithoutAParameter_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticParameters.SelectOf(Sample(("limit", "10")), Parameters)
        );

        thrown.Message.ShouldContain("@q");
    }

    [Fact]
    public void SelectOf_ParameterWithoutAType_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticParameters.SelectOf(Sample(("id", "1")), [new SidecarParameter("id", 0, null, null, null)])
        );

        thrown.Message.ShouldContain("@id");
    }

    private static Dictionary<string, string> Sample(params (string Name, string Literal)[] values)
    {
        var sample = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, literal) in values)
        {
            sample.Add(name, literal);
        }

        return sample;
    }
}
