using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;

namespace SqlSource.Tool.Tests;

// A run of "diagnose" from the command line to the zip: one project of the engine in the working directory, whose
// type claims the .sql files beside it, the engine's real describer, and a connection in the engine's variable.  The
// database is a case of the describers' fixtures: every call is answered from its capture, so no server is needed.
internal sealed class DiagnoseScene : IDisposable
{
    // The line of a renamed bundle that was written, after the lines of its two paths.
    public const string PrivateLine =
        "sqlsource: the mapping holds your own names: keep it private, outside your project's folder, since a build "
        + "may copy a .json file into its output\n";

    // The line after a raw bundle's path.
    public const string RawLine =
        "sqlsource: this bundle holds your own names and SQL, and must not be posted: open an issue that says a raw "
        + "bundle is ready, without attaching it, and a maintainer will say how to send it\n";

    // The help of SQLSRC293 and SQLSRC294.
    public const string PrivateRoute =
        "    help: make a raw bundle with --no-obfuscate and keep it, then open an issue on GitHub that says a raw "
        + "bundle is ready, without attaching it: a maintainer will say how to send it\n";

    public const string Reproduced = "sqlsource: reproduced: the pipeline gives this answer\n";

    // The file the query of a case is put in.
    public const string File = "Users.sql";

    public DiagnoseScene(string engine = PostgresRenamings.Engine)
    {
        Engine = engine;
        Run.Describers.Add(engine == PostgresRenamings.Engine ? new PostgresDescriber() : new SqlServerDescriber());
        Project = new TestProject(Run.Folder, "App", directory: "");
        Project.Settings["dialect"] = engine;
        _ = Project.AddSource("Queries.cs", Plans.Type);
        _ = Project.AnsweredBy(Run.Processes);
        Run.Environment["SQLSOURCE_CONNECTION_" + engine.ToUpperInvariant()] =
            engine == PostgresRenamings.Engine ? Secrets.Connection : "Server=db;Password=hunter2";
    }

    public string Engine { get; }

    public CliRun Run { get; } = new();

    public TestProject Project { get; }

    // A case of the engine's fixtures.
    public FixtureCase Case(string name) => FixtureCases.Of(Engine).Single(found => found.Name == name);

    // Puts the case's query in the project's file, or the text given, and answers every call from its capture.
    public string AddCase(FixtureCase fixture, string? fileText = null)
    {
        AnswerWith(CaptureJson.Read(fixture.Capture).Value.ShouldNotBeNull());
        return Project.AddSql(File, fileText ?? fixture.Query);
    }

    // Answers every call of the run from the capture.
    public void AnswerWith(DescribeCapture capture) => Run.Exchanges = _ => new ReplayingExchange(capture);

    public Task<CliResult> DiagnoseAsync(params string[] args) => Run.RunAsync(["diagnose", .. args]);

    public string PathOf(string relativePath) => Run.Folder.PathOf(relativePath);

    // The lines of a renamed bundle that was written at the path, and its mapping beside it.
    public string Written(string relativePath)
    {
        var zip = PathOf(relativePath);
        return $"sqlsource: bundle '{zip}' written\n"
            + $"sqlsource: mapping '{zip}{BundleFiles.MappingSuffix}' written\n"
            + PrivateLine;
    }

    // The lines of a raw bundle that was written at the path.
    public string WrittenRaw(string relativePath) =>
        $"sqlsource: raw bundle '{PathOf(relativePath)}' written\n" + RawLine;

    public void Dispose() => Run.Dispose();
}
