using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The input model of a case's sample in the run through Dapper, whatever the engine: a DBNull read as null, and the
// input model built from the values of exactly its properties' types.
public sealed class RealisticInputsTests
{
    private static readonly SidecarParameter[] Parameters =
    [
        new("limit", 0, new OtherEngineType("bigint"), null, "inferred"),
        new("q", 1, new OtherEngineType("text"), true, "marker"),
    ];

    [Fact]
    public void ValueOf_DbNull_IsNull() => RealisticInputs.ValueOf(DBNull.Value).ShouldBeNull();

    [Fact]
    public void ValueOf_Value_IsTheValue() => RealisticInputs.ValueOf(10L).ShouldBe(10L);

    [Fact]
    public void ModelOf_Values_IsTheRecordBuiltByItsConstructor() =>
        RealisticInputs.ModelOf(typeof(R), Parameters, [10L, null]).ShouldBe(new R(10, null));

    // Invoke would give the constructor 0 for a null, so a NULL sample would send 0 and pass.
    [Fact]
    public void ModelOf_NullForAValueTypeThatCannotBeNull_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticInputs.ModelOf(typeof(R), Parameters, [null, "a"])
        );

        thrown.Message.ShouldContain("@limit");
    }

    [Fact]
    public void ModelOf_ValueOfAnotherType_ThrowsNamingIt()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticInputs.ModelOf(typeof(R), Parameters, [10, "a"])
        );

        thrown.Message.ShouldContain("@limit");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ModelOf_ValuesNotOneForEachParameter_ThrowsNamingTheCounts(int count)
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticInputs.ModelOf(typeof(R), Parameters, new object?[count])
        );

        thrown.Message.ShouldContain($"count of values is {count}");
        thrown.Message.ShouldContain("count of parameters is 2");
    }

    // An input model as the generator writes one, for the parameters limit and q.
    private sealed record R(long Limit, string? Q);
}
