using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Spike10;

// The phase 10.0 spike, finding 3: the vocabulary SQL Server holds.  It has no catalog of its keywords or of its
// intrinsic functions, so the documented function names (sources/mssql-functions.tsv, from Microsoft's table of
// contents)
// are each tried as "SELECT F()" with sys.dm_exec_describe_first_result_set: error 195 means the server does not know
// the name.  Run on the floor image and on the default one:
// SPIKE10_OUT=<folder> dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class
// "SqlSource.DatabaseTests.SqlServer.Spike10.VocabularyTests" --explicit only
[Collection(UsesSqlServer.Name)]
public sealed class VocabularyTests(SqlServerDatabases databases)
{
    private const string TypesSql = "SELECT name FROM sys.types WHERE is_user_defined = 0 ORDER BY name";

    private const string ProbeSql =
        "SELECT TOP (1) error_number FROM sys.dm_exec_describe_first_result_set(@tsql, NULL, 0)";

    private const int NotARecognizedFunction = 195;

    [Fact(Explicit = true)]
    public async Task Dump_Vocabulary_WritesTheLists()
    {
        var token = TestContext.Current.CancellationToken;
        var root = Environment.GetEnvironmentVariable("SPIKE10_OUT") is { Length: > 0 } variable
            ? variable
            : throw new InvalidOperationException("SPIKE10_OUT is not set.");
        var results = Path.Combine(root, "spike10", "results");
        _ = Directory.CreateDirectory(results);
        var image = databases.Major switch
        {
            14 => "2017",
            17 => "default",
            _ => "major" + databases.Major.ToString(CultureInfo.InvariantCulture),
        };
        var names = (
            await File.ReadAllLinesAsync(Path.Combine(root, "spike10", "sources", "mssql-functions.tsv"), token)
        )
            .Skip(1)
            .Select(static line => line.Split('\t')[0])
            .Where(static name => name.Length > 0)
            .ToList();

        await using var source = databases.SourceOf(SqlServerDatabases.Database);
        await using var opened = (SqlConnection)await source.OpenConnectionAsync(token);

        var catalog = new StringBuilder("kind\tname\n");
        await using (var command = new SqlCommand(TypesSql, opened))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                _ = catalog.Append("type\t").Append(reader.GetString(0)).Append('\n');
            }
        }

        var unknown = new StringBuilder("name\terror\n");
        var known = 0;
        foreach (var name in names)
        {
            var error = await ProbeAsync(opened, name, token);
            if (error == NotARecognizedFunction)
            {
                _ = unknown.Append(name).Append('\t').Append(error).Append('\n');
                continue;
            }

            known++;
            _ = catalog.Append("function\t").Append(name).Append('\n');
        }

        await File.WriteAllTextAsync(Path.Combine(results, $"t4-mssql-{image}-catalog.tsv"), catalog.ToString(), token);
        await File.WriteAllTextAsync(Path.Combine(results, $"t4-mssql-{image}-unknown.tsv"), unknown.ToString(), token);
        Assert.True(known > 0);
    }

    // The error the server gives "SELECT F()", or 0 when it gives none.
    private static async Task<int> ProbeAsync(
        SqlConnection opened,
        string name,
        System.Threading.CancellationToken token
    )
    {
        await using var command = new SqlCommand(ProbeSql, opened);
        _ = command.Parameters.AddWithValue("@tsql", "SELECT " + name + "()");
        try
        {
            return await command.ExecuteScalarAsync(token) is int number ? number : 0;
        }
        catch (SqlException exception)
        {
            return exception.Number;
        }
    }
}
