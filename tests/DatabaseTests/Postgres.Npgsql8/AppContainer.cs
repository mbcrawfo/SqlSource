using System.Collections.Generic;
using Npgsql;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// A PostgreSQL of EngineImage.Postgres.Default, the end-to-end project's default image, which ignores the variable that
// names another, with the end-to-end database "app": its schema and its seed.
public sealed class AppContainer() : PostgresContainer(NpgsqlFactory.Instance, EngineImage.Postgres.Default)
{
    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [("app", EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, "app"))];
}
