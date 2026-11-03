using System;
using System.Globalization;
using System.IO;
using System.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.DotEnv;

public sealed class DotEnvSearchTests : IDisposable
{
    private const string KeyA = "SQLSOURCE_CONNECTION_A";

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Read_EnvInTheUnitFolder_IsRead()
    {
        var path = _folder.WriteFile("unit/.env", $"{KeyA}=Host=unit\n");

        var file = DotEnvSearch.Read(_folder.PathOf("unit"), ToolHost.ReadText);

        file.Path.ShouldBe(path);
        file.Values.Count.ShouldBe(1);
        file.Values[KeyA].ShouldBe("Host=unit");
        file.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Read_NoEnvAnywhere_GivesNoFile()
    {
        _ = _folder.CreateFolder("unit");

        var file = DotEnvSearch.Read(_folder.PathOf("unit"), ToolHost.ReadText);

        file.Path.ShouldBeNull();
        file.Values.ShouldBeEmpty();
        file.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Read_EnvsInTheUnitFolderAndAbove_NearestWins()
    {
        _ = _folder.CreateFolder("repo/.git");
        _ = _folder.WriteFile("repo/.env", $"{KeyA}=Host=repo\n");
        var nearest = _folder.WriteFile("repo/src/app/.env", $"{KeyA}=Host=app\n");

        var file = DotEnvSearch.Read(_folder.PathOf("repo/src/app"), ToolHost.ReadText);

        file.Path.ShouldBe(nearest);
        file.Values[KeyA].ShouldBe("Host=app");
    }

    [Fact]
    public void Read_EnvAboveTheUnitFolder_IsReadUpToTheRepositoryRoot()
    {
        _ = _folder.CreateFolder("repo/.git");
        var path = _folder.WriteFile("repo/.env", $"{KeyA}=Host=repo\n");
        _ = _folder.CreateFolder("repo/src/app");

        var file = DotEnvSearch.Read(_folder.PathOf("repo/src/app"), ToolHost.ReadText);

        file.Path.ShouldBe(path);
    }

    [Fact]
    public void Read_EnvAboveTheRepositoryRoot_IsNotRead()
    {
        _ = _folder.WriteFile(".env", $"{KeyA}=Host=above\n");
        _ = _folder.CreateFolder("repo/.git");
        _ = _folder.CreateFolder("repo/src");

        var file = DotEnvSearch.Read(_folder.PathOf("repo/src"), ToolHost.ReadText);

        file.Path.ShouldBeNull();
        file.Values.ShouldBeEmpty();
    }

    [Fact]
    public void Read_GitFileMarksTheRoot()
    {
        _ = _folder.WriteFile(".env", $"{KeyA}=Host=above\n");
        _ = _folder.WriteFile("worktree/.git", "gitdir: /elsewhere\n");
        _ = _folder.CreateFolder("worktree/src");

        var file = DotEnvSearch.Read(_folder.PathOf("worktree/src"), ToolHost.ReadText);

        file.Path.ShouldBeNull();
    }

    [Fact]
    public void Read_OutsideARepository_ReadsOnlyTheUnitFolder()
    {
        _ = _folder.WriteFile(".env", $"{KeyA}=Host=parent\n");
        _ = _folder.CreateFolder("unit");

        var file = DotEnvSearch.Read(_folder.PathOf("unit"), ToolHost.ReadText);

        file.Path.ShouldBeNull();
        file.Values.ShouldBeEmpty();
    }

    [Fact]
    public void Read_EnvIsAFolder_IsPassedOverAndTheSearchGoesOn()
    {
        _ = _folder.CreateFolder("repo/.git");
        _ = _folder.CreateFolder("repo/unit/.env/lib");
        var path = _folder.WriteFile("repo/.env", $"{KeyA}=Host=repo\n");

        var file = DotEnvSearch.Read(_folder.PathOf("repo/unit"), ToolHost.ReadText);

        file.Path.ShouldBe(path);
        file.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ReaderThrows_ReportsSqlsrc285AndGivesNothing()
    {
        _ = _folder.CreateFolder("unit");
        var path = _folder.PathOf("unit/.env");

        var file = DotEnvSearch.Read(
            _folder.PathOf("unit"),
            requested => requested == path ? throw new IOException("Disk on fire.") : null
        );

        file.Path.ShouldBe(path);
        file.Values.ShouldBeEmpty();
        var error = file.Errors.ShouldHaveSingleItem();
        error.Descriptor.ShouldBeSameAs(ToolDiagnostics.DotEnvCannotBeRead);
        ReportedLine(error).ShouldBe($"{path} : error SQLSRC285: '{path}' cannot be read: Disk on fire.");
    }

    [Fact]
    public void Read_ReaderDeniesAccess_ReportsSqlsrc285AndGivesNothing()
    {
        _ = _folder.CreateFolder("unit");

        var file = DotEnvSearch.Read(
            _folder.PathOf("unit"),
            static _ => throw new UnauthorizedAccessException("Access denied.")
        );

        file.Values.ShouldBeEmpty();
        file.Errors.ShouldHaveSingleItem().Descriptor.ShouldBeSameAs(ToolDiagnostics.DotEnvCannotBeRead);
    }

    [Fact]
    public void Read_MalformedToolLine_ReportsSqlsrc286AtTheLineAndGivesNothing()
    {
        var path = _folder.WriteFile(
            "unit/.env",
            $"{KeyA}=Host=good\nSQLSOURCE_CONNECTION_B=\"Password=hunter2\nSQLSOURCE_CONNECTION_C=Host=c\n"
        );

        var file = DotEnvSearch.Read(_folder.PathOf("unit"), ToolHost.ReadText);

        file.Path.ShouldBe(path);
        file.Values.ShouldBeEmpty();
        var error = file.Errors.ShouldHaveSingleItem();
        error.Descriptor.ShouldBeSameAs(ToolDiagnostics.DotEnvLineCannotBeRead);
        var line = ReportedLine(error);
        line.ShouldBe(
            $"{path}(2,1): error SQLSRC286: "
                + "The line that sets 'SQLSOURCE_CONNECTION_B' cannot be read: a quote is not closed"
        );
        file.ToString().ShouldNotContain("hunter2");
        line.ShouldNotContain("hunter2");
    }

    // The file is usually the application's, saved by an editor that is not told to use UTF-8.
    [Fact]
    public void Read_ToolLineNotSavedAsUtf8_ReportsSqlsrc286AtTheLineAndGivesNothing()
    {
        var path = _folder.PathOf("unit/.env");
        _ = _folder.CreateFolder("unit");
        File.WriteAllBytes(
            path,
            Encoding.Latin1.GetBytes($"{KeyA}=Password=p\u00e4ss\nGREETING=caf\u00e9\nSQLSOURCE_CONNECTION_B=Host=b\n")
        );

        var file = DotEnvSearch.Read(_folder.PathOf("unit"), ToolHost.ReadText);

        file.Path.ShouldBe(path);
        file.Values.ShouldBeEmpty();
        var error = file.Errors.ShouldHaveSingleItem();
        error.Descriptor.ShouldBeSameAs(ToolDiagnostics.DotEnvLineCannotBeRead);
        var line = ReportedLine(error);
        line.ShouldBe(
            $"{path}(1,1): error SQLSRC286: " + $"The line that sets '{KeyA}' cannot be read: it is not saved as UTF-8"
        );
        line.ShouldNotContain("Password");
    }

    // A line of another key that is not UTF-8 is the application's business: the tool's lines are read.
    [Fact]
    public void Read_NotSavedAsUtf8OnlyInALineOfAnotherKey_GivesTheToolsValuesAndNoError()
    {
        var path = _folder.PathOf("unit/.env");
        _ = _folder.CreateFolder("unit");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes($"GREETING=caf\u00e9\n{KeyA}=Host=a\n"));

        var file = DotEnvSearch.Read(_folder.PathOf("unit"), ToolHost.ReadText);

        file.Path.ShouldBe(path);
        file.Errors.ShouldBeEmpty();
        file.Values.Count.ShouldBe(1);
        file.Values[KeyA].ShouldBe("Host=a");
    }

    [Fact]
    public void Read_TwoMalformedToolLines_ReportEachAndGiveNothing()
    {
        _ = _folder.WriteFile("unit/.env", $"{KeyA}\nSQLSOURCE_CONNECTION_B='x\n");

        var file = DotEnvSearch.Read(_folder.PathOf("unit"), ToolHost.ReadText);

        file.Values.ShouldBeEmpty();
        file.Errors.Length.ShouldBe(2);
    }

    [Fact]
    public void NotRead_SolutionWithAProjectEnv_NamesIt()
    {
        _ = _folder.CreateFolder("repo/.git");
        var read = _folder.WriteFile("repo/.env", $"{KeyA}=Host=repo\n");
        var projectEnv = _folder.WriteFile("repo/src/Api/.env", $"{KeyA}=Host=api\n");
        _ = _folder.CreateFolder("repo/src/Web");
        _ = _folder.WriteFile("repo/src/Web/.env", $"{KeyA}=Host=web\n");

        var notRead = DotEnvSearch.NotRead(
            _folder.PathOf("repo"),
            read,
            [_folder.PathOf("repo/src/Api"), _folder.PathOf("repo/src/Web"), _folder.PathOf("repo/src/Api")],
            ToolHost.ReadText
        );

        notRead.ShouldBe([projectEnv, _folder.PathOf("repo/src/Web/.env")]);
    }

    [Fact]
    public void NotRead_EnvInAFolderBetween_IsNamedOnce()
    {
        _ = _folder.CreateFolder("repo/.git");
        var between = _folder.WriteFile("repo/src/.env", $"{KeyA}=Host=src\n");

        var notRead = DotEnvSearch.NotRead(
            _folder.PathOf("repo"),
            read: null,
            [_folder.PathOf("repo/src/Api"), _folder.PathOf("repo/src/Web")],
            ToolHost.ReadText
        );

        notRead.ShouldBe([between]);
    }

    [Fact]
    public void NotRead_ProjectInTheUnitFolder_IsEmpty()
    {
        _ = _folder.CreateFolder("repo/.git");
        _ = _folder.WriteFile("repo/.env", $"{KeyA}=Host=repo\n");
        _ = _folder.WriteFile("repo/src/.env", $"{KeyA}=Host=src\n");

        var notRead = DotEnvSearch.NotRead(
            _folder.PathOf("repo/src"),
            _folder.PathOf("repo/src/.env"),
            [_folder.PathOf("repo/src")],
            ToolHost.ReadText
        );

        notRead.ShouldBeEmpty();
    }

    [Fact]
    public void NotRead_EnvThatCannotBeRead_Counts()
    {
        _ = _folder.CreateFolder("repo/src/Api");
        var projectEnv = _folder.PathOf("repo/src/Api/.env");

        var notRead = DotEnvSearch.NotRead(
            _folder.PathOf("repo"),
            read: null,
            [_folder.PathOf("repo/src/Api")],
            path => path == projectEnv ? throw new IOException("Disk on fire.") : null
        );

        notRead.ShouldBe([projectEnv]);
    }

    [Fact]
    public void NotRead_FolderNamedEnv_DoesNotCount()
    {
        _ = _folder.CreateFolder("repo/src/Api/.env");

        var notRead = DotEnvSearch.NotRead(
            _folder.PathOf("repo"),
            read: null,
            [_folder.PathOf("repo/src/Api")],
            ToolHost.ReadText
        );

        notRead.ShouldBeEmpty();
    }

    [Fact]
    public void FoldersOf_InsideARepository_AreTheUnitFolderUpToTheRoot()
    {
        _ = _folder.CreateFolder("repo/.git");

        DotEnvSearch
            .FoldersOf(_folder.PathOf("repo/src/app"))
            .ShouldBe([_folder.PathOf("repo/src/app"), _folder.PathOf("repo/src"), _folder.PathOf("repo")]);
    }

    private static string ReportedLine(ToolDiagnostic diagnostic)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        new Reporter(writer).Report(diagnostic);
        return writer.ToString().Split('\n')[0];
    }
}
