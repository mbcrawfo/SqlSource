using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Models;
using SqlSource.Generation.Types;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Models;

public sealed class ModelCollectorTests
{
    [Fact]
    public void Resolve_ModelsOfDifferentQueries_GivePlacesBesideThem()
    {
        var requests = ImmutableArray.Create(
            Request("UserFilter", ModelRole.Input, "app/Users.sql", 3, "App.Users"),
            Request("OrderRow", ModelRole.Output, "app/Orders.sql", 1, "App.Orders-1")
        );

        var set = ModelCollector.Resolve(ModelCollector.Group(requests), EquatableArray<TakenName>.Empty);

        // The models are in the ordinal order of their full names.
        set.Models.Select(model => model.Name).ShouldBe(["OrderRow", "UserFilter"]);
        set.Places.ShouldBe([
            new ModelPlace("app/Orders.sql", 1, ModelRole.Output, "App.Orders-1"),
            new ModelPlace("app/Users.sql", 3, ModelRole.Input, "App.Users"),
        ]);
    }

    [Fact]
    public void Resolve_QueryClaimedByTwoTypes_GivesThePlaceOfTheFirstRequest()
    {
        var requests = ImmutableArray.Create(
            Request("OrderRow", ModelRole.Output, "app/Orders.sql", 1, "App.Zeta"),
            Request("OrderRow", ModelRole.Output, "app/Orders.sql", 1, "App.Alpha")
        );

        var set = ModelCollector.Resolve(ModelCollector.Group(requests), EquatableArray<TakenName>.Empty);

        set.Places.ShouldBe([new ModelPlace("app/Orders.sql", 1, ModelRole.Output, "App.Alpha")]);
    }

    [Fact]
    public void Resolve_NoGroups_GivesNoPlaces() =>
        ModelCollector
            .Resolve(EquatableArray<ModelGroup>.Empty, EquatableArray<TakenName>.Empty)
            .Places.ShouldBeEmpty();

    private static ModelRequest Request(string name, ModelRole role, string path, int queryIndex, string claimingType)
    {
        var location = new LocationInfo(path, new TextSpan(0, 1), new LinePositionSpan(default, default));
        return new ModelRequest(
            "App",
            "App",
            name,
            role,
            ModelKind.Record,
            false,
            true,
            null,
            TestModels.Array(
                new ModelProperty(
                    "Id",
                    new PropertyType("int", true, true, TestModels.Array<string>(), "int4", null),
                    false,
                    string.Empty,
                    "column 'id'",
                    false
                )
            ),
            new ModelSource(
                path,
                path[(path.LastIndexOf('/') + 1)..],
                queryIndex,
                "Query" + queryIndex,
                claimingType,
                claimingType,
                location,
                null,
                null
            )
        );
    }
}
