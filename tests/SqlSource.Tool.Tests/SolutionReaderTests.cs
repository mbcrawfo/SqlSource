using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class SolutionReaderTests : IDisposable
{
    // The type of a C# project and of an F# project, as a .sln names them.
    private const string CSharp = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
    private const string FSharp = "{F2A71F9B-5D33-465A-A702-920D77279786}";
    private const string SolutionFolder = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    // A solution folder, a project under it, one beside the solution, one in a folder above it, and one of another
    // language.  A .sln writes its paths with "\" on every system.
    private const string Sln = $$"""
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{{SolutionFolder}}") = "src", "src", "{00000000-0000-0000-0000-000000000001}"
        EndProject
        Project("{{CSharp}}") = "Web", "src\Web\Web.csproj", "{00000000-0000-0000-0000-000000000002}"
        EndProject
        Project("{{CSharp}}") = "App", "App.csproj", "{00000000-0000-0000-0000-000000000003}"
        EndProject
        Project("{{CSharp}}") = "Shared", "..\Shared\Shared.csproj", "{00000000-0000-0000-0000-000000000004}"
        EndProject
        Project("{{FSharp}}") = "Script", "src\Script\Script.fsproj", "{00000000-0000-0000-0000-000000000005}"
        EndProject

        """;

    private const string Slnx = """
        <Solution>
            <Folder Name="/src/">
                <Project Path="src/Web/Web.csproj" />
                <Project Path="src/Script/Script.fsproj" />
            </Folder>
            <Project Path="App.csproj" />
            <Project Path="../Shared/Shared.csproj" />
        </Solution>

        """;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static Task<SolutionProjects> ReadAsync(string path) =>
        SolutionReader.ReadAsync(path, TestContext.Current.CancellationToken);

    private string[] Expected() =>
        // Ordinal order of the full paths: "Shared" is beside the solution's folder, and "A" is before "s".
        [
            _folder.PathOf("Shared/Shared.csproj"),
            _folder.PathOf("Solution/App.csproj"),
            _folder.PathOf("Solution/src/Web/Web.csproj"),
        ];

    [Theory]
    [InlineData("Solution/App.sln", Sln)]
    [InlineData("Solution/App.slnx", Slnx)]
    [InlineData("Solution/APP.SLNX", Slnx)]
    public async Task Read_Solution_GivesItsCSharpProjectsInOrdinalOrder(string file, string content)
    {
        var read = await ReadAsync(_folder.WriteFile(file, content));

        read.Failure.ShouldBeNull();
        read.Projects.ShouldBe(Expected());
    }

    [Fact]
    public async Task Read_SolutionWithNoProject_GivesNone()
    {
        var read = await ReadAsync(_folder.WriteFile("App.slnx", "<Solution />"));

        read.Failure.ShouldBeNull();
        read.Projects.ShouldBeEmpty();
    }

    // The library refuses a solution that lists one project twice, whatever the two spellings, so the list that
    // is read never holds a project twice.
    [Theory]
    [InlineData("src/App/App.csproj")]
    [InlineData("src/app/APP.csproj")]
    [InlineData("src/App/../App/App.csproj")]
    public async Task Read_ProjectListedTwice_IsNoSolution(string second)
    {
        var read = await ReadAsync(
            _folder.WriteFile(
                "App.slnx",
                $"<Solution><Project Path=\"src/App/App.csproj\" /><Project Path=\"{second}\" /></Solution>"
            )
        );

        read.Failure.ShouldNotBeNullOrWhiteSpace();
        read.Projects.ShouldBeEmpty();
    }

    // The reason is the library's own text, in the language of the machine, so it is not compared.
    [Theory]
    [InlineData("App.sln", "")]
    [InlineData("App.sln", "this is not a solution\n")]
    [InlineData("App.slnx", "")]
    [InlineData("App.slnx", "<Solution><Project Path=\"App.csproj\"")]
    [InlineData("App.slnx", "<Project Sdk=\"Microsoft.NET.Sdk\" />")]
    public async Task Read_FileThatIsNoSolution_GivesTheReason(string file, string content)
    {
        var read = await ReadAsync(_folder.WriteFile(file, content));

        read.Failure.ShouldNotBeNullOrWhiteSpace();
        read.Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_FileThatIsGone_GivesTheReason()
    {
        var read = await ReadAsync(_folder.PathOf("Gone.slnx"));

        read.Failure.ShouldNotBeNullOrWhiteSpace();
        read.Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_FileOfAnotherKind_IsNotRead()
    {
        var read = await ReadAsync(_folder.WriteFile("App.slnf", "{}"));

        read.Failure.ShouldBe("it is neither a .sln nor a .slnx file");
    }
}
