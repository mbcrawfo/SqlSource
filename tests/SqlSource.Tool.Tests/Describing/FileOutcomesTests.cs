using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Step 4 of the run: what happens to each file's sidecar, and what the sidecar is to hold.  Nothing is written here.
public sealed class FileOutcomesTests : IDisposable
{
    private static readonly RunFilters OnlyBilling = new([], ["billing"]);

    private readonly TempFolder _folder = new();
    private readonly TestProject _project;

    public FileOutcomesTests()
    {
        _project = Plans.Postgres(_folder);
        _ = _project.AddSql("Users.sql", SqlTexts.UsersAndPlain);
    }

    public void Dispose() => _folder.Dispose();

    // Decides for the plan, and describes what the run would: each such query gets an entry of a newer server.
    private static ImmutableArray<FileWork> Described(RunPlan plan, bool force = false, params string[] failing)
    {
        var files = RunDecisions.Decide(plan, force ? RunMode.Force : RunMode.Describe, []);
        foreach (var query in files.SelectMany(file => file.Queries).Where(q => q.State == QueryState.ToDescribe))
        {
            if (failing.Contains(query.Planned.Query.Name))
            {
                query.State = QueryState.Failed;
                continue;
            }

            query.State = QueryState.Described;
            query.Entry = SidecarFiles.EntryFor(query.Planned) with { ServerVersion = "17.0" };
        }

        return files;
    }

    private static FileOutcome Outcome(ImmutableArray<FileWork> files, bool mayDelete = true) =>
        FileOutcomes.Decide(files, mayDelete, SidecarOnDisk.Read)[0];

    [Fact]
    public void Decide_EveryQueryDescribed_WritesATargetOfTheToolsVersionsInTheFilesOrder()
    {
        var plan = Plans.Of(_project);

        var outcome = Outcome(Described(plan));

        outcome.Action.ShouldBe(FileAction.Write);
        outcome.File.ShouldBe(plan.Files[0]);
        outcome.SidecarPath.ShouldBe(SidecarFormat.PathFor(plan.Files[0].Path));
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
        _ = SidecarFiles.WriteCurrent(plan.Files[0]);

        Outcome(Described(plan)).Action.ShouldBe(FileAction.Unchanged);
    }

    // What git or an editor did to the file's bytes is no change.
    [Fact]
    public void Decide_SameContentWithAByteOrderMarkAndOtherLineEndings_IsUnchanged()
    {
        var plan = Plans.Of(_project);
        var path = SidecarFiles.WriteCurrent(plan.Files[0]);
        var text = File.ReadAllText(path).Replace("\n", "\r\n", StringComparison.Ordinal);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Outcome(Described(plan)).Action.ShouldBe(FileAction.Unchanged);
    }

    [Fact]
    public void Decide_OneQueryDescribedAndOneKept_WritesBoth()
    {
        var plan = Plans.Of(_project);
        _ = SidecarFiles.Write(plan.Files[0], [SidecarFiles.EntryFor(plan.Files[0].Queries[1])]);

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
        var gone = SidecarFiles.EntryFor(file.Queries[0]) with { Name = "Gone" };
        _ = SidecarFiles.Write(file, [.. file.Queries.Take(2).Select(SidecarFiles.EntryFor), gone]);

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
        var outcome = Outcome(Described(Plans.Of(OnlyBilling, _project)), mayDelete: false);

        outcome.Action.ShouldBe(FileAction.HeldBack);
        outcome.HeldBackBy.ToArray().ShouldBe([new HeldBackQuery("GetUser", Failed: false)]);
        outcome.HasDescribed.ShouldBeTrue();
    }

    [Fact]
    public void Decide_QueryThatIsNotInTheRunAndHasACurrentEntry_IsWrittenBesideTheNewOne()
    {
        var all = Plans.Of(_project);
        _ = SidecarFiles.Write(all.Files[0], [SidecarFiles.EntryFor(all.Files[0].Queries[0])]);

        var outcome = Outcome(Described(Plans.Of(OnlyBilling, _project)), mayDelete: false);

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
        File.WriteAllText(SidecarFormat.PathFor(plan.Files[0].Path), "anything");

        var outcome = Outcome(Described(plan), mayDelete: false);

        outcome.Action.ShouldBe(FileAction.None);
        outcome.Target.ShouldBeNull();
    }

    // Which run may delete is DescribeRun's to say: one with no filter that reported nothing.  FilterRunTests holds
    // that.
    [Theory]
    [InlineData(true, true, nameof(FileAction.Delete))]
    [InlineData(false, true, nameof(FileAction.None))]
    [InlineData(true, false, nameof(FileAction.None))]
    public void Decide_FileThatNeedsNoEntry_LosesItsSidecarOnlyInARunThatMayDeleteAndWhenThereIsOne(
        bool mayDelete,
        bool sidecarExists,
        string action
    )
    {
        var project = Plans.Postgres(_folder, "Other");
        var sql = project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        if (sidecarExists)
        {
            File.WriteAllText(SidecarFormat.PathFor(sql), "anything");
        }

        Outcome(Described(Plans.Of(project)), mayDelete).Action.ShouldBe(Enum.Parse<FileAction>(action));
    }

    // A sidecar of a higher format version is a newer tool's file, which SQLSRC221 says is left alone.
    [Fact]
    public void Decide_FileThatNeedsNoEntryWithASidecarOfAHigherFormat_IsNotDeleted()
    {
        var project = Plans.Postgres(_folder, "Other");
        var sql = project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        File.WriteAllText(SidecarFormat.PathFor(sql), SidecarFiles.Newer);

        var outcome = Outcome(Described(Plans.Of(project)));

        outcome.Action.ShouldBe(FileAction.None);
        outcome.Target.ShouldBeNull();
    }

    // What cannot be opened cannot be told from a newer tool's file: it is left, and the outcome holds the error.
    [Fact]
    public void Decide_FileThatNeedsNoEntryWithASidecarThatCannotBeOpened_IsUnreadableWithTheSystemsReason()
    {
        var project = Plans.Postgres(_folder, "Other");
        var sidecar = SidecarFormat.PathFor(project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n"));
        _ = Directory.CreateDirectory(sidecar);
        var reason = SidecarOnDisk.Read(project.PathOf("Plain.sql")).OpenFailure.ShouldNotBeNull();

        var outcome = Outcome(Described(Plans.Of(project)));

        outcome.Action.ShouldBe(FileAction.Unreadable);
        var failure = outcome.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC218");
        failure.Path.ShouldBe(sidecar);
        failure.Arguments.AsEnumerable().ShouldBe([sidecar, "read", reason]);
    }

    [Fact]
    public void Decide_FileThatNeedsNoEntryWithASidecarThatCannotBeOpened_IsNotLookedAtInARunThatMayNotDelete()
    {
        var project = Plans.Postgres(_folder, "Other");
        _ = Directory.CreateDirectory(
            SidecarFormat.PathFor(project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n"))
        );

        var outcome = Outcome(Described(Plans.Of(project)), mayDelete: false);

        outcome.Action.ShouldBe(FileAction.None);
        outcome.Failure.ShouldBeNull();
    }

    [Fact]
    public void Decide_FileThatIsNotReady_IsNotTouched()
    {
        _project.Properties["SqlSourceDialect"] = "ansi";
        var plan = Plans.Of(_project);
        File.WriteAllText(SidecarFormat.PathFor(plan.Files[0].Path), "anything");

        Outcome(Described(plan)).Action.ShouldBe(FileAction.None);
    }
}
