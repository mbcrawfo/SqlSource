using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// The one place that changes a sidecar on the disk: a file is written whole or not at all.
public sealed class SidecarStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly TestClock _clock = new();
    private readonly PlannedFile _file;
    private readonly string _sidecar;

    public SidecarStoreTests()
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSql("Users.sql", SqlTexts.GetUser);
        _file = Plans.Of(project).Files[0];
        _sidecar = SidecarFormat.PathFor(_file.Path);
    }

    public void Dispose()
    {
        _error.Dispose();
        _folder.Dispose();
    }

    private FileOutcome Outcome(FileAction action, string? text = null, params HeldBackQuery[] heldBackBy) =>
        new(
            _file,
            _sidecar,
            action,
            Target: null,
            text,
            new EquatableArray<HeldBackQuery>([.. heldBackBy]),
            HasDescribed: false
        );

    // A run on a part of the project, as under a filter.
    private void Apply(params FileOutcome[] outcomes) => Apply(onTheWhole: false, outcomes);

    private void Apply(bool onTheWhole, params FileOutcome[] outcomes) =>
        SidecarStore.Apply(
            [.. outcomes],
            new Reporter(_error),
            _clock,
            onTheWhole,
            TestContext.Current.CancellationToken
        );

    // A file as a run leaves it when it is killed between the write and the move, last written so long ago.
    private string Leftover(TimeSpan age, string? name = null)
    {
        var path = Path.Combine(Path.GetDirectoryName(_sidecar)!, name ?? $"Users.sql.json.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "{");
        File.SetLastWriteTimeUtc(path, (_clock.Now - age).UtcDateTime);
        return path;
    }

    // The names of what is in the project's folder, its settings file among them, to see that nothing else was left
    // there.
    private string[] Left() =>
        [
            .. Directory
                .EnumerateFileSystemEntries(Path.GetDirectoryName(_sidecar)!)
                .Select(entry => Path.GetFileName(entry))
                .Order(StringComparer.Ordinal),
        ];

    [Fact]
    public void Apply_Write_MakesTheFileWithTheTextAndLeavesNoTemporaryFile()
    {
        Apply(Outcome(FileAction.Write, "{\n}\n"));

        File.ReadAllBytes(_sidecar).ShouldBe("{\n}\n"u8.ToArray());
        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql", "Users.sql.json", "sqlsource.json"]);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_WriteOverAFile_ReplacesIt()
    {
        File.WriteAllText(_sidecar, "old and longer than the new text");

        Apply(Outcome(FileAction.Write, "new"));

        File.ReadAllText(_sidecar).ShouldBe("new");
        Left().Length.ShouldBe(5);
    }

    [Theory]
    [InlineData(nameof(FileAction.Unchanged))]
    [InlineData(nameof(FileAction.None))]
    [InlineData(nameof(FileAction.HeldBack))]
    public void Apply_ActionThatChangesNothing_LeavesTheFileAsItWas(string actionName)
    {
        var action = Enum.Parse<FileAction>(actionName);
        File.WriteAllText(_sidecar, "as it was");
        File.SetLastWriteTimeUtc(_sidecar, SidecarFiles.Old);

        Apply(Outcome(action, action == FileAction.Unchanged ? "as it was" : null));

        File.ReadAllText(_sidecar).ShouldBe("as it was");
        File.GetLastWriteTimeUtc(_sidecar).ShouldBe(SidecarFiles.Old);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_Delete_RemovesTheFile()
    {
        File.WriteAllText(_sidecar, "of a file that needs none");

        Apply(Outcome(FileAction.Delete));

        File.Exists(_sidecar).ShouldBeFalse();
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_HeldBackWithAQueryDescribed_IsSqlsrc217WithTheQueriesThatHeldItBack()
    {
        var outcome = Outcome(
            FileAction.HeldBack,
            text: null,
            new HeldBackQuery("GetUser", Failed: true),
            new HeldBackQuery("GetInvoice", Failed: false)
        ) with
        {
            HasDescribed = true,
        };

        Apply(outcome);

        _error
            .ToString()
            .ShouldBe(
                $"{_file.Path} : error SQLSRC217: The sidecar of '{_file.Path}' was not written, because not every "
                    + "query of the file has an entry\n"
                    + "    query: GetUser: failed\n"
                    + "    query: GetInvoice: not in this run\n"
                    + "    help: describe the whole file, or fix the queries that failed\n"
                    + Expected.See("SQLSRC217")
            );
        File.Exists(_sidecar).ShouldBeFalse();
    }

    // A path that cannot be written, on every operating system and for every user.
    [Fact]
    public void Apply_WriteWhereADirectoryStands_IsSqlsrc218AndLeavesNoTemporaryFile()
    {
        _ = Directory.CreateDirectory(_sidecar);

        Apply(Outcome(FileAction.Write, "{}"));

        _error.ToString().ShouldStartWith($"{_sidecar} : error SQLSRC218: '{_sidecar}' could not be written: ");
        _error.ToString().ShouldEndWith(Expected.See("SQLSRC218"));
        Directory.Exists(_sidecar).ShouldBeTrue();
        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql", "Users.sql.json", "sqlsource.json"]);
    }

    [Fact]
    public void Apply_DeleteOfWhatIsNoFile_IsSqlsrc218()
    {
        _ = Directory.CreateDirectory(_sidecar);

        Apply(Outcome(FileAction.Delete));

        _error.ToString().ShouldStartWith($"{_sidecar} : error SQLSRC218: '{_sidecar}' could not be deleted: ");
    }

    [Fact]
    public void Apply_SidecarThatCouldNotBeOpened_ReportsTheErrorOfItsOutcomeAndLeavesIt()
    {
        _ = Directory.CreateDirectory(_sidecar);
        var failure = ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, _sidecar, "read", "refused");

        Apply(Outcome(FileAction.Unreadable) with { Failure = failure });

        _error
            .ToString()
            .ShouldBe(
                $"{_sidecar} : error SQLSRC218: '{_sidecar}' could not be read: refused\n" + Expected.See("SQLSRC218")
            );
        Directory.Exists(_sidecar).ShouldBeTrue();
    }

    [Fact]
    public void Apply_FailureOfOneFile_DoesNotKeepTheNextFromBeingWritten()
    {
        _ = Directory.CreateDirectory(_sidecar);
        var other = _file.Path + ".other.json";

        Apply(Outcome(FileAction.Write, "{}"), Outcome(FileAction.Write, "{}") with { SidecarPath = other });

        File.ReadAllText(other).ShouldBe("{}");
        _error.ToString().Split("error SQLSRC218").Length.ShouldBe(2);
    }

    [Fact]
    public void Apply_RunThatWasCancelled_WritesNothing()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        _ = Should.Throw<OperationCanceledException>(() =>
            SidecarStore.Apply(
                [Outcome(FileAction.Write, "{}")],
                new Reporter(_error),
                _clock,
                onTheWhole: false,
                cancelled.Token
            )
        );

        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql", "sqlsource.json"]);
    }

    [Fact]
    public void Apply_RunOnTheWholeThatWasCancelled_RemovesNothing()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var leftover = Leftover(TimeSpan.FromDays(3));

        _ = Should.Throw<OperationCanceledException>(() =>
            SidecarStore.Apply(
                [Outcome(FileAction.Write, "{}")],
                new Reporter(_error),
                _clock,
                onTheWhole: true,
                cancelled.Token
            )
        );

        File.Exists(leftover).ShouldBeTrue();
    }

    // The run is cancelled when the store has begun: it reads the clock once, before it removes anything.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_RunThatIsCancelledAsItBegins_RemovesNothing(bool onTheWhole)
    {
        using var cancel = new CancellationTokenSource();
        var leftover = Leftover(TimeSpan.FromDays(3));

        _ = Should.Throw<OperationCanceledException>(() =>
            SidecarStore.Apply(
                [Outcome(FileAction.Write, "{}")],
                new Reporter(_error),
                new CancellingClock(cancel, _clock.Now),
                onTheWhole,
                cancel.Token
            )
        );

        File.Exists(leftover).ShouldBeTrue();
        File.Exists(_sidecar).ShouldBeFalse();
    }

    // A clock that cancels a run when it is read.
    private sealed class CancellingClock(CancellationTokenSource cancel, DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            cancel.Cancel();
            return now;
        }
    }

    // The .sql file of the temporary file is gone, or no type claims it any more, so no run looks at its sidecar.
    // A run on the whole knows every file of the project and removes what a sidecar of the folder left, whichever
    // file it was of and whatever the run does with the sidecars that are there.
    [Theory]
    [InlineData(nameof(FileAction.Write))]
    [InlineData(nameof(FileAction.None))]
    public void Apply_RunOnTheWhole_RemovesTheTemporaryFilesOfEverySidecarOfTheFolder(string actionName)
    {
        var action = Enum.Parse<FileAction>(actionName);
        var own = Leftover(TimeSpan.FromDays(3));
        var ofAFileThatIsGone = Leftover(TimeSpan.FromDays(3), "Gone.sql.json.0123456789abcdef0123456789abcdef.tmp");
        var ofAnotherCase = Leftover(TimeSpan.FromDays(3), "Loud.SQL.json.0123456789abcdef0123456789abcdef.tmp");

        Apply(onTheWhole: true, Outcome(action, action == FileAction.Write ? "{}" : null));

        File.Exists(own).ShouldBeFalse();
        File.Exists(ofAFileThatIsGone).ShouldBeFalse();
        File.Exists(ofAnotherCase).ShouldBeFalse();
        _error.ToString().ShouldBeEmpty();
    }

    // The generator takes a sidecar in any case, so the sweep of a run on the whole does too.
    [Fact]
    public void Apply_RunOnTheWhole_RemovesTheTemporaryFileOfASidecarInCapitals()
    {
        var inCapitals = Leftover(TimeSpan.FromDays(3), "Q.SQL.JSON.0123456789abcdef0123456789abcdef.tmp");

        Apply(onTheWhole: true, Outcome(FileAction.None));

        File.Exists(inCapitals).ShouldBeFalse();
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RunOnTheWhole_LeavesATemporaryFileThatIsNew()
    {
        var writing = Leftover(
            SidecarStore.LeftoverAge - TimeSpan.FromSeconds(1),
            "Gone.sql.json.0123456789abcdef0123456789abcdef.tmp"
        );

        Apply(onTheWhole: true, Outcome(FileAction.Write, "{}"));

        File.Exists(writing).ShouldBeTrue();
    }

    // Only the temporary file of a sidecar: a file of another program that ends the same way is not the tool's.
    [Theory]
    [InlineData("Users.sql.json.tmp")]
    [InlineData("Users.sql.json.backup.tmp")]
    [InlineData("Users.sql.json.0123456789abcdef0123456789abcde.tmp")]
    [InlineData("Users.sql.json.0123456789abcdef0123456789abcdeg.tmp")]
    [InlineData("Users.sql.json.0123456789ABCDEF0123456789abcdef.tmp")]
    [InlineData("Users.sql.json.0123456789abcdef0123456789abcdef.tmp.txt")]
    [InlineData("Users.sql.0123456789abcdef0123456789abcdef.tmp")]
    [InlineData("settings.json.0123456789abcdef0123456789abcdef.tmp")]
    [InlineData("Users.sqlx.json.0123456789abcdef0123456789abcdef.tmp")]
    [InlineData("0123456789abcdef0123456789abcdef.tmp")]
    public void Apply_RunOnTheWhole_LeavesAFileOfAnotherName(string name)
    {
        var other = Leftover(TimeSpan.FromDays(3), name);

        Apply(onTheWhole: true, Outcome(FileAction.Write, "{}"));

        File.Exists(other).ShouldBeTrue();
    }

    // A folder of the project that holds no file of the plan is not looked into.
    [Fact]
    public void Apply_RunOnTheWhole_LeavesAFolderThatHoldsNoFileOfTheRun()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(_sidecar)!, "Other")).FullName;
        var elsewhere = Leftover(
            TimeSpan.FromDays(3),
            Path.Combine(folder, "Gone.sql.json.0123456789abcdef0123456789abcdef.tmp")
        );

        Apply(onTheWhole: true, Outcome(FileAction.Write, "{}"));

        File.Exists(elsewhere).ShouldBeTrue();
    }

    // A run that was killed between the write and the move left its temporary file, and nothing else removes it.
    [Theory]
    [InlineData(nameof(FileAction.Write))]
    [InlineData(nameof(FileAction.Delete))]
    [InlineData(nameof(FileAction.Unchanged))]
    [InlineData(nameof(FileAction.HeldBack))]
    public void Apply_FileTheRunLooksAt_RemovesTheTemporaryFilesAnEarlierRunLeft(string actionName)
    {
        var action = Enum.Parse<FileAction>(actionName);
        var first = Leftover(TimeSpan.FromDays(3));
        var second = Leftover(SidecarStore.LeftoverAge + TimeSpan.FromSeconds(1));

        Apply(Outcome(action, action is FileAction.Write or FileAction.Unchanged ? "{}" : null));

        File.Exists(first).ShouldBeFalse();
        File.Exists(second).ShouldBeFalse();
        _error.ToString().ShouldBeEmpty();
    }

    // The run did not look at the sidecar, as under a filter that leaves the file out: nothing beside it is changed.
    [Fact]
    public void Apply_FileTheRunDoesNotLookAt_LeavesTheTemporaryFileOfAnEarlierRun()
    {
        var leftover = Leftover(TimeSpan.FromDays(3));

        Apply(Outcome(FileAction.None));

        File.Exists(leftover).ShouldBeTrue();
    }

    // Another run that writes the same sidecar now has its temporary file there for a moment.
    [Fact]
    public void Apply_TemporaryFileThatIsNew_IsLeftForTheRunThatWritesIt()
    {
        var writing = Leftover(SidecarStore.LeftoverAge - TimeSpan.FromSeconds(1));
        var ahead = Leftover(-TimeSpan.FromHours(1));

        Apply(Outcome(FileAction.Write, "{}"));

        File.Exists(writing).ShouldBeTrue();
        File.Exists(ahead).ShouldBeTrue();
    }

    // Only a name that this store gives a temporary file of this sidecar: nothing else in a project is the tool's.
    [Theory]
    [InlineData("Users.sql.json.tmp")]
    [InlineData("Users.sql.json.backup.tmp")]
    [InlineData("Users.sql.json.0123456789abcdef0123456789abcde.tmp")]
    [InlineData("Users.sql.json.0123456789abcdef0123456789abcdef0.tmp")]
    [InlineData("Users.sql.json.0123456789abcdef0123456789abcdeg.tmp")]
    [InlineData("Users.sql.json.0123456789abcdef0123456789abcdef.tmp.txt")]
    [InlineData("Users.sql.json.x.0123456789abcdef0123456789abcdef.tmp")]
    [InlineData("Others.sql.json.0123456789abcdef0123456789abcdef.tmp")]
    [InlineData("Users.sql.0123456789abcdef0123456789abcdef.tmp")]
    public void Apply_FileOfAnotherName_IsLeft(string name)
    {
        var other = Leftover(TimeSpan.FromDays(3), name);

        Apply(Outcome(FileAction.Write, "{}"));

        File.Exists(other).ShouldBeTrue();
    }

    // What stands where a temporary file would is not removed, and is no error of the run.
    [Fact]
    public void Apply_DirectoryWithTheNameOfATemporaryFile_IsLeftAndTheSidecarIsWritten()
    {
        var folder = Path.Combine(Path.GetDirectoryName(_sidecar)!, $"Users.sql.json.{Guid.NewGuid():N}.tmp");
        _ = Directory.CreateDirectory(folder);

        Apply(Outcome(FileAction.Write, "{}"));

        Directory.Exists(folder).ShouldBeTrue();
        File.ReadAllText(_sidecar).ShouldBe("{}");
        _error.ToString().ShouldBeEmpty();
    }
}
