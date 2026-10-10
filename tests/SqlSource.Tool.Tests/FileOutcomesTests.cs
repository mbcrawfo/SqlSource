using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// Step 4 of the run: what happens to each file's sidecar, and what the sidecar is to hold.  Nothing is written here.
public sealed class FileOutcomesTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n\n"
        + "-- name: Plain\n-- output: sql\nSELECT 3;\n";

    private static readonly RunFilters OnlyBilling = new([], ["billing"]);

    private readonly TempFolder _folder = new();
    private readonly TestProject _project;

    public FileOutcomesTests()
    {
        _project = Plans.Postgres(_folder);
        _ = _project.AddSql("Users.sql", Users);
    }

    public void Dispose() => _folder.Dispose();

    // Decides for the plan, and describes what the run would: each such query gets an entry of a newer server.
    private static ImmutableArray<FileWork> Described(RunPlan plan, bool force = false, params string[] failing)
    {
        var files = RunDecisions.Decide(plan, force, []);
        foreach (var query in files.SelectMany(file => file.Queries).Where(q => q.State == QueryState.ToDescribe))
        {
            if (failing.Contains(query.Planned.Query.Name))
            {
                query.State = QueryState.Failed;
                continue;
            }

            query.State = QueryState.Described;
            query.Entry = TestSidecar.EntryFor(query.Planned) with { ServerVersion = "17.0" };
        }

        return files;
    }

    private static FileOutcome Outcome(ImmutableArray<FileWork> files, bool runHasFilter = false) =>
        FileOutcomes.Decide(files, runHasFilter, File.Exists)[0];

    [Fact]
    public void Decide_EveryQueryDescribed_WritesATargetOfTheToolsVersionsInTheFilesOrder()
    {
        var plan = Plans.Of(_project);

        var outcome = Outcome(Described(plan));

        outcome.Action.ShouldBe(FileAction.Write);
        outcome.File.ShouldBe(plan.Files[0]);
        outcome.SidecarPath.ShouldBe(plan.Files[0].Path + ".json");
        var target = outcome.Target.ShouldNotBeNull();
        target.FormatVersion.ShouldBe(SidecarFormat.Version);
        target.ToolVersion.ShouldBe(PackageVersion.Prefix);
        target
            .Queries.Select(entry => (entry.Name, entry.ServerVersion))
            .ShouldBe([("GetUser", "17.0"), ("GetInvoice", "17.0")]);
        outcome.Text.ShouldBe(SidecarWriter.Write(target));
    }

    [Fact]
    public void Decide_EveryEntryCurrent_IsUnchanged()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files[0]);

        Outcome(Described(plan)).Action.ShouldBe(FileAction.Unchanged);
    }

    // Review focus 4: what git or an editor did to the file's bytes is no change.
    [Fact]
    public void Decide_SameContentWithAByteOrderMarkAndOtherLineEndings_IsUnchanged()
    {
        var plan = Plans.Of(_project);
        var path = TestSidecar.WriteCurrent(plan.Files[0]);
        var text = File.ReadAllText(path).Replace("\n", "\r\n", StringComparison.Ordinal);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Outcome(Described(plan)).Action.ShouldBe(FileAction.Unchanged);
    }

    [Fact]
    public void Decide_OneQueryDescribedAndOneKept_WritesBoth()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.Write(plan.Files[0], [TestSidecar.EntryFor(plan.Files[0].Queries[1])]);

        var outcome = Outcome(Described(plan));

        outcome.Action.ShouldBe(FileAction.Write);
        outcome
            .Target.ShouldNotBeNull()
            .Queries.Select(entry => (entry.Name, entry.ServerVersion))
            .ShouldBe([("GetUser", "17.0"), ("GetInvoice", "16.4")]);
    }

    [Fact]
    public void Decide_EntryOfAQueryThatIsGone_IsDroppedAndTheFileIsWritten()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        var gone = TestSidecar.EntryFor(file.Queries[0]) with { Name = "Gone" };
        _ = TestSidecar.Write(file, [.. file.Queries.Take(2).Select(TestSidecar.EntryFor), gone]);

        var outcome = Outcome(Described(plan));

        outcome.Action.ShouldBe(FileAction.Write);
        outcome.Target.ShouldNotBeNull().Queries.Select(entry => entry.Name).ShouldBe(["GetUser", "GetInvoice"]);
    }

    [Fact]
    public void Decide_QueryThatFailed_HoldsTheFileBackAndSaysThatOneWasDescribed()
    {
        var outcome = Outcome(Described(Plans.Of(_project), failing: "GetUser"));

        outcome.Action.ShouldBe(FileAction.HeldBack);
        outcome.Target.ShouldBeNull();
        outcome.Text.ShouldBeNull();
        outcome.HeldBackBy.ToArray().ShouldBe([new HeldBackQuery("GetUser", Failed: true)]);
        outcome.HasDescribed.ShouldBeTrue();
    }

    [Fact]
    public void Decide_EveryQueryFailed_HoldsTheFileBackAndSaysThatNoneWasDescribed()
    {
        var outcome = Outcome(Described(Plans.Of(_project), false, "GetUser", "GetInvoice"));

        outcome.Action.ShouldBe(FileAction.HeldBack);
        outcome.HeldBackBy.Count.ShouldBe(2);
        outcome.HasDescribed.ShouldBeFalse();
    }

    [Fact]
    public void Decide_QueryThatIsNotInTheRunAndHasNoCurrentEntry_HoldsTheFileBack()
    {
        var outcome = Outcome(Described(Plans.Of(OnlyBilling, _project)), runHasFilter: true);

        outcome.Action.ShouldBe(FileAction.HeldBack);
        outcome.HeldBackBy.ToArray().ShouldBe([new HeldBackQuery("GetUser", Failed: false)]);
        outcome.HasDescribed.ShouldBeTrue();
    }

    [Fact]
    public void Decide_QueryThatIsNotInTheRunAndHasACurrentEntry_IsWrittenBesideTheNewOne()
    {
        var all = Plans.Of(_project);
        _ = TestSidecar.Write(all.Files[0], [TestSidecar.EntryFor(all.Files[0].Queries[0])]);

        var outcome = Outcome(Described(Plans.Of(OnlyBilling, _project)), runHasFilter: true);

        outcome.Action.ShouldBe(FileAction.Write);
        outcome
            .Target.ShouldNotBeNull()
            .Queries.Select(entry => (entry.Name, entry.ServerVersion))
            .ShouldBe([("GetUser", "16.4"), ("GetInvoice", "17.0")]);
    }

    [Fact]
    public void Decide_FileWithNoSelectedQuery_IsNotTouched()
    {
        var project = Plans.Postgres(_folder, "Other");
        _ = project.AddSql("Q.sql", "-- name: A\nSELECT 1;\n");
        var plan = Plans.Of(OnlyBilling, project);
        File.WriteAllText(plan.Files[0].Path + ".json", "anything");

        var outcome = Outcome(Described(plan), runHasFilter: true);

        outcome.Action.ShouldBe(FileAction.None);
        outcome.Target.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, true, nameof(FileAction.Delete))]
    [InlineData(true, true, nameof(FileAction.None))]
    [InlineData(false, false, nameof(FileAction.None))]
    public void Decide_FileThatNeedsNoEntry_LosesItsSidecarOnlyInARunWithNoFilter(
        bool runHasFilter,
        bool sidecarExists,
        string action
    )
    {
        var project = Plans.Postgres(_folder, "Other");
        var sql = project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        if (sidecarExists)
        {
            File.WriteAllText(sql + ".json", "anything");
        }

        Outcome(Described(Plans.Of(project)), runHasFilter).Action.ShouldBe(Enum.Parse<FileAction>(action));
    }

    [Fact]
    public void Decide_FileThatIsNotReady_IsNotTouched()
    {
        _project.Properties["SqlSourceDialect"] = "ansi";
        var plan = Plans.Of(_project);
        File.WriteAllText(plan.Files[0].Path + ".json", "anything");

        Outcome(Described(plan)).Action.ShouldBe(FileAction.None);
    }
}
