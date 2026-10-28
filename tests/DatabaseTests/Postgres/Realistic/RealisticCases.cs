using SqlSource.Settings;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.DatabaseTests.Postgres.Realistic;

// The binding of this project to its realistic corpus, whose harness is in tests/SqlSource.Tool.Testing/Realistic/:
// the corpus of PostgreSQL, its generated models in this assembly, and its descriptions on the database "realistic".
internal static class RealisticCases
{
    public static RealisticCorpus Corpus => RealisticCorpus.Postgres;

    // The models of the classes of Realistic/Generation/RealisticQueries.cs, named as the generator names them.
    public static RealisticModels Models { get; } =
        new(
            typeof(RealisticCases).Assembly,
            "SqlSource.DatabaseTests.Postgres.Realistic.Generation",
            DatabaseEngine.PostgreSql
        );

    public static RealisticDescribe Describe { get; } =
        new(Corpus, PostgresContainer.RealisticDatabase, static () => new PostgresDescriber());
}
