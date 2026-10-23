using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// The parameters of the run through Dapper: a sample's expressions read as the server reads them.  It needs no server.
// The building of the input model and of Dapper's arguments from it is tested in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticParametersTests
{
    private static readonly SidecarParameter[] Parameters =
    [
        new("limit", 0, new SqlServerType("bigint", 8, 19, 0, null), null, "inferred"),
        new("q", 1, new SqlServerType("nvarchar(10)", 20, 0, 0, null), true, "marker"),
    ];

    [Fact]
    public void SelectOf_Sample_CastsEachExpressionToItsType() =>
        RealisticParameters
            .SelectOf(Sample(("q", "N'a'"), ("limit", "10")), Parameters)
            .ShouldBe("SELECT CAST((\n10\n) AS bigint), CAST((\nN'a'\n) AS nvarchar(10))");

    [Fact]
    public void SelectOf_SampleNamedInAnotherCase_IsFound() =>
        RealisticParameters
            .SelectOf(Sample(("Q", "N'a'"), ("LIMIT", "10")), Parameters)
            .ShouldBe("SELECT CAST((\n10\n) AS bigint), CAST((\nN'a'\n) AS nvarchar(10))");

    // A comment at the end of a sample runs to the end of its line, so a parenthesis on that line would be lost.
    [Fact]
    public void SelectOf_SampleEndingInAComment_KeepsItsParenthesis() =>
        RealisticParameters
            .SelectOf(Sample(("q", "N'a' -- any"), ("limit", "10")), Parameters)
            .ShouldBe("SELECT CAST((\n10\n) AS bigint), CAST((\nN'a' -- any\n) AS nvarchar(10))");

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

    private static Dictionary<string, string> Sample(params (string Name, string Expression)[] values)
    {
        var sample = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, expression) in values)
        {
            sample.Add(name, expression);
        }

        return sample;
    }
}
