using Dapper;
using Npgsql;

namespace PostgresExamples;

// What the manual's recipe for Dapper sets up once, before the first query: Dapper matches the columns in snake_case to
// the models' properties, and reads a timestamptz, which the project's type override makes a DateTimeOffset, through
// UtcDateTimeOffsetHandler.  Its data source calls neither UseNodaTime() nor UseNetTopologySuite(), which
// StoreQueries.CreateDataSource calls for the generated methods: after UseNodaTime(), Npgsql gives Dapper an Instant
// for every timestamptz, which UtcDateTimeOffsetHandler cannot read.  See docs/manual/recipes.md, "Run the models
// through Dapper".
public static class DapperSetup
{
    static DapperSetup()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
    }

    public static NpgsqlDataSource CreateDataSource(string connectionString) =>
        NpgsqlDataSource.Create(connectionString);
}
