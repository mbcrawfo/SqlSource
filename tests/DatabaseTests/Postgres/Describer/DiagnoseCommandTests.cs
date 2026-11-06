using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Tool;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// The whole "diagnose" command in process, as Cli.Run runs it, with the PostgreSQL describer against the container: one
// postgres project in the run's folder whose settings file names the database "app", and a connection for "app" on the
// command line to the container's database "fixtures".  The query joins two of its tables and holds literals, so that
// the bundle has names and literals to rename.  "replay" then runs the bundle with no database at all.
[Collection(UsesPostgres.Name)]
public sealed class DiagnoseCommandTests : IDisposable
{
    private const string Database = "app";
    private const string QueryFile = "Orders.sql";
    private const string Query = "FindQuokkaOrders";

    private const string Reproduced = "sqlsource: reproduced: the pipeline gives this answer\n";

    private const string Sql =
        "-- name: "
        + Query
        + "\n"
        + "SELECT u.name, o.total, 'zebra-crossing' AS tag\n"
        + "FROM users u JOIN orders o ON o.user_id = u.id\n"
        + "WHERE u.name = 'Quokka Marsupial';\n";

    private readonly PostgresDatabases _postgres;
    private readonly CliRun _run = new();

    public DiagnoseCommandTests(PostgresDatabases postgres)
    {
        _postgres = postgres;
        _run.Describers.Add(new PostgresDescriber());
        var project = Plans.Postgres(_run.Folder, directory: "").AnsweredBy(_run.Processes);
        project.Settings["database"] = Database;
        _ = project.AddSql(QueryFile, Sql);
    }

    public void Dispose() => _run.Dispose();

    private Task<CliResult> DiagnoseAsync(params string[] args) =>
        _run.RunAsync([
            "diagnose",
            "--query",
            QueryFile + ":" + Query,
            "--connection",
            Database + "=" + _postgres.ConnectionString,
            .. args,
        ]);

    // The bundle at the path of the run's folder, read as replay reads it.
    private Bundle ReadBundle(string relativePath)
    {
        var read = BundleReader.Read(_run.Folder.PathOf(relativePath), ToolHost.OpenRead, ToolHost.ReadText);
        read.Failure.ShouldBeNull();
        return read.Value.ShouldNotBeNull();
    }

    private static async Task ShouldLeakNothingAsync(Bundle renamed)
    {
        var schema = await File.ReadAllTextAsync(
            DescriberFixtures.Postgres.Schema,
            TestContext.Current.CancellationToken
        );
        BundleLeaks.Of(renamed, [schema, Sql], PostgresWords.IsEngineWord).ShouldBeEmpty();
    }

    [Fact]
    public async Task Diagnose_AQueryOfApp_LeaksNoNameOfTheSchemaAndReproduces()
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
        await ShouldLeakNothingAsync(bundle);

        var replayed = await _run.RunAsync("replay", "report.zip");

        replayed.ExitCode.ShouldBe(0, replayed.Error);
        replayed.Out.ShouldEndWith(Reproduced);
    }

    [Fact]
    public async Task Diagnose_NoObfuscateThenScrub_Reproduces()
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
        await ShouldLeakNothingAsync(clean);

        var replayed = await _run.RunAsync("replay", "clean.zip");

        replayed.ExitCode.ShouldBe(0, replayed.Error);
        replayed.Out.ShouldEndWith(Reproduced);
    }
}
