using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Validation;

// ProjectIncludes on small projects made for each case, in a repository of the test's own.
public sealed class ProjectIncludesTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("project-includes-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void FromOutside_RemoveAndUpdateElements_AreIgnored() =>
        ProjectIncludes
            .FromOutside(
                "/repo/tests/App",
                Project(
                    new XElement("Compile", new XAttribute("Remove", "../Other/File.cs")),
                    Update("../Other/Doc.md")
                ),
                static _ => true
            )
            .ShouldBeEmpty();

    [Fact]
    public void FromOutside_SemicolonList_JudgesEachPart() =>
        ProjectIncludes
            .FromOutside("/repo/tests/App", Project(Item("Compile", "Local.cs; ../Other/File.cs;;")), static _ => true)
            .ShouldBe(["../Other/File.cs"]);

    [Fact]
    public void FromOutside_PropertyThatIsNotResolved_IsOutside() =>
        ProjectIncludes
            .FromOutside("/repo/tests/App", Project(Item("Compile", "$(Other)/File.cs")), static _ => true)
            .ShouldBe(["$(Other)/File.cs"]);

    [Fact]
    public void FromOutside_ImportOfAnotherFolder_IsFound() =>
        ProjectIncludes
            .FromOutside("/repo/tests/App", Project(Import("../Other/Shared.props")), static _ => true)
            .ShouldBe(["../Other/Shared.props"]);

    [Fact]
    public void Expand_FileWithoutStar_IsTheFileFromTheRoot() =>
        ProjectIncludes
            .Expand(_root.FullName, Path.Combine(_root.FullName, "tests", "App"), "../Other/File.cs")
            .ShouldBe(["tests/Other/File.cs"]);

    [Fact]
    public void Expand_Wildcard_ListsTheFilesThatMatch()
    {
        Write("tests/Other/Fixtures/A.sql");
        Write("tests/Other/Fixtures/Deep/B.sql");
        Write("tests/Other/Fixtures/C.txt");
        Write("tests/Other/Elsewhere/D.sql");

        ProjectIncludes
            .Expand(_root.FullName, Path.Combine(_root.FullName, "tests", "App"), "../Other/Fixtures/*.sql")
            .ShouldBe(["tests/Other/Fixtures/A.sql", "tests/Other/Fixtures/Deep/B.sql"]);
    }

    [Fact]
    public void Refused_ProjectReference_IsClassifiedByItsProjectFileAlone()
    {
        Write("tests/Other/Other.csproj");
        Write("tests/Other/Extra.cs");
        var scopes = Scopes("none tests/Other/Other.csproj", "postgres tests/Other/*");
        WriteProject(Item("ProjectReference", "../Other/Other.csproj"));

        ProjectIncludes.Refused(scopes, _root.FullName).ShouldBe(["App: tests/Other/Other.csproj is None"]);
    }

    [Fact]
    public void Refused_SemicolonList_NamesEachPartThatIsSkipped()
    {
        var scopes = Scopes("none tests/Other/*");
        WriteProject(Item("None", "Local.cs;../Other/File.cs"));

        ProjectIncludes.Refused(scopes, _root.FullName).ShouldBe(["App: tests/Other/File.cs is None"]);
    }

    [Fact]
    public void Refused_ImportOfASkippedFile_NamesIt()
    {
        var scopes = Scopes("none tests/Other/*");
        WriteProject(Import("../Other/Shared.props"));

        ProjectIncludes.Refused(scopes, _root.FullName).ShouldBe(["App: tests/Other/Shared.props is None"]);
    }

    [Fact]
    public void Refused_PropertyThatIsNotResolved_SaysSo()
    {
        var scopes = Scopes("none tests/Other/*");
        WriteProject(Item("Compile", "$(Other)/File.cs"));

        ProjectIncludes
            .Refused(scopes, _root.FullName)
            .ShouldBe(["App: $(Other)/File.cs holds a property the check cannot resolve"]);
    }

    [Fact]
    public void Refused_RemoveOnlyElement_IsIgnored()
    {
        var scopes = Scopes("none tests/Other/*");
        WriteProject(new XElement("Compile", new XAttribute("Remove", "../Other/File.cs")));

        ProjectIncludes.Refused(scopes, _root.FullName).ShouldBeEmpty();
    }

    private static XDocument Project(params XElement[] items) => new(new XElement("Project", items));

    private static XElement Item(string name, string include) => new(name, new XAttribute("Include", include));

    private static XElement Update(string update) => new("None", new XAttribute("Update", update));

    private static XElement Import(string project) => new("Import", new XAttribute("Project", project));

    // The project is `App`, in tests/App, and runs on PostgreSQL.
    private static TestScopes Scopes(params string[] lines) =>
        TestScopes.Parse(string.Join('\n', lines.Prepend("project postgres tests/App/App.csproj")));

    private void WriteProject(params XElement[] items) =>
        Project(items)
            .Save(
                Path.Combine(
                    Directory.CreateDirectory(Path.Combine(_root.FullName, "tests", "App")).FullName,
                    "App.csproj"
                )
            );

    private void Write(string path)
    {
        var file = Path.Combine(_root.FullName, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "");
    }
}
