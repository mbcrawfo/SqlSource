using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Testing;

// What an engine gives the "diagnose" tests: the dialect of the project, a describer, the connection to the database
// "fixtures" and the SQL of the query, which joins two of its tables and holds literals, as the query FindQuokkaOrders
// of a file Orders.sql.  ShouldLeakNothingAsync checks that nothing of the schema's names and literals, nor of the
// query's, is in the renamed bundle; it is given the commands that made the bundle, to make a raw one of the same
// query.
internal sealed record DiagnoseEngine(
    string Dialect,
    Func<IQueryDescriber> Describer,
    string Connection,
    string Sql,
    Func<DiagnoseCommands, Bundle, Task> ShouldLeakNothingAsync
);

// The whole "diagnose" command in process, as Cli.Run runs it, with the describer of the engine against its container:
// one project in the run's folder whose settings file names the database "app", and a connection for "app" on the
// command line.  "replay" then runs the bundle with no database at all.
internal sealed class DiagnoseCommands : IDisposable
{
    // What replay prints of a bundle that gives the answer it expects.
    public const string Reproduced = "sqlsource: reproduced: the pipeline gives this answer\n";

    public const string Query = "FindQuokkaOrders";

    private const string Database = "app";
    private const string QueryFile = "Orders.sql";

    private readonly DiagnoseEngine _engine;
    private readonly CliRun _run = new();

    public DiagnoseCommands(DiagnoseEngine engine)
    {
        _engine = engine;
        _run.Describers.Add(engine.Describer());
        var project = Plans.Postgres(_run.Folder, directory: "").AnsweredBy(_run.Processes);
        project.Settings["database"] = Database;
        project.Settings["dialect"] = engine.Dialect;
        _ = project.AddSql(QueryFile, engine.Sql);
    }

    public void Dispose() => _run.Dispose();

    // Runs "diagnose" on the query with the connection and the arguments given.
    public Task<CliResult> DiagnoseAsync(params string[] args) =>
        _run.RunAsync([
            "diagnose",
            "--query",
            QueryFile + ":" + Query,
            "--connection",
            Database + "=" + _engine.Connection,
            .. args,
        ]);

    // The bundle at the path of the run's folder, read as replay reads it.
    public Bundle ReadBundle(string relativePath) => WrittenBundles.Read(_run.Folder.PathOf(relativePath));

    // Diagnoses the query, finds the bundle renamed and its mapping beside it, with nothing of the schema in it, and
    // replays it to the answer it expects.
    public async Task ShouldLeakNothingAndReproduceAsync()
    {
        var result = await DiagnoseAsync("--out", "report.zip");

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldContain("bundle '" + _run.Folder.PathOf("report.zip") + "' written");
        var zip = _run.Folder.PathOf("report.zip");
        File.Exists(zip + BundleFiles.MappingSuffix).ShouldBeTrue();
        await using (var archive = await ZipFile.OpenReadAsync(zip, TestContext.Current.CancellationToken))
        {
            archive.Entries.ShouldNotBeEmpty();
            archive.Entries.ShouldNotContain(static entry =>
                entry.FullName.EndsWith(".mapping.json", StringComparison.Ordinal)
            );
        }

        var bundle = ReadBundle("report.zip");
        bundle.Info.ShouldNotBeNull().Renamed.ShouldBeTrue();
        await _engine.ShouldLeakNothingAsync(this, bundle);

        var replayed = await _run.RunAsync("replay", "report.zip");

        replayed.ExitCode.ShouldBe(0, replayed.Error);
        replayed.Out.ShouldEndWith(Reproduced);
    }

    // Diagnoses the query with no renaming, scrubs the raw bundle, and replays the clean one to the answer it expects.
    public async Task ShouldReproduceAfterNoObfuscateThenScrubAsync()
    {
        var made = await DiagnoseAsync("--out", "raw.zip", "--no-obfuscate");

        made.ExitCode.ShouldBe(0, made.Error);
        File.Exists(_run.Folder.PathOf("raw.zip" + BundleFiles.MappingSuffix)).ShouldBeFalse();
        var raw = ReadBundle("raw.zip");
        raw.Info.ShouldNotBeNull().Renamed.ShouldBeFalse();
        raw.Query.ShouldContain(Query);

        var scrubbed = await _run.RunAsync("diagnose", "--scrub", "raw.zip", "--out", "clean.zip");

        scrubbed.ExitCode.ShouldBe(0, scrubbed.Error);
        File.Exists(_run.Folder.PathOf("clean.zip" + BundleFiles.MappingSuffix)).ShouldBeTrue();
        var clean = ReadBundle("clean.zip");
        clean.Info.ShouldNotBeNull().Renamed.ShouldBeTrue();
        clean.Query.ShouldNotContain(Query);
        await _engine.ShouldLeakNothingAsync(this, clean);

        var replayed = await _run.RunAsync("replay", "clean.zip");

        replayed.ExitCode.ShouldBe(0, replayed.Error);
        replayed.Out.ShouldEndWith(Reproduced);
    }
}
