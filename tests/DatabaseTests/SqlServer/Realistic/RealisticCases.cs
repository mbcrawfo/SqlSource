using SqlSource.Settings;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.DatabaseTests.SqlServer.Realistic;

// The binding of this project to its realistic corpus, whose harness is in tests/SqlSource.Tool.Testing/Realistic/:
// the corpus of SQL Server, its generated models in this assembly, and its descriptions on the database "realistic".
internal static class RealisticCases
{
    public static RealisticCorpus Corpus => RealisticCorpus.SqlServer;

    // The models and methods of the classes of Realistic/Generation/RealisticQueries.cs and RealisticMethods.cs, named
    // as the generator names them.
    public static RealisticModels Models { get; } =
        new(
            typeof(RealisticCases).Assembly,
            "SqlSource.DatabaseTests.SqlServer.Realistic.Generation",
            DatabaseEngine.SqlServer
        );

    public static RealisticDescribe Describe { get; } =
        new(Corpus, SqlServerContainer.RealisticDatabase, static () => new SqlServerDescriber());
}
