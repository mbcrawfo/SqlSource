using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// The closed tables of the operators, functions and aggregates of pg_catalog that never return NULL for arguments that
// are not, looked up by their exact argument types.  tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs holds
// each row to the catalog of 14 and 18.
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

    [Theory]
    [InlineData("sum", BuiltInTypes.Int4, BuiltInTypes.Int8)]
    [InlineData("sum", BuiltInTypes.Int8, BuiltInTypes.Numeric)]
    [InlineData("avg", BuiltInTypes.Float4, BuiltInTypes.Float8)]
    [InlineData("max", BuiltInTypes.Timestamptz, BuiltInTypes.Timestamptz)]
    [InlineData("bool_and", BuiltInTypes.Bool, BuiltInTypes.Bool)]
    public void AggregateResult_RowOfTheTable_IsItsResult(string name, long argument, long expected) =>
        TotalFunctions.AggregateResult(name, [argument]).ShouldBe(expected);

    [Fact]
    public void AggregateResult_StringAgg_IsText() =>
        TotalFunctions
            .AggregateResult("string_agg", [BuiltInTypes.Text, BuiltInTypes.Text])
            .ShouldBe(BuiltInTypes.Text);

    // The catalog has no max of a varchar, which the plan prints cast to text, and array_agg is polymorphic.
    [Theory]
    [InlineData("max", BuiltInTypes.Varchar)]
    [InlineData("array_agg", BuiltInTypes.Int4)]
    [InlineData("count", BuiltInTypes.Int4)]
    [InlineData("sum", TotalFunctions.UnknownType)]
    public void AggregateResult_SignatureNotInTheTable_IsNull(string name, long argument) =>
        TotalFunctions.AggregateResult(name, [argument]).ShouldBeNull();

    // An aggregate is no function, and a function no aggregate.
    [Fact]
    public void AggregateResult_Function_IsNull() =>
        TotalFunctions.AggregateResult("upper", [BuiltInTypes.Text]).ShouldBeNull();

    [Fact]
    public void FunctionResult_Aggregate_IsNull() =>
        TotalFunctions.FunctionResult("max", [BuiltInTypes.Int4]).ShouldBeNull();

    // A call is checked by its name and its number of arguments before they are proved.
    [Theory]
    [InlineData("upper", 1, true, false)]
    [InlineData("date_trunc", 2, true, false)]
    [InlineData("upper", 2, false, false)]
    [InlineData("max", 1, false, true)]
    [InlineData("string_agg", 2, false, true)]
    [InlineData("string_agg", 1, false, false)]
    [InlineData("array_agg", 1, false, false)]
    public void HasFunctionAndHasAggregate_NameAndCount_AreTheTables(
        string name,
        int arguments,
        bool function,
        bool aggregate
    )
    {
        TotalFunctions.HasFunction(name, arguments).ShouldBe(function);
        TotalFunctions.HasAggregate(name, arguments).ShouldBe(aggregate);
    }
}
