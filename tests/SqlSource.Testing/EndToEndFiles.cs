using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Testing;

// The files of the end-to-end databases "app" and "billing", in the repository: a folder for each in the engine's
// folder, with its schema.sql and its seed.sql, which tools/describe-end-to-end.sh applies in the same order.  Every
// database of the test projects of both engines is built through it, over a DbDataSource of the project's own, so that
// it needs no driver: "app" and "billing", and from the files its caller names, "fixtures" and "realistic" of each
// engine.
internal static partial class EndToEndFiles
{
    public static string Schema(EndToEndEngine engine, string database) =>
        Path.Combine(engine.Folder, database, "schema.sql");

    public static string Seed(EndToEndEngine engine, string database) =>
        Path.Combine(engine.Folder, database, "seed.sql");

    // Reads the file and runs each of its batches as one command, in order, on one connection of the source, as
    // sqlcmd runs a script in one session.
    public static async Task RunAsync(
        DbDataSource source,
        string path,
        EndToEndEngine engine,
        CancellationToken cancellationToken
    )
    {
        var batches = engine.Batches(await File.ReadAllTextAsync(path, cancellationToken));
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        foreach (var batch in batches)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    // Creates the database on the server, then runs each file in order on a source of the database, which it
    // disposes.
    public static async Task CreateAsync(
        DbDataSource server,
        EndToEndEngine engine,
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
            await RunAsync(created, path, engine, cancellationToken);
        }
    }

    // Creates the end-to-end database of that name from its schema and its seed, in that order.
    public static Task CreateAsync(
        DbDataSource server,
        EndToEndEngine engine,
        string database,
        Func<string, DbDataSource> sourceOf,
        CancellationToken cancellationToken
    ) =>
        CreateAsync(
            server,
            engine,
            database,
            sourceOf,
            [Schema(engine, database), Seed(engine, database)],
            cancellationToken
        );

    // The batches of a script of SQL Server in order, as sqlcmd splits it: a batch ends at a line that holds GO alone,
    // ignoring case and the white space around it.  Each batch keeps its line endings, and none holds only white
    // space.  GO with a count, which sqlcmd runs that many times, is refused rather than run once.  The split reads
    // lines and not SQL, so a line of GO alone inside a block comment ends a batch, as it does for sqlcmd.
    public static IReadOnlyList<string> SqlServerBatches(string script)
    {
        var batches = new List<string>();
        var batch = new StringBuilder();
        var number = 0;
        foreach (var line in Lines(script))
        {
            number++;
            var text = line.Trim();
            if (text.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                Add(batches, batch);
                continue;
            }

            if (GoWithACount().IsMatch(text))
            {
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Line {number} of the script is GO with a count, and a count is not supported."
                    )
                );
            }

            _ = batch.Append(line);
        }

        Add(batches, batch);
        return batches;
    }

    // The lines of the script, each with the line feed that ends it.
    private static IEnumerable<string> Lines(string script)
    {
        var start = 0;
        while (start < script.Length)
        {
            var end = script.IndexOf('\n', start);
            var next = end < 0 ? script.Length : end + 1;
            yield return script[start..next];
            start = next;
        }
    }

    private static void Add(List<string> batches, StringBuilder batch)
    {
        var text = batch.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            batches.Add(text);
        }

        _ = batch.Clear();
    }

    [GeneratedRegex(@"^GO\s+\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoWithACount();
}
