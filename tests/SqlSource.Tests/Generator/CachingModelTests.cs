// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Models;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the models: which models an edit makes the generator write again.
public sealed class CachingModelTests
{
    private readonly CachingScene _scene = new();

    // The three files of the models tests: Orders and Payments are claimed by OrderQueries and Users by UserQueries,
    // so the models are App.Orders.GetOrderDto, App.Orders.GetPaymentDto and App.Users.GetUserDto, in that order.
    // Each file has its own model, with one column, of int4.
    [Fact]
    public void Run_SqlFileEditedChangingOneModel_EmitsOnlyThatModelAgain()
    {
        var compilation = ModelsCompilation();
        var ordersSidecar = TypedSidecar(_scene.Orders, "GetOrder", "int4");
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders, ordersSidecar);

        // The column of Orders is now text.
        var result = Run(
            driver.ReplaceAdditionalText(ordersSidecar, TypedSidecar(_scene.Orders, "GetOrder", "text")),
            compilation
        );

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Modified]);

        // The names are the same as before.  The lookup in the project still ran, since the driver gives every run a
        // new compilation, and it gave the same answer.
        AllReasons(result, TrackingNames.ModelNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.TakenModelNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("GetOrderDto-Model.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("GetOrderDto(string Value)");
    }

    // A -- column: marker is a comment, so the query's hash, and so its sidecar, stays current: only Users is parsed
    // again, and only its model is emitted again, with the column nullable.
    [Fact]
    public void Run_ColumnMarkerAdded_ParsesOnlyThatFileAndEmitsOnlyItsModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);

        var edited = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- name: GetUser\n-- column: value null\nSELECT 1;\n"
        );
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        result.Diagnostics.ShouldBeEmpty();
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Modified,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Cached,
                    [_scene.Payments.Path] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("GetUserDto-Model.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("GetUserDto(int? Value)");
    }

    // The model App.Shared.Row is the marker's in Orders and Users alike.  Orders comes first, so its request is the
    // model.  Whichever file is edited, the two now differ, which is SQLSRC257 at Users, and the model is the request
    // of Orders alone, so its summary no longer names Users.  The edit of Orders changes the model's property too;
    // the edit of Users changes the model's summary alone.  The model of Payments is no part of it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_SharedModelEdited_EmitsOnlyThatModelAgain(bool editsTheFileThatGivesIt)
    {
        var compilation = ModelsCompilation();
        var users = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- name: GetUser\n-- output-model: App.Shared.Row\nSELECT 1;\n"
        );
        var orders = new InMemoryAdditionalText(
            _scene.Orders.Path,
            "-- name: GetOrder\n-- output-model: App.Shared.Row\nSELECT 2;\n"
        );
        var usersSidecar = TypedSidecar(users, "GetUser", "int4");
        var ordersSidecar = TypedSidecar(orders, "GetOrder", "int4");
        var driver = _scene.FirstRunOfModels(compilation, users, orders, usersSidecar, ordersSidecar);
        var before = driver.GetRunResult().GeneratedTrees.Single(IsShared).ToString();
        before.ShouldContain("queries <c>GetOrder</c> from <c>Orders.sql</c> and <c>GetUser</c> from <c>Users.sql</c>");
        before.ShouldContain("Row(int Value)");

        var result = Run(
            editsTheFileThatGivesIt
                ? driver.ReplaceAdditionalText(ordersSidecar, TypedSidecar(orders, "GetOrder", "text"))
                : driver.ReplaceAdditionalText(usersSidecar, TypedSidecar(users, "GetUser", "text")),
            compilation
        );

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Shared.Row-Model.g.cs"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("SQLSRC257");
        diagnostic.Location.GetLineSpan().Path.ShouldBe(_scene.Users.Path);
        var after = result.GeneratedTrees.Single(IsShared).ToString();
        after.ShouldContain("A row of the query <c>GetOrder</c> from <c>Orders.sql</c>.");
        after.ShouldNotContain("GetUser");
        after.ShouldContain(editsTheFileThatGivesIt ? "Row(string Value)" : "Row(int Value)");
        static bool IsShared(SyntaxTree tree) => tree.FilePath.EndsWith("Row-Model.g.cs", StringComparison.Ordinal);
    }

    // The accepted cost of SelectMany (TD-0060): its nodes are matched by their place in the list, not by name, and
    // the list is sorted by the models' full names.  A model added sorted first takes the place of the one that was
    // first, and every model after it moves down a place, so each is emitted again.  This pins the cost so that it is
    // not made worse unseen.
    [Fact]
    public void Run_ModelAdded_EmitsTheModelsAfterItAgain()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);
        var archive = new InMemoryAdditionalText("/app/Orders/Archive.sql", "-- name: GetArchive\nSELECT 4;\n");

        var result = Run(
            driver.AddAdditionalTexts([archive, TypedSidecar(archive, "GetArchive", "int4")]),
            compilation
        );

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetArchiveDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
    }

    // The same cost for a model that goes: the models before it are left alone, and each one after it takes the place
    // of the one before it.
    [Fact]
    public void Run_ModelRemoved_EmitsTheModelsAfterItAgain()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);

        var result = Run(driver.RemoveAdditionalTexts([_scene.Payments, _scene.PaymentsSidecar]), compilation);

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        ModelsRemoved(result).ShouldBe(["App.Users.GetUserDto-Model.g.cs"]);
    }

    // The edit gives the pipeline a new compilation: the names the project has are looked up again and found the
    // same, so the models are not worked out again, and none is emitted.
    [Fact]
    public void Run_CSharpEdit_EmitsNoModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.TakenModelNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ModelOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        ModelReasons(result).Count.ShouldBe(3);
        OutputReasons(result).Distinct().ShouldBe([IncrementalStepRunReason.Cached]);
    }

    // No model reads the collection type, so the requests of each type come out equal and nothing after them runs.
    [Fact]
    public void Run_CollectionTypePropertyChanged_EmitsNoModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    OutputKind.Models,
                    null,
                    "postgres",
                    properties: new Dictionary<string, string?> { ["SqlSourceCollectionType"] = "List" }
                )
            ),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.TypeModels)
            .ShouldBe([IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.ModelGroups).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ModelOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        ModelReasons(result).Count.ShouldBe(3);
    }

    // An attribute argument that no model reads: the type is read again, and its requests come out equal.
    [Fact]
    public void Run_AttributeEditedWithoutChangingAModel_EmitsNoModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);

        var result = Run(
            driver,
            WithArgumentsOnUserQueries(compilation, "CollectionType = GeneratorCollectionType.List")
        );

        Reasons<TypeQueries>(result, TrackingNames.TypeQueries, type => type.HintName)["App.Users.UserQueries.g.cs"]
            .ShouldBe(IncrementalStepRunReason.Modified);
        Reasons<EquatableArray<ModelRequest>>(
                result,
                TrackingNames.TypeModels,
                requests => requests[0].Source.ClaimingType
            )
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries"] = IncrementalStepRunReason.Unchanged,
                    ["App.Orders.OrderQueries"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ModelGroups).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Cached]);
        ModelReasons(result).Count.ShouldBe(3);
        AllReasons(result, TrackingNames.ModelOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A project at Sql has no model, so there is no name to look up in the compilation.
    [Fact]
    public void Run_ProjectWithoutModels_LooksUpNoName()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);

        var result = _scene.FirstRun(OutputKind.Sql, compilation).GetRunResult();

        (
            (EquatableArray<string>)Outputs(result, TrackingNames.ModelNames).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
        (
            (EquatableArray<TakenName>)Outputs(result, TrackingNames.TakenModelNames).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
    }
}
