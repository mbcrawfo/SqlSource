using System.IO;

namespace SqlSource.Testing;

// The files of the end-to-end databases "app" and "billing", in the repository: a folder for each under
// tests/SqlSource.Tests.Postgres/EndToEnd/, with its schema.sql and its seed.sql, which tools/describe-end-to-end.sh
// applies in the same order.  Both PostgreSQL test projects build their databases from them.
internal static class EndToEndFiles
{
    public static string Folder { get; } =
        Path.Combine(RepositoryRoot.Folder, "tests", "SqlSource.Tests.Postgres", "EndToEnd");

    public static string Schema(string database) => Path.Combine(Folder, database, "schema.sql");

    public static string Seed(string database) => Path.Combine(Folder, database, "seed.sql");
}
