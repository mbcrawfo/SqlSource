using System;
using System.Globalization;
using System.IO;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class RunUnitTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public void Dispose()
    {
        _folder.Dispose();
        _error.Dispose();
    }

    private RunUnit? Find(string? argument) => RunUnitFinder.Find(argument, _folder.Path, new Reporter(_error));

    private void ShouldHaveReported(string id, string path)
    {
        var lines = _error.ToString().Split('\n');
        lines.Length.ShouldBe(3, _error.ToString());
        lines[0].ShouldStartWith($"sqlsource : error {id}: '{path}' ");
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
}
