using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "describe --check": every query is described, the sidecars on the disk are compared, and no file is changed.
public sealed class CheckTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public CheckTests()
    {
        _users = _scene.Project.AddSql("Users.sql", Users);
    }

    private string Sidecar => _users + ".json";

    public void Dispose() => _scene.Dispose();

    // Describes, and makes the sidecar look old, so that a write would show.
    private async Task<byte[]> DescribeFirstAsync()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        File.SetLastWriteTimeUtc(Sidecar, Old);
        return await File.ReadAllBytesAsync(Sidecar, TestContext.Current.CancellationToken);
    }

    private async Task ShouldBeUntouchedAsync(byte[] before)
    {
        (await File.ReadAllBytesAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(before);
        File.GetLastWriteTimeUtc(Sidecar).ShouldBe(Old);
    }

    [Theory]
    [InlineData("--check")]
    [InlineData("--check", "--force")]
    public async Task Check_SidecarsThatDescribeWrote_DescribesEveryQueryAgainAndChangesNoFile(params string[] args)
    {
        var before = await DescribeFirstAsync();

        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        result.Out.ShouldBe("postgres (postgres): 2 same, 0 differ, 0 failed\n");
        // Two by the first run, and two by the check: none is skipped for a current entry.
        _scene.Describer.Described.Count.ShouldBe(4);
        await ShouldBeUntouchedAsync(before);
    }

    [Fact]
    public async Task Check_QueryThatFails_ExitsOneAndChangesNoFile()
    {
        var before = await DescribeFirstAsync();
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC999: ");
        // A description that is not saved is no error of a check, which saves none.
        result.Error.ShouldNotContain("SQLSRC217");
        await ShouldBeUntouchedAsync(before);
    }

    // Without --check a database whose queries are all current needs no connection.
    [Fact]
    public async Task Check_DatabaseWithoutAConnection_IsSqlsrc213ThoughEveryEntryIsCurrent()
    {
        _ = await DescribeFirstAsync();
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC213: No connection is given for the database 'postgres'");
    }

    [Fact]
    public async Task Check_SidecarOfAHigherFormatVersion_IsNotSqlsrc221AndItsQueriesAreDescribed()
    {
        _ = await DescribeFirstAsync();
        var newer = SidecarWriter.Write(TestSidecar.Read(_users) with { FormatVersion = 2 });
        await File.WriteAllTextAsync(Sidecar, newer, TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldNotContain("SQLSRC221");
        _scene.Describer.Described.Count.ShouldBe(4);
        (await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(newer);
    }

    // The help line depends on what differs: the command that mends it.
    private const string Help = "    help: run 'dotnet sqlsource describe' and commit the sidecars\n";
    private const string ForceHelp = "    help: run 'dotnet sqlsource describe --force' and commit the sidecars\n";
    private const string UpdateHelp = "    help: update the SqlSource.Tool package\n";

    // A statement that produces rows, where the describer of the tests says none unless told.
    private static readonly QueryDescription Rows = new(
        SidecarResultKind.Rows,
        EquatableArray<DescribedParameter>.Empty,
        EquatableArray<SidecarColumn>.Empty,
        null,
        "not-needed",
        "no-origin"
    );

    // One SQLSRC219 as the reporter writes it.  The origin is a path with a position, or a path and a space.
    private static string Difference(string origin, string subject, string what, string help = Help) =>
        $"{origin}: error SQLSRC219: The sidecar is out of date for '{subject}': {what}\n"
        + $"{help}{DescribeScene.See}219\n";

    // Where a key stands in a text, as the reporter writes a position: "(line,column)", each counted from one.
    private static string PositionOf(string text, string key)
    {
        var index = text.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        var line = text[..index].Count(character => character == '\n') + 1;
        return $"({line},{index - text.LastIndexOf('\n', index)})";
    }

    // Writes the sidecar again with a change, and makes it look old.
    private async Task<string> RewriteAsync(Func<Sidecar, Sidecar> change)
    {
        var text = SidecarWriter.Write(change(TestSidecar.Read(_users)));
        await File.WriteAllTextAsync(Sidecar, text, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Sidecar, Old);
        return text;
    }

    private static Sidecar WithEntries(Sidecar sidecar, params SidecarEntry[] entries) =>
        sidecar with
        {
            Queries = new EquatableArray<SidecarEntry>([.. entries]),
        };

    [Fact]
    public async Task Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo()
    {
        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{_users}(1,10)", _users, "the sidecar does not exist"));
        File.Exists(Sidecar).ShouldBeFalse();
        Directory.GetFiles(Path.GetDirectoryName(_users)!, "*.tmp").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("a merge conflict")]
    [InlineData("a directory")]
    public async Task Check_SidecarThatCannotBeRead_IsOneDifferenceAtTheSidecar(string kind)
    {
        var written = Encoding.UTF8.GetString(await DescribeFirstAsync());
        if (kind == "a directory")
        {
            File.Delete(Sidecar);
            _ = Directory.CreateDirectory(Sidecar);
        }
        else
        {
            await File.WriteAllTextAsync(Sidecar, "<<<<<<< HEAD\n" + written, TestContext.Current.CancellationToken);
        }

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{Sidecar} ", _users, "the sidecar cannot be read"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Check_SidecarOfAnotherFormatVersion_IsOneDifferenceAtTheSidecar(int version)
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar => sidecar with { FormatVersion = version });

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(
            Difference(
                $"{Sidecar} ",
                _users,
                $"the sidecar has format {version}, and this tool writes {SidecarFormat.Version}",
                // A newer tool's file is left alone by describe, and an older one is written again.
                version > SidecarFormat.Version
                    ? UpdateHelp
                    : Help
            )
        );
    }

    [Fact]
    public async Task Check_QueryWithoutAnEntry_IsADifferenceAtTheQuery()
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar => WithEntries(sidecar, sidecar.Find("GetUser").ShouldNotBeNull()));

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{_users}(4,10)", "ListUsers", "the sidecar has no entry"));
    }

    // The commonest difference: SQL that was edited and not described again.  Neither hash is printed.
    [Fact]
    public async Task Check_QueryThatChanged_SaysSoWithoutTheHashes()
    {
        _ = await DescribeFirstAsync();
        var hash = TestSidecar.Read(_users).Find("GetUser").ShouldNotBeNull().Hash;
        await File.WriteAllTextAsync(
            _users,
            Users.Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{_users}(1,10)", "GetUser", "the query has changed since it was described"));
        result.Error.ShouldNotContain(hash);
    }

    [Fact]
    public async Task Check_DatabaseDescribesTheQueryAnotherWay_NamesThePathAndTheTwoValues()
    {
        var before = await DescribeFirstAsync();
        _scene.Describer.Descriptions["ListUsers"] = Rows;

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(
            Difference(
                $"{_users}(4,10)",
                "ListUsers",
                "resultKind is \"none\", and the database says \"rows\"",
                ForceHelp
            )
        );
        await ShouldBeUntouchedAsync(before);
    }

    // The database of a query comes from the project, not from the server.
    [Fact]
    public async Task Check_EntryOfAnotherDatabase_SaysWhatTheProjectSays()
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar,
                sidecar.Find("GetUser").ShouldNotBeNull() with
                {
                    Database = "elsewhere",
                },
                sidecar.Find("ListUsers").ShouldNotBeNull()
            )
        );

        var result = await _scene.DescribeAsync("--check");

        result.Error.ShouldBe(
            Difference($"{_users}(1,10)", "GetUser", "database is \"elsewhere\", and the project says \"postgres\"")
        );
    }

    [Fact]
    public async Task Check_EntryOfAQueryThatIsGone_IsADifferenceAtTheEntrysLineInTheSidecar()
    {
        _ = await DescribeFirstAsync();
        var text = await RewriteAsync(sidecar =>
            WithEntries(sidecar, [.. sidecar.Queries, sidecar.Find("GetUser").ShouldNotBeNull() with { Name = "Gone" }])
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference(Sidecar + PositionOf(text, "Gone"), "Gone", "the query is not in the file"));
    }

    [Fact]
    public async Task Check_EntryOfAQueryThatNeedsNone_IsADifferenceAtTheEntrysLineInTheSidecar()
    {
        _ = await DescribeFirstAsync();
        var text = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar,
                [.. sidecar.Queries, sidecar.Find("GetUser").ShouldNotBeNull() with { Name = "Plain" }]
            )
        );
        await File.AppendAllTextAsync(
            _users,
            "\n-- name: Plain\n-- output: sql\nSELECT 3;\n",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        result.Error.ShouldBe(Difference(Sidecar + PositionOf(text, "Plain"), "Plain", "the query needs no entry"));
    }

    // The sidecar of a file that needs none any more, which "describe" would delete.
    private string AddPlainWithASidecar(int formatVersion = SidecarFormat.Version)
    {
        var plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        File.WriteAllText(
            plain + ".json",
            SidecarWriter.Write(new Sidecar(formatVersion, PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty))
        );
        return plain;
    }

    [Fact]
    public async Task Check_SidecarOfAFileThatNeedsNone_IsADifferenceAndTheFileStays()
    {
        _ = await DescribeFirstAsync();
        var plain = AddPlainWithASidecar();

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{plain}.json ", plain, "no query of the file needs an entry"));
        File.Exists(plain + ".json").ShouldBeTrue();
    }

    // "describe" deletes nothing after an error, and leaves a newer tool's file alone: so the check says nothing.
    [Fact]
    public async Task Check_SidecarOfAFileThatNeedsNoneAfterAnError_IsNotReported()
    {
        _ = await DescribeFirstAsync();
        _ = AddPlainWithASidecar();
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain("SQLSRC219");
    }

    [Fact]
    public async Task Check_SidecarOfANewerToolBesideAFileThatNeedsNone_IsNotReported()
    {
        _ = await DescribeFirstAsync();
        _ = AddPlainWithASidecar(formatVersion: SidecarFormat.Version + 1);

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    // What differs between a developer's machine and a build server by design, and the order of the entries.
    [Fact]
    public async Task Check_MembersThatAreNotCompared_AreNoDifference()
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar with
                {
                    ToolVersion = "0.0.1",
                },
                [.. sidecar.Queries.Reverse().Select(entry => entry with { ServerVersion = "99.9" })]
            )
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    // "Could not check" is not "differs".
    [Fact]
    public async Task Check_FileNoneOfWhoseQueriesCouldBeDescribed_IsComparedWithNothing()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        _scene.Describer.Failures["ListUsers"] = FakeDescriber.Failure("ListUsers");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain("SQLSRC219");
    }

    [Fact]
    public async Task Check_FailureBesideADifference_ExitsOneAndReportsBoth()
    {
        _ = await DescribeFirstAsync();
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        _scene.Describer.Descriptions["ListUsers"] = Rows;

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC999: ");
        result.Error.ShouldContain("error SQLSRC219: The sidecar is out of date for 'ListUsers': ");
        // Exit 1 is the exit of a failure: the line that says how to get a log is written.
        result.Error.ShouldEndWith(DescribeScene.RunAgain);
    }

    // A checkout may give the file other line endings and an editor a byte order mark.  Entries are compared, not
    // text, and the line of an entry is counted in the text as it is.
    [Fact]
    public async Task Check_SidecarWithAByteOrderMarkAndCarriageReturns_IsReadAsAnyOther()
    {
        _ = await DescribeFirstAsync();
        var text = SidecarWriter.Write(TestSidecar.Read(_users));
        await File.WriteAllTextAsync(
            Sidecar,
            text.Replace("\n", "\r\n", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_EntryThatIsGoneInASidecarWithCarriageReturns_HasTheLineOfTheTextAsItIs()
    {
        _ = await DescribeFirstAsync();
        var text = await RewriteAsync(sidecar =>
            WithEntries(sidecar, [.. sidecar.Queries, sidecar.Find("GetUser").ShouldNotBeNull() with { Name = "Gone" }])
        );
        await File.WriteAllTextAsync(
            Sidecar,
            text.Replace("\n", "\r\n", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        // A line break of two characters moves no column: a key stands after the indent of its own line.
        result.Error.ShouldBe(Difference(Sidecar + PositionOf(text, "Gone"), "Gone", "the query is not in the file"));
    }
}
