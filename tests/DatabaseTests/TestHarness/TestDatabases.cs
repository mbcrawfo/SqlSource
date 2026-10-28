using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.TestHarness;

// What the two driver-free containers share: the connection strings of their databases, the statements of a test's own
// and the build of the databases of a project.  Nothing here names a driver; a DbDataSource and a
// DbConnectionStringBuilder are all it needs.
internal static class TestDatabases
{
    // The template with its Database replaced: DbConnectionStringBuilder, which both drivers read, does not know
    // SqlClient's Initial Catalog as a synonym of Database, so that entry goes too and the string keeps one for it.
    public static string ConnectionStringOf(string template, string database)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = template };
        _ = builder.Remove("Initial Catalog");
        builder["Database"] = database;
        return builder.ConnectionString;
    }

    // The first database's name, which is the container's default one.
    public static string DefaultOf(
        IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> databases,
        Type container
    ) =>
        databases.Count > 0
            ? databases[0].Name
            : throw new InvalidOperationException($"{container.Name} names no database, and a container needs one.");

    // Runs one statement of a test's own.  The command is made by the source, which CA2100 does not report; the
    // statement is a test's, never a user's.
    public static async Task ExecuteAsync(DbDataSource source, string sql, CancellationToken token)
    {
        await using var command = source.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(token);
    }

    // One value from the server, for a statement of a test's own.
    public static async Task<object?> ScalarAsync(DbDataSource source, string sql, CancellationToken token)
    {
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(token);
    }

    // Creates each database and runs its scripts, in order, so that the databases are built in the order that
    // tools/describe-end-to-end.sh applies them.
    public static async Task BuildAsync(
        DbDataSource server,
        EndToEndEngine engine,
        IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> databases,
        Func<string, DbDataSource> sourceOf,
        CancellationToken token
    )
    {
        foreach (var (name, scripts) in databases)
        {
            await EndToEndFiles.CreateAsync(server, engine, name, sourceOf, scripts, token);
        }
    }
}
