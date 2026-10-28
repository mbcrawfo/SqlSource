using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// The closed table of the names of pg_catalog whose every overload is never NULL under its condition.
// tests/DatabaseTests/Postgres/Describer/PlanOutputTests.cs holds every overload to the catalog of 14 and 18.
public sealed class CatalogOverloadsTests
{
    [Fact]
    public void RangeTypeOf_Constructor_IsItsRangeType() =>
        CatalogOverloads.RangeTypeOf("tstzrange").ShouldBe(BuiltInTypes.Tstzrange);

    [Fact]
    public void RangeTypeOf_AnotherFunction_IsNull() => CatalogOverloads.RangeTypeOf("upper").ShouldBeNull();

    [Fact]
    public void IsRange_RangeType_IsTrue() => CatalogOverloads.IsRange(BuiltInTypes.Int4range).ShouldBeTrue();

    [Fact]
    public void IsRange_ItsBoundType_IsFalse() => CatalogOverloads.IsRange(BuiltInTypes.Int4).ShouldBeFalse();

    [Fact]
    public void Overloads_Names_AreTheFunctionNames() =>
        CatalogOverloads
            .Overloads.Select(static row => row.Name)
            .Distinct()
            .ShouldBe(CatalogOverloads.FunctionNames, ignoreOrder: true);

    [Fact]
    public void Overloads_Count_IsEveryOverloadOfTheNames() => CatalogOverloads.Overloads.Count().ShouldBe(20);

    // Every node's entries are read, a subplan's, an InitPlan's, a subquery's and a child's that the root does not
    // print included, and each name is asked once, in ordinal order.
    [Fact]
    public void RequestOf_NamesAnywhereInTheTree_AsksEachOnceInOrder()
    {
        var withSubPlan = Scan("a", "a.id", "count(a.id)") with
        {
            Plans = [SubPlan("SubPlan 1", Scan("b", "(b.during * tstzrange($1, $2))", "array_agg(b.id)"))],
        };
        var subquery = Over("Subquery Scan", Scan("d", "json_build_object('k', d.id)"), "Subquery") with
        {
            Alias = "s",
            Output = ["s.k"],
        };
        var join = Join("Inner", withSubPlan, subquery) with
        {
            Output = ["a.id", "jsonb_build_object('k', a.id)", "s.k"],
        };
        var initPlan = Scan("c", "int4range(c.id, 2)") with
        {
            ParentRelationship = "InitPlan",
            SubplanName = "InitPlan 1",
        };
        var plan = Over("Aggregate", join) with { Output = ["count(a.id)", "count(*)"] };
        plan = plan with { Plans = [.. plan.Plans, initPlan] };

        var request = CatalogOverloads.RequestOf(plan).ShouldNotBeNull();

        request.Functions.ShouldBe([
            "array_agg",
            "count",
            "int4range",
            "json_build_object",
            "jsonb_build_object",
            "tstzrange",
        ]);
        request.Operators.ShouldBe(["*"]);
    }

    // count(*) is read by its signature, and a plan of nothing else asks nothing.
    [Fact]
    public void RequestOf_PlanOfCountStarAlone_IsNull() =>
        CatalogOverloads
            .RequestOf(Over("Aggregate", Scan("a", "a.id")) with { Output = ["count(*)", "(a.id + 1)"] })
            .ShouldBeNull();
}
