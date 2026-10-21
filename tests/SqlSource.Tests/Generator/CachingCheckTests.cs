// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

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

    // TD-0019: the check of each file is a node of its own, but the nodes are matched by their place in the sorted
    // list of parsed files, not by path.  So a claimed file added to the list checks again every file sorted after
    // it, and none before it: Payments takes the place Users had, and Users is checked as a new node after it.
    [Fact]
    public void Run_ClaimedSqlFileAdded_ChecksTheFilesSortedAfterIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);
        var payments = new InMemoryAdditionalText("/app/Orders/Payments.sql", "-- name: GetPayment\nSELECT 3;\n");

        var result = Run(driver.AddAdditionalTexts([payments, CurrentSidecar(payments, "GetPayment")]), compilation);

        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Payments.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
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

    [Fact]
    public void Run_ModelSuffixPropertyChanged_ChecksNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

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
