using System;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.DatabaseTests.SqlServer;

// The database "user_clr", which holds a CLR type of a user's own assembly: dbo.Grade, of the net48 build of
// tests/DatabaseTests/SqlServer.ClrType, whose net10.0 build this project references.  Every image's server runs it
// once CLR is enabled and the assembly is trusted by its hash, since clr strict security is on from 2017.
internal static class UserClrDatabase
{
    public const string Name = "user_clr";

    // The type's assembly, as CREATE ASSEMBLY names it: the name of its manifest.
    private const string Assembly = "SqlSource.DatabaseTests.SqlServer.ClrType";

    // The type, then the table and the view the tests read, each a batch of its own, since a batch is compiled before
    // it runs: grade is NOT NULL, maybe is NULL, and the view passes both through.
    private static readonly string[] Schema =
    [
        "CREATE TYPE dbo.Grade EXTERNAL NAME [SqlSource.DatabaseTests.SqlServer.ClrType]"
            + ".[SqlSource.DatabaseTests.SqlServer.ClrType.Grade];",
        "CREATE TABLE dbo.grades (id int NOT NULL PRIMARY KEY, grade dbo.Grade NOT NULL, maybe dbo.Grade NULL);",
        "INSERT INTO dbo.grades (id, grade, maybe) VALUES (1, N'5', NULL), (2, N'7', N'8');",
        "CREATE VIEW dbo.grade_view AS SELECT id, grade, maybe FROM dbo.grades;",
    ];

    // The net48 build, which the project embeds as the resource ServerClrType.dll.
    public static byte[] AssemblyBytes()
    {
        using var stream =
            typeof(UserClrDatabase).Assembly.GetManifestResourceStream("ServerClrType.dll")
            ?? throw new InvalidOperationException("The project embeds no ServerClrType.dll.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    // Enables CLR on the server, trusts the assembly, and makes the database with the type, its table and its view.
    public static async Task CreateAsync(
        DbDataSource server,
        Func<string, DbDataSource> sourceOf,
        CancellationToken token
    )
    {
        var bits = AssemblyBytes();
        await ExecuteAsync(
            server,
            """
            EXEC sys.sp_configure N'show advanced options', 1;
            RECONFIGURE;
            EXEC sys.sp_configure N'clr enabled', 1;
            RECONFIGURE;
            """,
            token
        );
        await ExecuteAsync(
            server,
            """
            DECLARE @hash varbinary(64) = HASHBYTES('SHA2_512', @bits);
            IF NOT EXISTS (SELECT 1 FROM sys.trusted_assemblies WHERE hash = @hash)
                EXEC sys.sp_add_trusted_assembly @hash, @description;
            """,
            token,
            new SqlParameter("@bits", bits),
            new SqlParameter("@description", Assembly)
        );
        await ExecuteAsync(server, $"CREATE DATABASE [{Name}];", token);

        await using var database = sourceOf(Name);
        await ExecuteAsync(
            database,
            $"CREATE ASSEMBLY [{Assembly}] FROM @bits WITH PERMISSION_SET = SAFE;",
            token,
            new SqlParameter("@bits", bits)
        );
        foreach (var batch in Schema)
        {
            await ExecuteAsync(database, batch, token);
        }
    }

    // A statement of the harness's own, with its parameters.  The statement is never a user's.
    private static async Task ExecuteAsync(
        DbDataSource source,
        string sql,
        CancellationToken token,
        params SqlParameter[] parameters
    )
    {
        await using var command = source.CreateCommand(sql);
        command.Parameters.AddRange(parameters);
        _ = await command.ExecuteNonQueryAsync(token);
    }
}
