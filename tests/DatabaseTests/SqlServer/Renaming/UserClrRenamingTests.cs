using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Renaming;

// A column and a parameter of a CLR type of a user's own assembly, captured on the server as diagnose captures them,
// then renamed by SQL Server's part.  The entry names such a type schema.name by its user type, which the renaming
// makes again from the renamed schema and name, so the proof replays the renamed bundle to its renamed entry whatever
// the names hold: a space, a ] or a dot.  Each type is in a database of its own, from the assembly of user_clr, since
// the server registers a CLR class once in a database.  Run on the floor image and on the default one.
[Collection(UsesSqlServer.Name)]
public sealed class UserClrRenamingTests(SqlServerDatabases sqlServer)
{
    private const string Database = "user_clr_renaming";

    private static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer);

    [Theory]
    [InlineData("dbo", "Grade")]
    [InlineData("odd schema", "Odd ]Grade")]
    [InlineData("dbo", "a.Grade")]
    public async Task RenameAsync_UserClrType_ReplaysToItsRenamedEntry(string schema, string name)
    {
        var type = Bracketed(schema) + "." + Bracketed(name);
        await CreateAsync(schema, type);
        try
        {
            using var folder = new TempFolder();
            _ = folder.WriteFile(
                FixtureCase.QueryFile,
                "-- name: GetGrades\nSELECT grade FROM dbo.grades WHERE grade = @grade;\n"
            );
            var raw = await RawBundles.CaptureAsync(
                new FixtureCase("mssql", "user-clr", folder.Path),
                static _ => new SqlServerDescriber(),
                sqlServer.ConnectionStringOf(Database),
                LiveExchange.Instance
            );
            raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Entry);

            var renamed = await BundleRenamings.RenamedAsync(
                raw,
                Part,
                new SqlServerDescriber(),
                BundleRenamings.Key(BundleRenamings.AnyKey)
            );

            var bundle = renamed.Bundle;
            Provenance.Of(bundle.Expected.ShouldNotBeNull()).ShouldBe(Provenance.Of(raw.Expected.ShouldNotBeNull()));
            bundle.Expected.ShouldNotContain(name, Case.Insensitive);
            SqlServerBundleLeaks.Find(bundle, Part, raw, raw.Query).ShouldBeEmpty();
        }
        finally
        {
            await sqlServer.ExecuteAsync(
                $"ALTER DATABASE [{Database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Database}];"
            );
        }
    }

    // The database with the assembly, the type of that schema and name, and a table of one row of it.
    private async Task CreateAsync(string schema, string type)
    {
        var token = TestContext.Current.CancellationToken;
        await sqlServer.ExecuteAsync($"CREATE DATABASE [{Database}];");
        await using var database = sqlServer.SourceOf(Database);
        await using (var assembly = database.CreateCommand(UserClrDatabase.CreateAssembly))
        {
            _ = assembly.Parameters.Add(new SqlParameter("@bits", UserClrDatabase.AssemblyBytes()));
            _ = await assembly.ExecuteNonQueryAsync(token);
        }

        string[] batches =
        [
            schema == "dbo" ? "" : $"CREATE SCHEMA {Bracketed(schema)};",
            $"CREATE TYPE {type} EXTERNAL NAME {UserClrDatabase.ExternalName};",
            $"CREATE TABLE dbo.grades (id int PRIMARY KEY, grade {type} NOT NULL);",
            "INSERT INTO dbo.grades (id, grade) VALUES (1, N'5');",
        ];
        foreach (var batch in batches)
        {
            if (batch.Length == 0)
            {
                continue;
            }

            await using var command = database.CreateCommand(batch);
            _ = await command.ExecuteNonQueryAsync(token);
        }
    }

    // A name in brackets, with a ] written twice.
    private static string Bracketed(string name) =>
        "[" + name.Replace("]", "]]", System.StringComparison.Ordinal) + "]";
}
