using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Steps 1 and 2 of the run: the sidecars that are there, and what each query needs of the run.
public sealed class RunDecisionsTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n\n"
        + "-- name: Plain\n-- output: sql\nSELECT 3;\n";

    private static readonly RunFilters OnlyBilling = new([], ["billing"]);

    private readonly TempFolder _folder = new();
    private readonly TestProject _project;
    private readonly List<ToolDiagnostic> _errors = [];

    public RunDecisionsTests()
    {
        _project = Plans.Postgres(_folder);
        _ = _project.AddSql("Users.sql", Users);
    }

    public void Dispose() => _folder.Dispose();

    // The states of the file's queries, in the file's order.
    private QueryState[] States(RunPlan plan, bool force = false) =>
        [.. RunDecisions.Decide(plan, force, _errors)[0].Queries.Select(query => query.State)];

    [Fact]
    public void Decide_NoSidecar_DescribesWhatNeedsAnEntry()
    {
        States(Plans.Of(_project)).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);

        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_CurrentEntries_AreSkippedAndKept()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files[0]);

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries.Select(query => query.State)
            .ShouldBe([QueryState.Skipped, QueryState.Skipped, QueryState.NotNeeded]);
        file.Queries[0].Entry.ShouldNotBeNull().Name.ShouldBe("GetUser");
        file.Queries[1].Entry.ShouldNotBeNull().Database.ShouldBe("billing");
        _ = file.OnDisk.ShouldNotBeNull().Usable.ShouldNotBeNull();
    }

    [Fact]
    public void Decide_CurrentEntriesUnderForce_AreDescribedAgain()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files[0]);

        States(plan, force: true).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
    }

    [Fact]
    public void Decide_EntryWithAnotherHash_IsDescribed()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(
            file,
            [TestSidecar.EntryFor(file.Queries[0]) with { Hash = "0000" }, TestSidecar.EntryFor(file.Queries[1])]
        );

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.Skipped, QueryState.NotNeeded]);
    }

    [Fact]
    public void Decide_EntryOfAnotherDatabase_IsDescribed()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(
            file,
            [
                TestSidecar.EntryFor(file.Queries[0]) with
                {
                    Database = "elsewhere",
                },
                TestSidecar.EntryFor(file.Queries[1]),
            ]
        );

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.Skipped, QueryState.NotNeeded]);
    }

    // The name of a database is compared ignoring case, as its connection variable is.
    [Fact]
    public void Decide_EntryWhoseDatabaseDiffersInCase_IsCurrent()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(
            file,
            [TestSidecar.EntryFor(file.Queries[0]), TestSidecar.EntryFor(file.Queries[1]) with { Database = "BILLING" }]
        );

        States(plan).ShouldBe([QueryState.Skipped, QueryState.Skipped, QueryState.NotNeeded]);
    }

    [Fact]
    public void Decide_SidecarOfAnotherToolVersion_IsAbsent()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(file, file.Queries.Take(2).Select(TestSidecar.EntryFor), toolVersion: "0.0.1");

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_SidecarOfALowerFormatVersion_IsAbsent()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(file, file.Queries.Take(2).Select(TestSidecar.EntryFor), formatVersion: 0);

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
        _errors.ShouldBeEmpty();
    }

    // The usual cause is a merge conflict, and "describe" is the fix.
    [Fact]
    public void Decide_SidecarThatCannotBeParsed_IsAbsent()
    {
        var plan = Plans.Of(_project);
        File.WriteAllText(SidecarFormat.PathFor(plan.Files[0].Path), "<<<<<<< HEAD\n{");

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries.Select(query => query.State)
            .ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
        file.OnDisk.ShouldNotBeNull().Text.ShouldBe("<<<<<<< HEAD\n{");
        _errors.ShouldBeEmpty();
    }

    // Review focus 1.
    [Fact]
    public void Decide_SidecarPathThatIsADirectory_IsAbsentAndNothingThrows()
    {
        var plan = Plans.Of(_project);
        _ = Directory.CreateDirectory(SidecarFormat.PathFor(plan.Files[0].Path));

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries[0].State.ShouldBe(QueryState.ToDescribe);
        file.OnDisk.ShouldNotBeNull().Text.ShouldBeNull();
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_SidecarOfAHigherFormatVersion_IsSqlsrc221AndItsSelectedQueriesFail()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        var path = TestSidecar.Write(file, file.Queries.Take(2).Select(TestSidecar.EntryFor), formatVersion: 2);

        States(plan, force: true).ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.NotNeeded]);

        _errors
            .ShouldHaveSingleItem()
            .ShouldBe(
                ToolDiagnostic
                    .ForFile(ToolDiagnostics.SidecarOfNewerTool, path, path, "2", "1")
                    .WithLines(new ContinuationLine("help", "update the SqlSource.Tool package"))
            );
    }

    [Fact]
    public void Decide_QueryWithAProblemOfThePlan_FailsEvenUnderForce()
    {
        _ = _project.AddSql("Tokens.sql", "-- name: Find\nSELECT 1 {{where}};\n");
        var plan = Plans.Of(_project);

        var tokens = RunDecisions
            .Decide(plan, force: true, _errors)
            .Single(file => file.File.Path.EndsWith("Tokens.sql", StringComparison.Ordinal));

        tokens.Queries.ShouldHaveSingleItem().State.ShouldBe(QueryState.Failed);
    }

    // The plan reported SQLSRC209 for the file.  Its queries fail, and its sidecar is not read.
    [Fact]
    public void Decide_FileThatIsNotReady_FailsItsQueriesAndReadsNoSidecar()
    {
        _project.Properties["SqlSourceDialect"] = "ansi";
        var plan = Plans.Of(_project);
        plan.Files[0].State.ShouldBe(PlannedFileState.NotDescribable);
        _ = TestSidecar.Write(plan.Files[0], [], formatVersion: 2);

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries.Select(query => query.State)
            .ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.NotNeeded]);
        file.OnDisk.ShouldBeNull();
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_QueryThatIsNotSelected_IsKeptWhenCurrentAndLeftOutOtherwise()
    {
        var all = Plans.Of(_project);
        _ = TestSidecar.Write(all.Files[0], [TestSidecar.EntryFor(all.Files[0].Queries[0])]);

        States(Plans.Of(OnlyBilling, _project))
            .ShouldBe([QueryState.Skipped, QueryState.ToDescribe, QueryState.NotNeeded]);

        File.Delete(SidecarFormat.PathFor(all.Files[0].Path));
        States(Plans.Of(OnlyBilling, _project))
            .ShouldBe([QueryState.LeftOut, QueryState.ToDescribe, QueryState.NotNeeded]);
    }

    // "--force" describes what is selected, and nothing else.
    [Fact]
    public void Decide_QueryThatIsNotSelectedUnderForce_IsStillKept()
    {
        var all = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(all.Files[0]);

        States(Plans.Of(OnlyBilling, _project), force: true)
            .ShouldBe([QueryState.Skipped, QueryState.ToDescribe, QueryState.NotNeeded]);
    }

    // A filter narrows what a run reads as well as what it changes.
    [Fact]
    public void Decide_FileWithNoSelectedQuery_IsNotRead()
    {
        _ = _project.AddSql("Other.sql", "-- name: Other\nSELECT 4;\n");
        var all = Plans.Of(_project);
        var other = all.Files.Single(file => file.Path.EndsWith("Other.sql", StringComparison.Ordinal));
        _ = TestSidecar.Write(other, [], formatVersion: 2);

        var file = RunDecisions
            .Decide(Plans.Of(OnlyBilling, _project), force: false, _errors)
            .Single(work => work.File.Path == other.Path);

        file.OnDisk.ShouldBeNull();
        file.Queries.ShouldHaveSingleItem().State.ShouldBe(QueryState.LeftOut);
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void SelectedDatabases_Filter_GivesTheDatabasesOfSelectedQueriesInThePlansOrder()
    {
        RunDecisions
            .SelectedDatabases(Plans.Of(_project))
            .Select(database => database.Name)
            .ShouldBe(["postgres", "billing"]);
        RunDecisions
            .SelectedDatabases(Plans.Of(OnlyBilling, _project))
            .Select(database => database.Name)
            .ShouldBe(["billing"]);
    }
}
