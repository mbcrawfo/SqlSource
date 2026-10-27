// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the check of the sidecars: which files an edit makes the generator check again.
public sealed class CachingCheckTests
{
    private readonly CachingScene _scene = new();

    [Fact]
    public void Run_SqlFileEdited_ChecksOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        // The type's needs were found again and are the same, so only the edited file was checked, and its entry is
        // now stale.
        Reasons<EquatableArray<FileNeeds>>(result, TrackingNames.TypeNeeds, NeedsKey)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Unchanged,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.FileNeeds).ShouldBe([IncrementalStepRunReason.Cached]);
        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC244");
    }

    [Fact]
    public void Run_SidecarEdited_ChecksOnlyItsFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var stale = Sidecars.Of(ToSqlFile(_scene.Users), Sidecars.Entry("GetUser", "stale"));
        var edited = new InMemoryAdditionalText(stale.Path, stale.Text);
        var result = Run(driver.ReplaceAdditionalText(_scene.UsersSidecar, edited), compilation);

        // Every file gathered its input again, and only the one whose sidecar changed has another.
        Reasons<SidecarCheckInput>(result, TrackingNames.SidecarCheckInput, input => input.File.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeNeeds).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC244");
    }

    // The added file has a node of its own, from its file's node, which moves no other file's: Payments sorts between
    // Orders and Users, and Users keeps its node.
    [Fact]
    public void Run_ClaimedSqlFileAdded_ChecksOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var result = Run(
            driver.AddAdditionalTexts([_scene.Payments, CurrentSidecar(_scene.Payments, "GetPayment")]),
            compilation
        );

        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Payments.sql"] = IncrementalStepRunReason.New,
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
    }

    // Orders sorts first, and its node goes with it.  The others keep theirs, so neither is checked or typed again.
    [Fact]
    public void Run_ClaimedSqlFileRemoved_MovesNoOtherFilesCheck()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);
        driver = driver
            .AddAdditionalTexts([_scene.Payments, CurrentSidecar(_scene.Payments, "GetPayment")])
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        var result = Run(driver.RemoveAdditionalTexts([_scene.Orders, _scene.OrdersSidecar]), compilation);

        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Removed,
                    ["app/Orders/Payments.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason =>
                reason != IncrementalStepRunReason.Modified && reason != IncrementalStepRunReason.New
            );
        result.Diagnostics.ShouldBeEmpty();
    }

    // A path listed twice, under one spelling or two, has a node for each listing, and is checked once, from the
    // collected files: its stale entry is reported once.  The second spelling is a collision, SQLSRC013, as it is for
    // any claimed file.  When the first listing goes, the second is checked from its own node, once.
    [Theory]
    [InlineData("/app/Users/Users.sql")]
    [InlineData("/app/Users/users.sql")]
    public void Run_FileListedTwice_ChecksItOnce(string spelling)
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var usersAgain = new InMemoryAdditionalText(
            spelling,
            _scene.Users.GetText(TestContext.Current.CancellationToken)!.ToString()
        );
        var stale = Sidecars.Of(ToSqlFile(_scene.Users), Sidecars.Entry("GetUser", "stale"));
        var staleSidecar = new InMemoryAdditionalText(stale.Path, stale.Text);
        var driver = CreateDriver(
                ProjectFile(OutputKind.Models, "postgres"),
                [_scene.Users, usersAgain, _scene.Orders, _scene.OrdersSidecar, staleSidecar]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        var collides = spelling != _scene.Users.Path;

        var first = driver.GetRunResult();

        first.Results.Single().Exception.ShouldBeNull();
        first
            .Diagnostics.Select(diagnostic => diagnostic.Id)
            .ShouldBe(collides ? ["SQLSRC013", "SQLSRC244"] : ["SQLSRC244"], ignoreOrder: true);
        Checks(first).Keys.Count(path => path.StartsWith("app/Users/", StringComparison.Ordinal)).ShouldBe(1);

        var result = Run(driver.RemoveAdditionalTexts([_scene.Users]), compilation);

        result.Results.Single().Exception.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC244");
    }

    // The attribute changes the type, so its needs are found again, and they are the same.
    [Fact]
    public void Run_AttributeEditedWithoutChangingTheOutput_ChecksNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var result = Run(driver, WithArgumentsOnUserQueries(compilation, "ModelNamespace = \"App.Models\""));

        Reasons<TypeQueries>(result, TrackingNames.TypeQueries, type => type.HintName)["App.Users.UserQueries.g.cs"]
            .ShouldBe(IncrementalStepRunReason.Modified);
        Reasons<EquatableArray<FileNeeds>>(result, TrackingNames.TypeNeeds, NeedsKey)["app/Users/Users.sql"]
            .ShouldBe(IncrementalStepRunReason.Unchanged);
        AllReasons(result, TrackingNames.FileNeeds)
            .ShouldAllBe(reason =>
                reason == IncrementalStepRunReason.Unchanged || reason == IncrementalStepRunReason.Cached
            );
        Checks(result).Values.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A test of the MSBuild path: the output, the dialect and the suffix are the project's properties.
    [Fact]
    public void Run_ModelSuffixPropertyChanged_ChecksNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunOnMSBuild(
            compilation,
            new TestOptionsProvider(OutputKind.Models, null, "postgres"),
            _scene.Users,
            _scene.Orders,
            _scene.UsersSidecar,
            _scene.OrdersSidecar
        );

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    OutputKind.Models,
                    null,
                    "postgres",
                    properties: new Dictionary<string, string?> { ["SqlSourceOutputModelSuffix"] = "Row" }
                )
            ),
            compilation
        );

        // Every type's needs were found again, under the new property, and are the same.
        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.TypeNeeds).ShouldAllBe(reason => reason == IncrementalStepRunReason.Unchanged);
        AllReasons(result, TrackingNames.FileNeeds).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.SidecarCheckInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        Checks(result).Values.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);

        // The types read the property's type override alone, which did not change.
        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A project at Sql with no sidecar: no query needs an entry, so no check has anything to do and no hash is
    // computed.
    [Fact]
    public void Run_ProjectAtSql_ComputesNoHash()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);

        var result = _scene.FirstRun(OutputKind.Sql, compilation).GetRunResult();

        (
            (EquatableArray<FileNeeds>)Outputs(result, TrackingNames.FileNeeds).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
        Outputs(result, TrackingNames.SidecarCheck)
            .Select(output => (SidecarCheckResult)output.Value)
            .ShouldAllBe(check => check.Diagnostics.Count == 0 && check.UsableEntries.Count == 0);
    }
}
