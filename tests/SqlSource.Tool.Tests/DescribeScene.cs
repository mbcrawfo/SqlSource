using System;
using System.Threading.Tasks;

namespace SqlSource.Tool.Tests;

// A run of "describe" from the command line to the disk: one postgres project in the working directory, whose type
// claims the .sql files beside it, and the describer of the tests.  The databases "postgres" and "billing" have a
// connection, each in its own variable, until a test takes one away.
internal sealed class DescribeScene : IDisposable
{
    // What ends a "describe" that reported an error and wrote no log.
    public const string RunAgain = "sqlsource: run again with --log <file> for a record of what the run did\n";

    public DescribeScene()
    {
        Run.Time = Clock;
        Run.Describers.Add(Describer);
        Project = Plans.Postgres(Run.Folder, directory: "").AnsweredBy(Run.Processes);
        Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
    }

    public TestClock Clock { get; } = new();

    public CliRun Run { get; } = new();

    public FakeDescriber Describer { get; } = new();

    public TestProject Project { get; }

    public Task<CliResult> DescribeAsync(params string[] args) => Run.RunAsync(["describe", .. args]);

    public void Dispose() => Run.Dispose();
}
