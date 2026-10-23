using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// The closed table of the names of pg_catalog whose every overload is never NULL under its condition.
// tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs holds every overload to the catalog of 14 and 18.
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
}
