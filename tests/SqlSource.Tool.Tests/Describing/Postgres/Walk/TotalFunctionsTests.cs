using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// The closed tables of the operators and functions of pg_catalog that never return NULL for arguments that are not,
// looked up by their exact argument types.  tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs holds each
// row to the catalog of 14 and 18.
public sealed class TotalFunctionsTests
{
    [Theory]
    [InlineData("*", BuiltInTypes.Numeric, BuiltInTypes.Numeric, BuiltInTypes.Numeric)]
    [InlineData("*", BuiltInTypes.Int8, BuiltInTypes.Int4, BuiltInTypes.Int8)]
    [InlineData("/", BuiltInTypes.Float4, BuiltInTypes.Float8, BuiltInTypes.Float8)]
    [InlineData("=", BuiltInTypes.Xid, BuiltInTypes.Int4, BuiltInTypes.Bool)]
    [InlineData("||", BuiltInTypes.Text, BuiltInTypes.Text, BuiltInTypes.Text)]
    public void OperatorResult_RowOfTheTable_IsItsResult(string name, long left, long right, long expected) =>
        TotalFunctions.OperatorResult(name, left, right).ShouldBe(expected);

    [Theory]
    [InlineData("=", BuiltInTypes.Varchar, BuiltInTypes.Varchar)]
    [InlineData("*", BuiltInTypes.Numeric, BuiltInTypes.Int4)]
    [InlineData("*", TotalFunctions.UnknownType, TotalFunctions.UnknownType)]
    public void OperatorResult_SignatureNotInTheTable_IsNull(string name, long left, long right) =>
        TotalFunctions.OperatorResult(name, left, right).ShouldBeNull();

    [Fact]
    public void FunctionResult_Upper_IsText() =>
        TotalFunctions.FunctionResult("upper", [BuiltInTypes.Text]).ShouldBe(BuiltInTypes.Text);

    [Fact]
    public void FunctionResult_DateTrunc_IsItsTimestampsType() =>
        TotalFunctions
            .FunctionResult("date_trunc", [BuiltInTypes.Text, BuiltInTypes.Timestamptz])
            .ShouldBe(BuiltInTypes.Timestamptz);

    [Fact]
    public void FunctionResult_ArgumentOfNoKnownType_IsNull() =>
        TotalFunctions.FunctionResult("upper", [TotalFunctions.UnknownType]).ShouldBeNull();

    [Fact]
    public void FunctionResult_FunctionNotInTheTable_IsNull() =>
        TotalFunctions.FunctionResult("array_agg", [BuiltInTypes.Int4]).ShouldBeNull();
}
