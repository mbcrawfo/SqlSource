using System;
using System.Linq;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;

namespace SqlSource.Testing.Generator;

// Hands the generator a sidecar beside a .sql file, and computes a query's hash as the generator does, so that a test
// writes a current entry without restating the algorithm.
//
// Of fails a test whose current entry, one whose hash is its query's under some dialect, does not list the query's
// parameters as the tool writes them: each as the SQL spells it, in the order of the SQL, numbered from 0.  It does not
// compare their types, which the database gives, and it never compares their nullability, by design: a hand-built
// entry may give a parameter a nullability that no marker gives, which the tool never writes, to test how the
// generator reads it.  A stale entry, one of a query the file lacks and a file that does not parse are not checked.  A
// test of a sidecar a person edited writes it with HandEdited.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class Sidecars
{
    // The hash the generator computes for the query with this name in the file, under this dialect.  A -- dialect:
    // marker in the file wins, as it does in a build.  A name the file lacks is a bug of the test, and so is a file
    // that does not parse.  The caller passes the dialect the generator will see for the file, that of the settings
    // files of its folder, since these helpers read no settings file.
    public static string HashOf(SqlFile sqlFile, string queryName, SqlDialect dialect = SqlDialect.Ansi) =>
        HashOf(sqlFile, queryName, dialect, out _);

    // An entry of a query with no rows and no parameters, against the database main of a server of version 18.0.
    public static SidecarEntry Entry(string name, string hash, string engine = "postgres") =>
        TestSidecars.Entry(name, hash, engine, "main", "18.0", SidecarResultKind.None);

    // The sidecar of the file, by default of the current format and the current tool.
    public static SqlFile Of(SqlFile sqlFile, params SidecarEntry[] entries) =>
        Of(sqlFile, SidecarFormat.Version, PackageVersion.Prefix, entries);

    public static SqlFile Of(SqlFile sqlFile, int formatVersion, string toolVersion, params SidecarEntry[] entries)
    {
        CheckParameters(sqlFile, entries);
        return Write(sqlFile, formatVersion, toolVersion, entries);
    }

    // The sidecar of the file as a person edited it, of the current format and the current tool: its entries are not
    // checked against their queries.  A test that uses it says in a comment why its entry differs.
    public static SqlFile HandEdited(SqlFile sqlFile, params SidecarEntry[] entries) =>
        Write(sqlFile, SidecarFormat.Version, PackageVersion.Prefix, entries);

    // A sidecar with a current entry for each of the queries, for the engine of the dialect the file is read by.  Each
    // entry lists its query's parameters as the tool writes them, with the nullability the SQL gives and no type.
    public static SqlFile Current(SqlFile sqlFile, SqlDialect dialect, params string[] queryNames)
    {
        var entries = queryNames
            .Select(name =>
            {
                var block = BlockOf(sqlFile, name, dialect, out var effective);
                var hash = SqlQueryHash.Compute(effective, block.Segments, block.Tokens, block.Parameters);
                var parameters = block
                    .Parameters.Select(
                        (parameter, ordinal) =>
                            TestSidecars.Parameter(ordinal, parameter.Name, null, parameter.Nullable)
                    )
                    .ToArray();
                return Entry(name, hash, SqlDialectName.Canonical(effective)) with
                {
                    Parameters = TestSidecars.Of(parameters),
                };
            })
            .ToArray();
        return Of(sqlFile, entries);
    }

    // The sidecar as written, for the malformed cases.
    public static SqlFile Raw(SqlFile sqlFile, string text) => new(sqlFile.Path + ".json", text);

    private static SqlFile Write(SqlFile sqlFile, int formatVersion, string toolVersion, SidecarEntry[] entries) =>
        Raw(sqlFile, SidecarWriter.Write(new Sidecar(formatVersion, toolVersion, TestSidecars.Of(entries))));

    // Throws for the first current entry whose parameters are not its query's.  The dialect of an entry is the one
    // under which its hash is its query's, since a test may hash under another dialect than its engine's.
    private static void CheckParameters(SqlFile sqlFile, SidecarEntry[] entries)
    {
        var parses = Enum.GetValues<SqlDialect>()
            .Select(dialect => SqlFileParser.Parse(sqlFile.Text ?? "", SqlPath.GetFileName(sqlFile.Path), dialect))
            .Where(result => result.Errors.Count == 0)
            .ToArray();
        foreach (var entry in entries)
        {
            foreach (var result in parses)
            {
                var block = result.Blocks.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, entry.Name, StringComparison.Ordinal)
                );
                if (
                    block is null
                    || SqlQueryHash.Compute(result.Dialect, block.Segments, block.Tokens, block.Parameters)
                        != entry.Hash
                )
                {
                    continue;
                }

                var expected = block.Parameters.Select(parameter => parameter.Name).ToArray();
                var listed = entry.Parameters;
                var matches =
                    listed.Count == expected.Length
                    && listed
                        .Select((parameter, place) => (parameter, place))
                        .All(pair =>
                            pair.parameter.Ordinal == pair.place
                            && string.Equals(pair.parameter.Name, expected[pair.place], StringComparison.Ordinal)
                        );
                if (!matches)
                {
                    var actual = listed.Select(
                        (parameter, place) =>
                            parameter.Ordinal == place ? parameter.Name : $"{parameter.Name}@{parameter.Ordinal}"
                    );
                    throw new InvalidOperationException(
                        $"The entry {entry.Name} of {sqlFile.Path}.json lists the parameters "
                            + $"[{string.Join(", ", actual)}], but its query has [{string.Join(", ", expected)}], "
                            + "each as the SQL spells it, in the order of the SQL, numbered from 0.  Fix the entry, "
                            + "or write a sidecar a person edited with Sidecars.HandEdited."
                    );
                }

                break;
            }
        }
    }

    private static string HashOf(SqlFile sqlFile, string queryName, SqlDialect dialect, out SqlDialect effective)
    {
        var block = BlockOf(sqlFile, queryName, dialect, out effective);
        return SqlQueryHash.Compute(effective, block.Segments, block.Tokens, block.Parameters);
    }

    private static SqlBlock BlockOf(SqlFile sqlFile, string queryName, SqlDialect dialect, out SqlDialect effective)
    {
        var result = SqlFileParser.Parse(sqlFile.Text ?? "", SqlPath.GetFileName(sqlFile.Path), dialect);
        effective = result.Dialect;
        if (result.Errors.Count > 0)
        {
            throw new InvalidOperationException($"{sqlFile.Path} does not parse: {result.Errors[0].Kind}.");
        }

        return result.Blocks.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, queryName, StringComparison.Ordinal)
            ) ?? throw new InvalidOperationException($"{sqlFile.Path} has no query {queryName}.");
    }
}
