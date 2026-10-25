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
    public void Resolve_TwoTypesSharingOneModel_AssignsItToBoth()
    {
        var requests = ImmutableArray.Create(
            Request("GetUserDto", ModelRole.Output, "app/Users.sql", 0, "App.B"),
            Request("GetUserDto", ModelRole.Output, "app/Users.sql", 0, "App.A")
        );

        var set = Resolve(requests);

        set.Models.Count.ShouldBe(1);
        set.Assignments.ShouldBe([
            new ModelAssignment(
                "App.A",
                "app/Users.sql",
                0,
                ModelRole.Output,
                "global::App.GetUserDto",
                ModelKind.Record
            ),
            new ModelAssignment(
                "App.B",
                "app/Users.sql",
                0,
                ModelRole.Output,
                "global::App.GetUserDto",
                ModelKind.Record
            ),
        ]);
    }

    [Fact]
    public void Resolve_ModelInTheGlobalNamespace_IsAssignedWithGlobalAlone()
    {
        var requests = ImmutableArray.Create(
            Request("GetUserDto", ModelRole.Input, "app/Users.sql", 0, "App.A", @namespace: "", kind: ModelKind.Class)
        );

        var assignment = Resolve(requests).Assignments.ShouldHaveSingleItem();

        assignment.TypeText.ShouldBe("global::GetUserDto");
        assignment.Kind.ShouldBe(ModelKind.Class);
    }

    [Fact]
    public void Resolve_LaterRequestThatDiffers_IsNotAssigned()
    {
        var requests = ImmutableArray.Create(
            Request("GetUserDto", ModelRole.Output, "app/Orders.sql", 0, "App.A"),
            Request("GetUserDto", ModelRole.Output, "app/Users.sql", 0, "App.A", propertyName: "Name")
        );

        var set = Resolve(requests);

        set.Diagnostics.Count.ShouldBe(1);
        set.Assignments.Select(assignment => assignment.NormalizedPath).ShouldBe(["app/Orders.sql"]);
    }

    [Fact]
    public void Resolve_ModelWithAProblem_AssignsNothing()
    {
        // CS0542: the property has the model's name.
        var requests = ImmutableArray.Create(
            Request("GetUserDto", ModelRole.Output, "app/Users.sql", 0, "App.A", propertyName: "GetUserDto"),
            Request("GetUserDto", ModelRole.Output, "app/Users.sql", 0, "App.B", propertyName: "GetUserDto")
        );

        var set = Resolve(requests);

        set.Diagnostics.ShouldNotBeEmpty();
        set.Models.ShouldBeEmpty();
        set.Assignments.ShouldBeEmpty();
    }

    // The type ordinally, then the path ignoring case (so a.sql comes before Z.sql, which ordinal order would not
    // give), then the query's index, then the role.
    [Fact]
    public void Resolve_Requests_AssignmentsAreSortedByTypePathIndexAndRole()
    {
        var requests = ImmutableArray.Create(
            Request("M1", ModelRole.Output, "app/Z.sql", 0, "App.B"),
            Request("M2", ModelRole.Output, "app/a.sql", 1, "App.A"),
            Request("M3", ModelRole.Input, "app/a.sql", 0, "App.A"),
            Request("M4", ModelRole.Input, "app/a.sql", 1, "App.A"),
            Request("M5", ModelRole.Input, "app/Z.sql", 0, "App.A")
        );

        var set = Resolve(requests);

        set.Assignments.Select(assignment => assignment.TypeText)
            .ShouldBe(["global::App.M3", "global::App.M4", "global::App.M2", "global::App.M5", "global::App.M1"]);
    }

    [Fact]
    public void Resolve_NoGroups_GivesNoPlaces() =>
        ModelCollector
            .Resolve(EquatableArray<ModelGroup>.Empty, EquatableArray<TakenName>.Empty)
            .Places.ShouldBeEmpty();

    [Fact]
    public void Resolve_NoGroups_GivesNoAssignments() =>
        ModelCollector
            .Resolve(EquatableArray<ModelGroup>.Empty, EquatableArray<TakenName>.Empty)
            .Assignments.ShouldBeEmpty();

    private static ModelSet Resolve(ImmutableArray<ModelRequest> requests) =>
        ModelCollector.Resolve(ModelCollector.Group(requests), EquatableArray<TakenName>.Empty);

    private static ModelRequest Request(
        string name,
        ModelRole role,
        string path,
        int queryIndex,
        string claimingType,
        string @namespace = "App",
        ModelKind kind = ModelKind.Record,
        string propertyName = "Id"
    )
    {
        var location = new LocationInfo(path, new TextSpan(0, 1), new LinePositionSpan(default, default));
        return new ModelRequest(
            @namespace,
            @namespace,
            name,
            role,
            kind,
            false,
            true,
            null,
            TestModels.Array(
                new ModelProperty(
                    propertyName,
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
