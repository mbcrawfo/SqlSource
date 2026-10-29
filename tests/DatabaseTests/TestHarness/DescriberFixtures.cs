using System.IO;
using SqlSource.Testing;

namespace SqlSource.DatabaseTests.TestHarness;

// The folder of an engine's describer fixtures, tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine>/: its
// schema.sql and its cases, which the engine's describer project reads and records in the repository itself and not in
// an output folder, since recording writes there.  Each is made when it is read, so that a project of one engine never
// reads the other's folder, which RepositoryRoot would refuse.
internal sealed record DescriberFixtures(string Folder)
{
    // PostgreSQL's.
    public static DescriberFixtures Postgres => Of("postgres");

    // SQL Server's.
    public static DescriberFixtures SqlServer => Of("mssql");

    // The script that creates the engine's fixture database.
    public string Schema => Path.Combine(Folder, "schema.sql");

    private static DescriberFixtures Of(string engine) =>
        new(RepositoryRoot.Path("tests", "SqlSource.Tool.Tests", "Fixtures", "Describers", engine));
}
