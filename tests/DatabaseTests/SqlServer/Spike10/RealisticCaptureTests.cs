using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SqlSource.DatabaseTests.SqlServer.Realistic;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Spike10;

// The phase 10.0 spike: describes each case of the realistic corpus on the default image through a capturing exchange
// and writes it as a fixture folder, $SPIKE10_OUT/spike10/cases/realistic-mssql/mssql/<case>/, for the spike's replay
// tests.
// Run with SPIKE10_OUT=<folder> dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class
// "SqlSource.DatabaseTests.SqlServer.Spike10.RealisticCaptureTests" --explicit only
[Collection(UsesSqlServer.Name)]
public sealed class RealisticCaptureTests(SqlServerDatabases databases)
{
    private const string Engine = "mssql";

    [Fact(Explicit = true)]
    public async Task Capture_EveryCase_WritesAFixtureFolder()
    {
        var token = TestContext.Current.CancellationToken;
        var root = Environment.GetEnvironmentVariable("SPIKE10_OUT") is { Length: > 0 } variable
            ? variable
            : throw new InvalidOperationException("SPIKE10_OUT is not set.");
        var engineFolder = Path.Combine(root, "spike10", "cases", "realistic-" + Engine, Engine);
        if (Directory.Exists(engineFolder))
        {
            Directory.Delete(engineFolder, recursive: true);
        }

        _ = Directory.CreateDirectory(engineFolder);
        var results = Path.Combine(root, "spike10", "results");
        _ = Directory.CreateDirectory(results);

        var skipped = new StringBuilder("case\twhy\n");
        var written = 0;
        foreach (var found in RealisticCases.Corpus.All)
        {
            if (!found.RunsOn(databases.Major))
            {
                _ = skipped
                    .Append(found.Name)
                    .Append("\tneeds a later major than ")
                    .Append(databases.Major)
                    .Append('\n');
                continue;
            }

            var fixture = found.AsFixture;
            var capture = new DescribeCapture(Engine);
            string expected;
            try
            {
                expected = await FixtureReplay.DescribeThroughAsync(
                    fixture,
                    static _ => new SqlServerDescriber(),
                    databases.RealisticConnectionString,
                    new CapturingExchange(LiveExchange.Instance, capture)
                );
            }
            catch (InvalidOperationException exception)
            {
                _ = skipped.Append(found.Name).Append('\t').Append(OneLine(exception.Message)).Append('\n');
                continue;
            }

            var folder = Path.Combine(engineFolder, found.Name);
            _ = Directory.CreateDirectory(folder);
            File.Copy(fixture.QueryPath, Path.Combine(folder, FixtureCase.QueryFile));
            await File.WriteAllTextAsync(
                Path.Combine(folder, FixtureCase.CaptureFile),
                CaptureJson.Write(capture),
                token
            );
            await File.WriteAllTextAsync(Path.Combine(folder, FixtureCase.ExpectedFile), expected, token);
            written++;
        }

        await File.WriteAllTextAsync(
            Path.Combine(results, "t1-realistic-mssql-skipped.tsv"),
            skipped.ToString(),
            token
        );
        await File.WriteAllTextAsync(
            Path.Combine(results, "t1-realistic-mssql-count.txt"),
            $"{written} written of {RealisticCases.Corpus.All.Count}\n",
            token
        );
        Assert.True(written > 0, "no case was written");
    }

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Replace('\t', ' ');
}
