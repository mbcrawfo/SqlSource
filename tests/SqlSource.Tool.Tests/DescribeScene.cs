using System;
using System.Threading.Tasks;
using SqlSource.Tool.Testing;

namespace SqlSource.Tool.Tests;

// A run of "describe" from the command line to the disk: one postgres project in the working directory, whose type
// claims the .sql files beside it, and the describer of the tests.  The databases "postgres" and "billing" have a
// connection, each in its own variable, until a test takes one away.
internal sealed class DescribeScene : IDisposable
{
    // What ends a "describe" that reported an error and wrote no log.
    public const string RunAgain = "sqlsource: run again with --log <file> for a record of what the run did\n";

    // A .env line that gives the database "billing" a connection.
    public const string BillingFromTheFile = "SQLSOURCE_CONNECTION_BILLING=Host=from-the-file\n";

    // The project is in the working directory, or in the folder below it that is given.
    public DescribeScene(string directory = "")
    {
        Run.Time = Clock;
        Run.Describers.Add(Describer);
        Project = Plans.Postgres(Run.Folder, directory: directory).AnsweredBy(Run.Processes);
        Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
    }

    public TestClock Clock { get; } = new();

    public CliRun Run { get; } = new();

    public FakeDescriber Describer { get; } = new();

    public TestProject Project { get; }

    // Where the run looks for its .env file.
    public string DotEnvPath => Run.Folder.PathOf(".env");

    public Task<CliResult> DescribeAsync(params string[] args) => Run.RunAsync(["describe", .. args]);

    // Writes the .env file of the run and returns its path.
    public string WriteDotEnv(string text) => Run.Folder.WriteFile(".env", text);

    // Writes the manifest of the project, null for the one the project describes.  It is in a folder of its own, so
    // that the manifest's folder is not the project's.
    public string WriteManifest(string? text = null) =>
        Run.Folder.WriteFile("obj/App.csproj.manifest", text ?? Project.ManifestText());

    public void Dispose() => Run.Dispose();
}
