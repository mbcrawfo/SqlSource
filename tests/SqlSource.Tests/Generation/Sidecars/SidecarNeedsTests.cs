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
        new QueryNeed("ListUsers", OutputKind.CodeGen),
        new QueryNeed("GetUser", OutputKind.CodeGen)
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
                ("GetUser", OutputKind.CodeGen),
                ("ListUsers", OutputKind.CodeGen),
            ]);

    [Fact]
    public void Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames() =>
        SidecarNeeds
            .Merge([Second])
            .ShouldHaveSingleItem()
            .Queries.Select(query => query.Name)
            .ShouldBe(["GetUser", "ListUsers"]);

    [Fact]
    public void Merge_Files_AreSortedByPathIgnoringCase() =>
        SidecarNeeds
            .Merge([
                Needs("app/b.sql", new QueryNeed("Q", OutputKind.Models)),
                Needs("app/A.sql", new QueryNeed("Q", OutputKind.Models)),
            ])
            .Select(file => file.NormalizedPath)
            .ShouldBe(["app/A.sql", "app/b.sql"]);

    private static FileNeeds Needs(string path, params QueryNeed[] queries) =>
        new(path, new EquatableArray<QueryNeed>([.. queries]));
}
