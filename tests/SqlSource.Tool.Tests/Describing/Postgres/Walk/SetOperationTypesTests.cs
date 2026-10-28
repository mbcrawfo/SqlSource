using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// Whether a branch's value of one type reaches the set operation's column of another through a cast the server ships,
// which tests/DatabaseTests/Postgres/Describer/PlanOutputTests.cs pins in both directions for every pair of a family.
public sealed class SetOperationTypesTests
{
    [Theory]
    [InlineData(BuiltInTypes.Int4, BuiltInTypes.Int8)]
    [InlineData(BuiltInTypes.Int8, BuiltInTypes.Int4)]
    [InlineData(BuiltInTypes.Int2, BuiltInTypes.Numeric)]
    [InlineData(BuiltInTypes.Float4, BuiltInTypes.Float8)]
    [InlineData(BuiltInTypes.Float8, BuiltInTypes.Int2)]
    [InlineData(BuiltInTypes.Text, BuiltInTypes.Varchar)]
    [InlineData(BuiltInTypes.Bpchar, BuiltInTypes.Text)]
    [InlineData(BuiltInTypes.Varchar, BuiltInTypes.Bpchar)]
    [InlineData(BuiltInTypes.Date, BuiltInTypes.Timestamptz)]
    [InlineData(BuiltInTypes.Timestamp, BuiltInTypes.Date)]
    [InlineData(BuiltInTypes.Timestamptz, BuiltInTypes.Timestamp)]
    public void Carries_TwoTypesOfOneFamily_IsTrue(long from, long to) =>
        SetOperationTypes.Carries(from, to).ShouldBeTrue();

    // The same type needs no cast, whichever it is: a domain, an enum, a type that no family holds.
    [Theory]
    [InlineData(BuiltInTypes.Bool)]
    [InlineData(BuiltInTypes.Int4)]
    [InlineData(2950)]
    [InlineData(16999)]
    public void Carries_TypeToItself_IsTrue(long oid) => SetOperationTypes.Carries(oid, oid).ShouldBeTrue();

    [Theory]
    [InlineData(BuiltInTypes.Int4, BuiltInTypes.Text)]
    [InlineData(BuiltInTypes.Text, BuiltInTypes.Int4)]
    [InlineData(BuiltInTypes.Varchar, BuiltInTypes.Bool)]
    [InlineData(BuiltInTypes.Date, BuiltInTypes.Text)]
    [InlineData(BuiltInTypes.Numeric, BuiltInTypes.Timestamptz)]
    [InlineData(BuiltInTypes.Bool, BuiltInTypes.Int4)]
    [InlineData(BuiltInTypes.Int4, 16999)]
    [InlineData(16999, BuiltInTypes.Text)]
    [InlineData(16999, 17000)]
    public void Carries_TypesOfTwoFamiliesOrOfNone_IsFalse(long from, long to) =>
        SetOperationTypes.Carries(from, to).ShouldBeFalse();

    // The result reports the base of a domain, so a domain's column and its base are two OIDs that no cast joins.
    [Fact]
    public void Carries_DomainAndItsBase_IsFalse()
    {
        const long Domain = 16500;
        const long Text = BuiltInTypes.Text;

        SetOperationTypes.Carries(Domain, Text).ShouldBeFalse();
        SetOperationTypes.Carries(Text, Domain).ShouldBeFalse();
    }

    [Theory]
    [InlineData(BuiltInTypes.Int2)]
    [InlineData(BuiltInTypes.Int8)]
    [InlineData(BuiltInTypes.Float8)]
    [InlineData(BuiltInTypes.Varchar)]
    [InlineData(BuiltInTypes.Bpchar)]
    [InlineData(BuiltInTypes.Timestamptz)]
    public void InFamily_TypeOfAFamily_IsTrue(long oid) => SetOperationTypes.InFamily(oid).ShouldBeTrue();

    // A boolean, the value of an EXISTS or an IN, an array, a domain and a type outside every family are in none.
    [Theory]
    [InlineData(BuiltInTypes.Bool)]
    [InlineData(BuiltInTypes.Xid)]
    [InlineData(1007)]
    [InlineData(16500)]
    [InlineData(TotalFunctions.UnknownType)]
    public void InFamily_TypeOfNoFamily_IsFalse(long oid) => SetOperationTypes.InFamily(oid).ShouldBeFalse();
}
