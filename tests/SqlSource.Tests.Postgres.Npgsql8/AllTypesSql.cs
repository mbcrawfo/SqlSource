namespace SqlSource.Tests.Postgres.Npgsql8;

// The SQL of the linked AllTypes.sql as constants, for the test that runs it through Dapper: the output models, in the
// namespace of AllTypesQueries, whose models it shares.
[SqlSourceGenerate(
    Path = "../SqlSource.Tests.Postgres/EndToEnd/Queries/AllTypes.sql",
    SqlLocation = SqlLocation.Direct,
    Output = GeneratorOutput.Models
)]
internal static partial class AllTypesSql;
