// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Models;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
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

    // The new file has a node of its own, after those of the files the project listed before, so no other file's
    // node moves and no other model is emitted again.
    [Fact]
    public void Run_ModelAdded_EmitsOnlyThatModel()
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
                    ["App.Orders.GetArchiveDto-Model.g.cs"] = IncrementalStepRunReason.New,
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        ModelsRemoved(result).ShouldBeEmpty();
    }

    // The node of the file that went goes with its model, and the other files' nodes keep theirs.
    [Fact]
    public void Run_ModelRemoved_EmitsNoOtherModel()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);

        var result = Run(driver.RemoveAdditionalTexts([_scene.Payments, _scene.PaymentsSidecar]), compilation);

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        ModelsRemoved(result).ShouldBe(["App.Orders.GetPaymentDto-Model.g.cs"]);
    }

    // Orders is listed between Users and Payments.  Its node goes with its model, and the nodes of the files listed
    // after it keep theirs.
    [Fact]
    public void Run_MiddleFileRemoved_EmitsNoOtherModel()
    {
        var compilation = ModelsCompilation();
        var ordersSidecar = TypedSidecar(_scene.Orders, "GetOrder", "int4");
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders, ordersSidecar);

        var result = Run(driver.RemoveAdditionalTexts([_scene.Orders, ordersSidecar]), compilation);

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        ModelsRemoved(result).ShouldBe(["App.Orders.GetOrderDto-Model.g.cs"]);
    }

    // Orders becomes Receipts.sql, in the same folder and in its place in the list, and its sidecar becomes that of
    // Receipts.sql in its own place.  The file's node keeps its place, so its model alone is written, now naming
    // Receipts.sql.  Receipts.sql sorts after Payments.sql, so a node matched by its place among the sorted files
    // would move the model of Payments too.
    [Fact]
    public void Run_MiddleFileRenamed_EmitsOnlyItsModel()
    {
        var compilation = ModelsCompilation();
        var ordersSidecar = TypedSidecar(_scene.Orders, "GetOrder", "int4");
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders, ordersSidecar);
        var receipts = new InMemoryAdditionalText(
            "/app/Orders/Receipts.sql",
            _scene.Orders.GetText(TestContext.Current.CancellationToken)!.ToString()
        );

        var result = Run(
            driver
                .ReplaceAdditionalText(_scene.Orders, receipts)
                .ReplaceAdditionalText(ordersSidecar, TypedSidecar(receipts, "GetOrder", "int4")),
            compilation
        );

        result.Diagnostics.ShouldBeEmpty();
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
        ModelsRemoved(result).ShouldBeEmpty();
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("GetOrderDto-Model.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("A row of the query <c>GetOrder</c> from <c>Receipts.sql</c>.");
    }

    // A type that comes to claim Users.sql asks for a model of its own for GetUser, App.Admin.GetUserDto, in the slot
    // of the query's rows.  A slot holds its models in the order of the types that asked for them, and
    // App.Admin.AdminQueries sorts before App.Users.UserQueries, so the new model takes the first place of the slot
    // and moves the model of UserQueries to a second place.  The node of the first place now emits a model of
    // another name, that of the second place is new, and both are written.  The other files' models are not.
    [Fact]
    public void Run_TypeAddedThatSortsFirstInASlot_EmitsTheModelsOfTheTypesSortedAfterItAgain()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);
        var admin = CSharpSyntaxTree.ParseText(
            """
            using SqlSource;

            namespace App.Admin;

            [SqlSourceGenerate(Path = "Users.sql")]
            public partial class AdminQueries;
            """,
            GeneratorHarness.ParseOptions,
            "/app/Users/AdminQueries.cs",
            cancellationToken: TestContext.Current.CancellationToken
        );

        var result = Run(driver, compilation.AddSyntaxTrees(admin));

        result.Diagnostics.ShouldBeEmpty();
        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Admin.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
        ModelsRemoved(result).ShouldBeEmpty();
    }

    // A marker is a comment, so the sidecar stays current.  The query's row model keeps its slot, which now holds a
    // model of another name, so that model alone is emitted, and the file of the old name goes in its place.  The new
    // name sorts after App.Orders.GetPaymentDto, so a node matched by its place in the sorted list would move it too.
    [Fact]
    public void Run_ModelRenamedInPlace_EmitsOnlyThatModel()
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);
        var renamed = new InMemoryAdditionalText(
            _scene.Orders.Path,
            "-- name: GetOrder\n-- output-model: App.Orders.OrderRow\nSELECT 2;\n"
        );

        var result = Run(driver.ReplaceAdditionalText(_scene.Orders, renamed), compilation);

        result.Diagnostics.ShouldBeEmpty();
        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.OrderRow-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        ModelsRemoved(result).ShouldBeEmpty();
        result.GeneratedTrees.ShouldContain(tree =>
            tree.FilePath.EndsWith("OrderRow-Model.g.cs", StringComparison.Ordinal)
        );
        result.GeneratedTrees.ShouldNotContain(tree =>
            tree.FilePath.EndsWith("GetOrderDto-Model.g.cs", StringComparison.Ordinal)
        );
    }

    // The query of Orders gains its parameter in the sidecar, and with it its input model, whose name sorts before
    // the models of Payments and Users.  Its row model keeps the slot of its role, and the other files keep their
    // nodes, so none of them is emitted again.
    [Fact]
    public void Run_QueryGainsParameters_EmitsOnlyItsInputModel()
    {
        var compilation = ModelsCompilation();
        var orders = new InMemoryAdditionalText(_scene.Orders.Path, "-- name: GetOrder\nSELECT 2 WHERE 1 = @id;\n");
        var ordersSidecar = TypedSidecar(orders, "GetOrder", "int4");
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, orders, ordersSidecar);

        var result = Run(
            driver.ReplaceAdditionalText(
                ordersSidecar,
                TypedSidecar(
                    orders,
                    "GetOrder",
                    "int4",
                    [TestSidecars.Parameter(0, "id", TestSidecars.Postgres("int4", internalName: "int4"))]
                )
            ),
            compilation
        );

        result.Diagnostics.ShouldBeEmpty();
        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Orders.GetOrderParams-Model.g.cs"] = IncrementalStepRunReason.New,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
    }

    // App.Shared.Row is emitted from the node of Orders, whose request is first.  When Orders goes, the model moves to
    // the node of Users in the same run, and its file is added once, now naming GetUser alone.
    [Fact]
    public void Run_FileThatGivesASharedModelRemoved_EmitsItFromTheNextFile()
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

        var result = Run(driver.RemoveAdditionalTexts([orders, ordersSidecar]), compilation);

        result.Results.Single().Exception.ShouldBeNull();
        result.Diagnostics.ShouldBeEmpty();
        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Shared.Row-Model.g.cs"] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
        var shared = result
            .GeneratedTrees.Where(tree => tree.FilePath.EndsWith("Row-Model.g.cs", StringComparison.Ordinal))
            .ShouldHaveSingleItem()
            .ToString();
        shared.ShouldContain("A row of the query <c>GetUser</c> from <c>Users.sql</c>.");
        shared.ShouldNotContain("GetOrder");
    }

    // A path listed twice has two nodes, and a model emitted from both would add its file twice, which throws and
    // drops all of the generator's output.  Its model is emitted once, from the collected output, and moves to its
    // file's node when one listing goes.  The first listing is the one removed, since the collected files keep its
    // parse.
    [Fact]
    public void Run_FileListedTwice_EmitsItsModelOnce()
    {
        var compilation = ModelsCompilation();
        var ordersAgain = new InMemoryAdditionalText(
            _scene.Orders.Path,
            _scene.Orders.GetText(TestContext.Current.CancellationToken)!.ToString()
        );
        var driver = GeneratorHarness
            .CreateDriver(
                [
                    _scene.Users,
                    _scene.Orders,
                    ordersAgain,
                    _scene.Payments,
                    TypedSidecar(_scene.Users, "GetUser", "int4"),
                    TypedSidecar(_scene.Orders, "GetOrder", "int4"),
                    _scene.PaymentsSidecar,
                ],
                OptionsWithTypeOverrides(null)
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        var first = driver.GetRunResult();

        first.Results.Single().Exception.ShouldBeNull();
        first.Diagnostics.ShouldBeEmpty();
        first.GeneratedTrees.Count(IsOrderModel).ShouldBe(1);

        var result = Run(driver.RemoveAdditionalTexts([_scene.Orders]), compilation);

        result.Results.Single().Exception.ShouldBeNull();
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedTrees.Count(IsOrderModel).ShouldBe(1);
        static bool IsOrderModel(SyntaxTree tree) =>
            tree.FilePath.EndsWith("GetOrderDto-Model.g.cs", StringComparison.Ordinal);
    }

    // A path that the project comes to list a second time, under the same spelling or one that differs by case alone,
    // moves its model from its file's node to the collected output in one run, and the model's file is added once.
    // The second spelling is a collision, SQLSRC013, as it is for any claimed file.
    [Theory]
    [InlineData("/app/Orders/Orders.sql")]
    [InlineData("/app/Orders/orders.sql")]
    public void Run_FileListedAgain_EmitsItsModelOnce(string spelling)
    {
        var compilation = ModelsCompilation();
        var driver = _scene.FirstRunOfModels(compilation, _scene.Users, _scene.Orders);
        var ordersAgain = new InMemoryAdditionalText(
            spelling,
            _scene.Orders.GetText(TestContext.Current.CancellationToken)!.ToString()
        );

        var result = Run(driver.AddAdditionalTexts([ordersAgain]), compilation);

        result.Results.Single().Exception.ShouldBeNull();
        result
            .GeneratedTrees.Count(tree => tree.FilePath.EndsWith("GetOrderDto-Model.g.cs", StringComparison.Ordinal))
            .ShouldBe(1);
        if (spelling == _scene.Orders.Path)
        {
            result.Diagnostics.ShouldBeEmpty();
        }
        else
        {
            result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC013");
        }
    }

    // Each path listed twice, under one spelling or two, is typed and its models read once, from the collected files,
    // so that its errors are reported once.  Without Npgsql, Users' column of date has no C# type, SQLSRC249, an error
    // of its types.  Orders' column gives a property name that is not an identifier, SQLSRC254, an error of its
    // models.  A second spelling is a collision, SQLSRC013, as it is for any claimed file.  When the first listings go,
    // the second ones are typed and read from their own nodes, and each error is still reported once.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_FileListedTwice_ReportsItsTypeAndNameErrorsOnce(bool byCase)
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = GeneratorHarness
            .CreateDriver(
                [
                    _scene.Users,
                    Again(_scene.Users, byCase),
                    _scene.Orders,
                    Again(_scene.Orders, byCase),
                    TypedSidecar(_scene.Users, "GetUser", "date"),
                    TypedSidecar(_scene.Orders, "GetOrder", "int4", columnName: "api-url"),
                ],
                OptionsWithTypeOverrides(null)
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        var first = driver.GetRunResult();

        first.Results.Single().Exception.ShouldBeNull();
        first
            .Diagnostics.Select(diagnostic => diagnostic.Id)
            .ShouldBe(
                byCase ? ["SQLSRC013", "SQLSRC013", "SQLSRC249", "SQLSRC254"] : ["SQLSRC249", "SQLSRC254"],
                ignoreOrder: true
            );

        var result = Run(driver.RemoveAdditionalTexts([_scene.Users, _scene.Orders]), compilation);

        result.Results.Single().Exception.ShouldBeNull();
        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC249", "SQLSRC254"], ignoreOrder: true);

        // The file again at its own path, or at that path with its extension in capitals.
        static InMemoryAdditionalText Again(InMemoryAdditionalText file, bool byCase) =>
            new(
                byCase ? file.Path.Replace(".sql", ".SQL", StringComparison.Ordinal) : file.Path,
                file.GetText(TestContext.Current.CancellationToken)!.ToString()
            );
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
