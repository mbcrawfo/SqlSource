using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The parameters of the run through Dapper: a sample's literals read as the server reads them, a DBNull read as
// null, the input model built from the values of exactly its properties' types, and the input model's values passed
// under the names the SQL gives them.  It needs no server.
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

    [Fact]
    public void ValueOf_DbNull_IsNull() => RealisticParameters.ValueOf(DBNull.Value).ShouldBeNull();

    [Fact]
    public void ValueOf_Value_IsTheValue() => RealisticParameters.ValueOf(10L).ShouldBe(10L);

    [Fact]
    public void ModelOf_Values_IsTheRecordBuiltByItsConstructor() =>
        RealisticParameters.ModelOf(typeof(R), Parameters, [10L, null]).ShouldBe(new R(10, null));

    // Invoke would give the constructor 0 for a null, so a NULL sample would send 0 and pass.
    [Fact]
    public void ModelOf_NullForAValueTypeThatCannotBeNull_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticParameters.ModelOf(typeof(R), Parameters, [null, "a"])
        );

        thrown.Message.ShouldContain("@limit");
    }

    [Fact]
    public void ModelOf_ValueOfAnotherType_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticParameters.ModelOf(typeof(R), Parameters, [10, "a"])
        );

        thrown.Message.ShouldContain("@limit");
    }

    [Fact]
    public void ArgumentsOf_Model_NamesEachValueAsTheSql()
    {
        var arguments = RealisticParameters.ArgumentsOf(new R(10, null), Parameters);

        arguments.ParameterNames.ShouldBe(["limit", "q"]);
        arguments.Get<long>("limit").ShouldBe(10);
        arguments.Get<object?>("q").ShouldBeNull();
    }

    [Fact]
    public void ArgumentsOf_ModelWithoutAProperty_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticParameters.ArgumentsOf(new R(10, null), [new SidecarParameter("offset", 0, null, null, null)])
        );

        thrown.Message.ShouldContain("Offset");
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

    // An input model as the generator writes one, for the parameters limit and q.
    private sealed record R(long Limit, string? Q);
}
