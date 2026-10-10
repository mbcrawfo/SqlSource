using System;
using System.Threading.Tasks;

namespace SqlSource.Tool.Tests;

// A run of "describe" from the command line to the disk: one postgres project in the working directory, whose type
// claims the .sql files beside it, and the describer of the tests.  The databases "postgres" and "billing" have a
// connection, each in its own variable, until a test takes one away.
internal sealed class DescribeScene : IDisposable
{
    public const string Connection = "Host=db;Password=hunter2";

    public const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    public DescribeScene()
    {
        Run.Describers.Add(Describer);
        Project = Plans.Postgres(Run.Folder, directory: "").AnsweredBy(Run.Processes);
        Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Connection;
        Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Connection;
    }

    public CliRun Run { get; } = new();

    public FakeDescriber Describer { get; } = new();

    public TestProject Project { get; }

    public Task<CliResult> DescribeAsync(params string[] args) => Run.RunAsync(["describe", .. args]);

    public void Dispose() => Run.Dispose();
}
