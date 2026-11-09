using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Renaming;

// The vocabulary of the renaming against the server: every system type and every documented function that the image
// knows must be a word of SqlServerVocabulary.  SQL Server has no catalog of its intrinsic functions, so each
// documented name is tried as "SELECT F()" with sys.dm_exec_describe_first_result_set: error 195 means the server does
// not know the name as a scalar function, and is then tried as "SELECT * FROM F()", where any answer but 208 means it
// knows a rowset function of the name.  Run on the floor image and on the default one.
[Collection(UsesSqlServer.Name)]
public sealed class VocabularyTests(SqlServerDatabases databases)
{
    private const string TypesSql = "SELECT name FROM sys.types WHERE is_user_defined = 0 ORDER BY name";

    private const string ProbeSql =
        "SELECT TOP (1) error_number FROM sys.dm_exec_describe_first_result_set(@tsql, NULL, 0)";

    private const int NotARecognizedFunction = 195;

    private const int InvalidObjectName = 208;

    [Fact]
    public async Task SystemTypes_OfTheImage_AreAllInTheVocabulary()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = databases.SourceOf(SqlServerDatabases.Database);
        await using var opened = (SqlConnection)await source.OpenConnectionAsync(token);
        var missing = new List<string>();
        await using (var command = new SqlCommand(TypesSql, opened))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                var name = reader.GetString(0);
                if (!SqlServerVocabulary.Contains(name))
                {
                    missing.Add(name);
                }
            }
        }

        missing.ShouldBeEmpty("Missing types: " + string.Join(", ", missing.Select(Quote)));
    }

    [Fact]
    public async Task DocumentedFunctions_TheImageKnows_AreAllInTheVocabulary()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = databases.SourceOf(SqlServerDatabases.Database);
        await using var opened = (SqlConnection)await source.OpenConnectionAsync(token);
        var missing = new List<string>();
        var known = 0;
        foreach (var name in DocumentedFunctions.Names)
        {
            if (!await KnowsAsync(opened, name, token))
            {
                continue;
            }

            known++;
            if (!SqlServerVocabulary.Contains(name))
            {
                missing.Add(name);
            }
        }

        known.ShouldBeGreaterThan(0);
        missing.ShouldBeEmpty("Missing functions: " + string.Join(", ", missing.Select(Quote)));
    }

    private static string Quote(string name) => "\"" + name + "\"";

    private static async Task<bool> KnowsAsync(SqlConnection opened, string name, CancellationToken token)
    {
        if (await ErrorOfAsync(opened, "SELECT " + name + "()", token) != NotARecognizedFunction)
        {
            return true;
        }

        return await ErrorOfAsync(opened, "SELECT * FROM " + name + "()", token) != InvalidObjectName;
    }

    // The error that the server gives the statement, or 0 when it gives none.
    private static async Task<int> ErrorOfAsync(SqlConnection opened, string tsql, CancellationToken token)
    {
        await using var command = new SqlCommand(ProbeSql, opened);
        _ = command.Parameters.AddWithValue("@tsql", tsql);
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
