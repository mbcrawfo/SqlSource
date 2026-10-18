using System;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;

namespace SqlSource.Testing.Generator;

// Hands the generator a sidecar beside a .sql file, and computes a query's hash as the generator does, so that a test
// writes a current entry without restating the algorithm.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class Sidecars
{
    // The hash the generator computes for the query with this name in the file, under this dialect.  A -- dialect:
    // marker in the file wins, as it does in a build.  A name the file lacks is a bug of the test, and so is a file
    // that does not parse.  The caller passes the dialect the generator will see for the file, its metadata's or the
    // project's, since these helpers do not read SqlFile.Dialect.
    public static string HashOf(SqlFile sqlFile, string queryName, SqlDialect dialect = SqlDialect.Ansi) =>
        HashOf(sqlFile, queryName, dialect, out _);

    // An entry of a query with no rows and no parameters, against the database main of a server of version 18.0.
    public static SidecarEntry Entry(string name, string hash, string engine = "postgres") =>
        TestSidecars.Entry(name, hash, engine, "main", "18.0", SidecarResultKind.None);

    // The sidecar of the file, by default of the current format and the current tool.
    public static SqlFile Of(SqlFile sqlFile, params SidecarEntry[] entries) =>
        Of(sqlFile, SidecarFormat.Version, PackageVersion.Prefix, entries);

    public static SqlFile Of(SqlFile sqlFile, int formatVersion, string toolVersion, params SidecarEntry[] entries) =>
        Raw(
            sqlFile,
            SidecarWriter.Write(
                new Sidecar(
                    formatVersion,
                    toolVersion,
                    new EquatableArray<SidecarEntry>(ImmutableArray.Create(entries))
                )
            )
        );

    // A sidecar with a current entry for each of the queries, for the engine of the dialect the file is read by.
    public static SqlFile Current(SqlFile sqlFile, SqlDialect dialect, params string[] queryNames)
    {
        var entries = queryNames
            .Select(name =>
            {
                var hash = HashOf(sqlFile, name, dialect, out var effective);
                return Entry(name, hash, SqlDialectName.Canonical(effective));
            })
            .ToArray();
        return Of(sqlFile, entries);
    }

    // The sidecar as written, for the malformed cases.
    public static SqlFile Raw(SqlFile sqlFile, string text) => new(sqlFile.Path + ".json", text);

    private static string HashOf(SqlFile sqlFile, string queryName, SqlDialect dialect, out SqlDialect effective)
    {
        var result = SqlFileParser.Parse(sqlFile.Text ?? "", SqlPath.GetFileName(sqlFile.Path), dialect);
        effective = result.Dialect;
        if (result.Errors.Count > 0)
        {
            throw new InvalidOperationException($"{sqlFile.Path} does not parse: {result.Errors[0].Kind}.");
        }

        var block =
            result.Blocks.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, queryName, StringComparison.Ordinal)
            ) ?? throw new InvalidOperationException($"{sqlFile.Path} has no query {queryName}.");
        return SqlQueryHash.Compute(result.Dialect, block.Segments, block.Tokens, block.Parameters);
    }
}
