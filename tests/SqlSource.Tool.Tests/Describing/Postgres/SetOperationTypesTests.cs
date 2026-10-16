using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Whether a branch's value of one type reaches the set operation's column of another through a cast the server ships,
// which tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs pins in both directions for every pair of a family.
public class SetOperationTypesTests
{
    [Theory]
    [InlineData(23, 20)]
    [InlineData(20, 23)]
    [InlineData(21, 1700)]
    [InlineData(700, 701)]
    [InlineData(701, 21)]
    [InlineData(25, 1043)]
    [InlineData(1042, 25)]
    [InlineData(1043, 1042)]
    [InlineData(1082, 1184)]
    [InlineData(1114, 1082)]
    [InlineData(1184, 1114)]
    public void Carries_TwoTypesOfOneFamily_IsTrue(long from, long to) =>
        SetOperationTypes.Carries(from, to).ShouldBeTrue();

    // The same type needs no cast, whichever it is: a domain, an enum, a type that no family holds.
    [Theory]
    [InlineData(16)]
    [InlineData(23)]
    [InlineData(2950)]
    [InlineData(16999)]
    public void Carries_TypeToItself_IsTrue(long oid) => SetOperationTypes.Carries(oid, oid).ShouldBeTrue();

    [Theory]
    [InlineData(23, 25)]
    [InlineData(25, 23)]
    [InlineData(1043, 16)]
    [InlineData(1082, 25)]
    [InlineData(1700, 1184)]
    [InlineData(16, 23)]
    [InlineData(23, 16999)]
    [InlineData(16999, 25)]
    [InlineData(16999, 17000)]
    public void Carries_TypesOfTwoFamiliesOrOfNone_IsFalse(long from, long to) =>
        SetOperationTypes.Carries(from, to).ShouldBeFalse();

    // The result reports the base of a domain, so a domain's column and its base are two OIDs that no cast joins.
    [Fact]
    public void Carries_DomainAndItsBase_IsFalse()
    {
        const long Domain = 16500;
        const long Text = 25;

        SetOperationTypes.Carries(Domain, Text).ShouldBeFalse();
        SetOperationTypes.Carries(Text, Domain).ShouldBeFalse();
    }
}
