using System;
using System.Globalization;
using System.IO;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

public sealed class RunUnitTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public void Dispose()
    {
        _folder.Dispose();
        _error.Dispose();
    }

    // The finder reports nothing: its caller does, as DescribeCommand does here.
    private RunUnit? Find(string? argument, int position = 2)
    {
        var found = RunUnitFinder.Find(
            argument is null ? null : new UsageValue(null, argument, position),
            _folder.Path
        );
        if (found.Succeeded)
        {
            return found.Value;
        }

        new Reporter(_error).Report(found.Failure);
        return null;
    }

    private void ShouldHaveReported(string id, string path) => ShouldHaveReportedNamed(id, $"'{path}'");

    // The first line starts with how the path is named: in quotes, or by its position.
    private void ShouldHaveReportedNamed(string id, string named)
    {
        var lines = _error.ToString().Split('\n');
        lines.Length.ShouldBe(3, _error.ToString());
        lines[0].ShouldStartWith($"sqlsource : error {id}: {named} ");
    }

    [Theory]
    [InlineData("App.csproj", nameof(RunUnitKind.Project))]
    [InlineData("App.sln", nameof(RunUnitKind.Solution))]
    [InlineData("App.slnx", nameof(RunUnitKind.Solution))]
    [InlineData("App.CSPROJ", nameof(RunUnitKind.Project))]
    [InlineData("App.Sln", nameof(RunUnitKind.Solution))]
    [InlineData("APP.SLNX", nameof(RunUnitKind.Solution))]
    public void Find_NoArgument_IsTheOneUnitOfTheWorkingDirectory(string file, string kind)
    {
        var path = _folder.WriteFile(file);
        _ = _folder.WriteFile("notes.txt");
        _ = _folder.WriteFile("App.slnf");

        Find(null).ShouldBe(new RunUnit(Enum.Parse<RunUnitKind>(kind), path));
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Find_NoArgumentAndNoUnit_IsSqlsrc201()
    {
        _ = _folder.WriteFile("App.fsproj");
        _ = _folder.WriteFile("App.slnf");

        Find(null).ShouldBeNull();
        ShouldHaveReported("SQLSRC201", _folder.Path);
    }

    [Theory]
    [InlineData("One.csproj", "Two.csproj")]
    [InlineData("One.sln", "Two.sln")]
    [InlineData("App.sln", "App.csproj")]
    [InlineData("App.sln", "App.slnx")]
    public void Find_NoArgumentAndSeveralUnits_IsSqlsrc202(string first, string second)
    {
        _ = _folder.WriteFile(first);
        _ = _folder.WriteFile(second);

        Find(null).ShouldBeNull();
        ShouldHaveReported("SQLSRC202", _folder.Path);
    }

    [Fact]
    public void Find_FolderNamedLikeAProject_IsNotAUnit()
    {
        _ = _folder.CreateFolder("Nested.csproj");
        var path = _folder.WriteFile("App.csproj");

        Find(null).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Fact]
    public void Find_UnitInAFolderBelow_IsNotFound()
    {
        _ = _folder.WriteFile("src/App/App.csproj");

        Find(null).ShouldBeNull();
        ShouldHaveReported("SQLSRC201", _folder.Path);
    }

    [Theory]
    [InlineData("src/App")]
    [InlineData("src/App/")]
    [InlineData("./src/App")]
    [InlineData("src/Other/../App")]
    public void Find_RelativeDirectory_IsTheOneUnitInIt(string argument)
    {
        var path = _folder.WriteFile("src/App/App.csproj");
        _ = _folder.WriteFile("Whole.sln");

        Find(argument).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Fact]
    public void Find_Dot_IsTheWorkingDirectory()
    {
        var path = _folder.WriteFile("Whole.slnx");

        Find(".").ShouldBe(new RunUnit(RunUnitKind.Solution, path));
    }

    [Fact]
    public void Find_AbsoluteDirectory_IsTheOneUnitInIt()
    {
        var path = _folder.WriteFile("src/App/App.csproj");

        Find(_folder.PathOf("src/App")).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Fact]
    public void Find_DirectoryWithoutAUnit_IsSqlsrc201WithItsFullPath()
    {
        var directory = _folder.CreateFolder("src/Empty");

        Find("src/Empty/").ShouldBeNull();
        ShouldHaveReported("SQLSRC201", directory);
    }

    [Fact]
    public void Find_DirectoryWithSeveralUnits_IsSqlsrc202WithItsFullPath()
    {
        _ = _folder.WriteFile("src/Two/One.csproj");
        _ = _folder.WriteFile("src/Two/Two.csproj");

        Find("src/Two").ShouldBeNull();
        ShouldHaveReported("SQLSRC202", _folder.PathOf("src/Two"));
    }

    [Theory]
    [InlineData("src/App/App.csproj", nameof(RunUnitKind.Project))]
    [InlineData("Whole.sln", nameof(RunUnitKind.Solution))]
    [InlineData("Whole.SLNX", nameof(RunUnitKind.Solution))]
    public void Find_RelativeFile_IsThatFile(string file, string kind)
    {
        var path = _folder.WriteFile(file);
        // A file that is named is the unit, whatever lies beside it.
        _ = _folder.WriteFile(Path.Combine(Path.GetDirectoryName(file)!, "Other.csproj"));

        Find(file).ShouldBe(new RunUnit(Enum.Parse<RunUnitKind>(kind), path));
    }

    [Fact]
    public void Find_AbsoluteFile_IsThatFile()
    {
        var path = _folder.WriteFile("src/App/App.csproj");

        Find(path).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Theory]
    [InlineData("Missing.csproj")]
    [InlineData("missing/folder")]
    public void Find_PathThatDoesNotExist_IsSqlsrc203WithItsFullPath(string argument)
    {
        Find(argument).ShouldBeNull();
        ShouldHaveReported("SQLSRC203", _folder.PathOf(argument));
    }

    [Theory]
    [InlineData("App.slnf")]
    [InlineData("App.fsproj")]
    [InlineData("App.csproj.user")]
    [InlineData("csproj")]
    public void Find_FileOfAnotherKind_IsSqlsrc203(string file)
    {
        var path = _folder.WriteFile(file);

        Find(file).ShouldBeNull();
        ShouldHaveReported("SQLSRC203", path);
    }

    [Fact]
    public void Find_EmptyArgument_IsSqlsrc203AndNotTheWorkingDirectory()
    {
        // What "$UNSET" gives in a shell.  It must not run on whatever the working directory holds.
        _ = _folder.WriteFile("App.csproj");

        Find("").ShouldBeNull();
        ShouldHaveReported("SQLSRC203", "");
    }

    // A connection string typed where the path stands: it is named by its position, and never repeated.
    [Fact]
    public void Find_PathWithAnEqualsSign_IsNamedByItsPosition()
    {
        Find("Host=db;Password=" + Secrets.Marker).ShouldBeNull();

        _error
            .ToString()
            .ShouldStartWith(
                "sqlsource : error SQLSRC203: The path at position 2 is not a .sln, .slnx or .csproj file, or a "
                    + "directory that holds one\n"
            );
        _error.ToString().ShouldNotContain(Secrets.Marker);
    }

    [Fact]
    public void Find_PathWithASemicolon_IsNamedByItsPosition()
    {
        Find("a;b", position: 5).ShouldBeNull();

        ShouldHaveReportedNamed("SQLSRC203", "The path at position 5");
    }

    [Fact]
    public void Find_DirectoryWithAnEqualsSignAndNoUnit_IsSqlsrc201ByItsPosition()
    {
        _ = _folder.CreateFolder("a=b");

        Find("a=b", position: 3).ShouldBeNull();
        ShouldHaveReportedNamed("SQLSRC201", "The path at position 3");
    }

    [Fact]
    public void Find_DirectoryWithAnEqualsSignAndSeveralUnits_IsSqlsrc202ByItsPosition()
    {
        _ = _folder.WriteFile("a=b/One.csproj");
        _ = _folder.WriteFile("a=b/Two.csproj");

        Find("a=b").ShouldBeNull();
        ShouldHaveReportedNamed("SQLSRC202", "The path at position 2");
    }

    [Fact]
    public void Find_DirectoryWithAnEqualsSignAndAUnit_IsTheUnit()
    {
        var path = _folder.WriteFile("a=b/App.csproj");

        Find("a=b").ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    // The rule reads the token and not the full path, so a working directory that holds "=" is printed.
    [Fact]
    public void Find_RelativePathInAFolderWithAnEqualsSign_PrintsTheFullPath()
    {
        var folder = _folder.CreateFolder("a=b");

        var found = RunUnitFinder.Find(new UsageValue(null, "App", 2), folder);

        found.Succeeded.ShouldBeFalse();
        new Reporter(_error).Report(found.Failure);
        ShouldHaveReported("SQLSRC203", Path.Combine(folder, "App"));
    }

    [Fact]
    public void Find_NoArgumentInAFolderWithAnEqualsSign_PrintsTheWorkingDirectoryInQuotes()
    {
        var folder = _folder.CreateFolder("a=b");

        var found = RunUnitFinder.Find(null, folder);

        found.Succeeded.ShouldBeFalse();
        new Reporter(_error).Report(found.Failure);
        ShouldHaveReported("SQLSRC201", folder);
    }

    [Fact]
    public void Find_DirectoryThatCannotBeRead_IsSqlsrc222()
    {
        if (!FileModes.KeepTheOwnerOut)
        {
            Assert.Skip("Needs a file mode that keeps the owner of a directory out of it.");
            return;
        }

        var directory = _folder.CreateFolder("locked");
        File.SetUnixFileMode(directory, UnixFileMode.None);
        try
        {
            Find("locked").ShouldBeNull();
            ShouldHaveReported("SQLSRC222", directory);
            _error.ToString().ShouldContain(" cannot be read: ");
        }
        finally
        {
            // So that the folder of the test can be deleted.
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void Find_DirectoryWithAnEqualsSignThatCannotBeRead_IsSqlsrc222WithoutTheSystemsMessage()
    {
        if (!FileModes.KeepTheOwnerOut)
        {
            Assert.Skip("Needs a file mode that keeps the owner of a directory out of it.");
            return;
        }

        var directory = _folder.CreateFolder("locked=1");
        File.SetUnixFileMode(directory, UnixFileMode.None);
        try
        {
            Find("locked=1").ShouldBeNull();
            _error
                .ToString()
                .ShouldStartWith(
                    "sqlsource : error SQLSRC222: The path at position 2 cannot be read: the system would not list "
                        + "its files\n"
                );
        }
        finally
        {
            // So that the folder of the test can be deleted.
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
