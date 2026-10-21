using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Testing;

// The files of the end-to-end databases "app" and "billing", in the repository: a folder for each under
// tests/SqlSource.Tests.Postgres/EndToEnd/, with its schema.sql and its seed.sql, which tools/describe-end-to-end.sh
// applies in the same order.  The databases of both PostgreSQL test projects are built through it, over a
// DbDataSource of the project's own, so that it needs no driver.
internal static class EndToEndFiles
{
    public static string Folder { get; } =
        Path.Combine(RepositoryRoot.Folder, "tests", "SqlSource.Tests.Postgres", "EndToEnd");

    public static string Schema(string database) => Path.Combine(Folder, database, "schema.sql");

    public static string Seed(string database) => Path.Combine(Folder, database, "seed.sql");

    // Reads the file and runs it as one command.
    public static async Task RunAsync(DbDataSource source, string path, CancellationToken cancellationToken)
    {
        await using var command = source.CreateCommand(await File.ReadAllTextAsync(path, cancellationToken));
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Creates the database on the server, then runs each file in order on a source of the database, which it
    // disposes.
    public static async Task CreateAsync(
        DbDataSource server,
        string database,
        Func<string, DbDataSource> sourceOf,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken
    )
    {
        await using (var create = server.CreateCommand($"CREATE DATABASE {database}"))
        {
            _ = await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var created = sourceOf(database);
        foreach (var path in files)
        {
            await RunAsync(created, path, cancellationToken);
        }
    }

    // Creates the end-to-end database of that name from its schema and its seed, in that order.
    public static Task CreateAsync(
        DbDataSource server,
        string database,
        Func<string, DbDataSource> sourceOf,
        CancellationToken cancellationToken
    ) => CreateAsync(server, database, sourceOf, [Schema(database), Seed(database)], cancellationToken);
}
