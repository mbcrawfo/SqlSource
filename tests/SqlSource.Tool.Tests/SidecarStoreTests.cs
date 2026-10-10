using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// The one place that changes a sidecar on the disk: a file is written whole or not at all.
public sealed class SidecarStoreTests : IDisposable
{
    private const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly TempFolder _folder = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly PlannedFile _file;
    private readonly string _sidecar;

    public SidecarStoreTests()
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSql("Users.sql", "-- name: GetUser\nSELECT 1;\n");
        _file = Plans.Of(project).Files[0];
        _sidecar = _file.Path + ".json";
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

    private void Apply(params FileOutcome[] outcomes) =>
        SidecarStore.Apply([.. outcomes], new Reporter(_error), TestContext.Current.CancellationToken);

    // The names of what is in the project's folder, to see that nothing else was left there.
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
        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql", "Users.sql.json"]);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_WriteOverAFile_ReplacesIt()
    {
        File.WriteAllText(_sidecar, "old and longer than the new text");

        Apply(Outcome(FileAction.Write, "new"));

        File.ReadAllText(_sidecar).ShouldBe("new");
        Left().Length.ShouldBe(4);
    }

    [Theory]
    [InlineData(nameof(FileAction.Unchanged))]
    [InlineData(nameof(FileAction.None))]
    [InlineData(nameof(FileAction.HeldBack))]
    public void Apply_ActionThatChangesNothing_LeavesTheFileAsItWas(string actionName)
    {
        var action = Enum.Parse<FileAction>(actionName);
        File.WriteAllText(_sidecar, "as it was");
        File.SetLastWriteTimeUtc(_sidecar, Old);

        Apply(Outcome(action, action == FileAction.Unchanged ? "as it was" : null));

        File.ReadAllText(_sidecar).ShouldBe("as it was");
        File.GetLastWriteTimeUtc(_sidecar).ShouldBe(Old);
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
                    + $"{See}217\n"
            );
        File.Exists(_sidecar).ShouldBeFalse();
    }

    // Review focus 1: a path that cannot be written, on every operating system and for every user.
    [Fact]
    public void Apply_WriteWhereADirectoryStands_IsSqlsrc218AndLeavesNoTemporaryFile()
    {
        _ = Directory.CreateDirectory(_sidecar);

        Apply(Outcome(FileAction.Write, "{}"));

        _error.ToString().ShouldStartWith($"{_sidecar} : error SQLSRC218: '{_sidecar}' could not be written: ");
        _error.ToString().ShouldEndWith($"{See}218\n");
        Directory.Exists(_sidecar).ShouldBeTrue();
        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql", "Users.sql.json"]);
    }

    [Fact]
    public void Apply_DeleteOfWhatIsNoFile_IsSqlsrc218()
    {
        _ = Directory.CreateDirectory(_sidecar);

        Apply(Outcome(FileAction.Delete));

        _error.ToString().ShouldStartWith($"{_sidecar} : error SQLSRC218: '{_sidecar}' could not be deleted: ");
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
            SidecarStore.Apply([Outcome(FileAction.Write, "{}")], new Reporter(_error), cancelled.Token)
        );

        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql"]);
    }
}
