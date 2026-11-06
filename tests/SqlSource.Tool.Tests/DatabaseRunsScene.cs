using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// Step 3 of the run, for the classes named DatabaseRuns*: one postgres project whose Users.sql has a query for the
// database "postgres" and one for "billing", the describer of the tests behind a capturing exchange, and the writer
// that the run reports its errors to.
internal sealed class DatabaseRunsScene : IDisposable
{
    public const string Users =
        "-- name: GetUser\nSELECT 1 WHERE @id = 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    public DatabaseRunsScene()
    {
        Exchange = new CapturingExchange(LiveExchange.Instance, Capture);
        Project = Plans.Postgres(Folder);
        UsersPath = Project.AddSql("Users.sql", Users);
    }

    public TempFolder Folder { get; } = new();

    public FakeDescriber Describer { get; } = new();

    public DescribeCapture Capture { get; } = new(SidecarValues.Engine.Postgres);

    public CapturingExchange Exchange { get; }

    public Dictionary<string, string> Environment { get; } = [];

    public StringWriter Error { get; } = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public TestProject Project { get; }

    public string UsersPath { get; }

    // The files of the last run.
    public ImmutableArray<FileWork> Files { get; private set; }

    // The reporter of the last run, which writes to Error.
    public Reporter? Reporter { get; private set; }

    // Decides for the plan and runs its databases.  Both databases have a connection unless "given" says otherwise.
    // The value at index i of "given" stands at position (2 * i) + 3, as if each followed "describe --connection".
    // A capture is given to the run as diagnose gives one.
    public async Task<ImmutableArray<FileWork>> RunAsync(
        RunPlan? plan = null,
        bool registered = true,
        string[]? given = null,
        CancellationToken? cancellationToken = null,
        IQueryDescriber? describer = null,
        RunCapture? capture = null
    )
    {
        plan ??= Plans.Of(Project);
        var files = Files = RunDecisions.Decide(plan, RunMode.Describe, []);
        var selected = RunDecisions.SelectedDatabases(plan);
        var connections = Connections.Resolve(
            [.. selected.Select(database => database.Name)],
            [.. RunDecisions.DescribableDatabases(plan).Select(database => database.Name)],
            [.. plan.Databases.Select(database => database.Name)],
            [
                .. (
                    given ?? ["postgres=" + Secrets.ConnectionWithWord, "billing=" + Secrets.ConnectionWithWord]
                ).Select((text, index) => ConnectionArguments.Parse(text, (2 * index) + 3)),
            ],
            ConnectionEnvironment.Of(name => Environment.GetValueOrDefault(name), NoDotEnv, Folder.Path)
        );
        var host = Hosts.Create(Folder.Path, new FakeProcessRunner(), Folder.Path, environment: Environment) with
        {
            Describers = new DescriberRegistry(registered ? [describer ?? Describer] : []),
            Exchange = _ => Exchange,
        };

        Reporter = new Reporter(Error);
        await DatabaseRuns.RunAsync(
            selected,
            files,
            connections,
            host,
            Reporter,
            cancellationToken ?? TestContext.Current.CancellationToken,
            capture: capture
        );
        return files;
    }

    public static QueryState[] States(ImmutableArray<FileWork> files) =>
        [.. files.SelectMany(file => file.Queries).Select(query => query.State)];

    // The run of a part has no .env file.
    private static readonly DotEnvFile NoDotEnv = new(null, [], []);

    public void Dispose()
    {
        Error.Dispose();
        Folder.Dispose();
    }
}
