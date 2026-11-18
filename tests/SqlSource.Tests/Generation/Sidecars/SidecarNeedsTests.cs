using System.Linq;
using Shouldly;
using SqlSource.Generation.Sidecars;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Sidecars;

public sealed class SidecarNeedsTests
{
    private static readonly FileNeeds First = Needs(
        "app/Users.sql",
        new QueryNeed("GetUser", OutputKind.Models),
        new QueryNeed("CountUsers", OutputKind.Models)
    );

    private static readonly FileNeeds Second = Needs(
        "app/Users.sql",
        new QueryNeed("ListUsers", OutputKind.Methods),
        new QueryNeed("GetUser", OutputKind.Methods)
    );

    // The order the types come in does not change the value, so equal needs never check a file again.
    [Fact]
    public void Merge_NeedsOfTwoTypesInEitherOrder_AreEqual() =>
        SidecarNeeds.Merge([First, Second]).ShouldBe(SidecarNeeds.Merge([Second, First]));

    [Fact]
    public void Merge_NeedsOfTwoTypes_AreTheUnionByNameWithTheGreatestOutput() =>
        SidecarNeeds
            .Merge([First, Second])
            .ShouldHaveSingleItem()
            .Queries.Select(query => (query.Name, query.Output))
            .ShouldBe([
                ("CountUsers", OutputKind.Models),
                ("GetUser", OutputKind.Methods),
                ("ListUsers", OutputKind.Methods),
            ]);

    [Fact]
    public void Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames() =>
        SidecarNeeds
            .Merge([
                Needs("app/Users.sql", new QueryNeed("a", OutputKind.Models), new QueryNeed("B", OutputKind.Models)),
            ])
            .ShouldHaveSingleItem()
            .Queries.Select(query => query.Name)
            .ShouldBe(["B", "a"]);

    [Fact]
    public void Merge_Files_AreSortedByPathIgnoringCase() =>
        SidecarNeeds
            .Merge([
                Needs("app/B.sql", new QueryNeed("Q", OutputKind.Models)),
                Needs("app/a.sql", new QueryNeed("Q", OutputKind.Models)),
            ])
            .Select(file => file.NormalizedPath)
            .ShouldBe(["app/a.sql", "app/B.sql"]);

    private static FileNeeds Needs(string path, params QueryNeed[] queries) =>
        new(path, new EquatableArray<QueryNeed>([.. queries]));
}
