namespace SqlSource.DatabaseTests.TestHarness;

// The default image of each engine's tests: one constant for each, so that the tests and the scripts read an image from
// one place.
internal static class DefaultImages
{
    // tools/pull-test-images.sh, tools/record-fixtures.sh, when run for postgres or with no argument, and
    // tools/describe-end-to-end.sh read this line by its shape, with sed, and AppContainer of
    // tests/DatabaseTests/Postgres.Npgsql8 builds on it.  examples/PostgresExamples/docker-compose.yml repeats the
    // tag, which rule 5 of AGENTS.md keeps in step.  It names a major and a minor, as every image of the tests does:
    // the recorded fixtures carry the version they were recorded against, and a moving tag would run another.
    // See tests/DatabaseTests/Postgres/README.md.
    public const string Postgres = "postgres:18.6";

    // tools/pull-test-images.sh, tools/record-fixtures.sh, when run for mssql or with no argument, and
    // tools/describe-end-to-end.sh read this line by its shape, with sed, and AppContainer of
    // tests/DatabaseTests/SqlServer.SqlClient61 builds on it.  examples/SqlServerExamples/docker-compose.yml
    // repeats the tag, which rule 5 of AGENTS.md keeps in step.  It names its cumulative update, and the GDR over it
    // when the build has one, as every SQL Server image of the tests does, and
    // SqlServerContainer.ImageBuilds of tests/DatabaseTests/SqlServer records the build its server reports.
    // See tests/DatabaseTests/SqlServer/README.md.
    public const string SqlServer = "mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04";
}
