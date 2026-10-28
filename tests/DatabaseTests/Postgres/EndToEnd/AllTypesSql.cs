namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The SQL of Queries/AllTypes.sql as constants, for the tests that run it through Dapper: the output models, in the
// namespace of AllTypesQueries, whose models it shares.
[SqlSourceGenerate(Path = "Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class AllTypesSql;
