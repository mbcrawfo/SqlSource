using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Types;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The types that carry a modifier are handled by three switches, each in its own way: a type added to the set and
// left out of one of them fails here.
public sealed class BuiltInTypesTests
{
    public static TheoryData<string> WithModifier => [.. BuiltInTypes.WithModifier];

    [Theory]
    [MemberData(nameof(WithModifier))]
    public void WithModifier_EachMember_HasFacetsInEverySwitch(string typname)
    {
        var row = new PostgresTypeRow(1, typname, "pg_catalog", "b", "X", 0, 0, null, null, typname, null, -1);

        TypeModifiers.Decode(typname, 10).ShouldNotBe(PostgresFacets.None);
        ColumnFacets.Of(typname, new PostgresColumnRow(0, "c", 1, 0, 0, 5, 5, 2)).ShouldNotBe(PostgresFacets.None);
        PostgresTypeNames.InCatalog(row, new PostgresFacets(5, 5, 2)).ShouldNotBe(typname);
    }
}
