using Dapper;
using Npgsql;

namespace PostgresExamples;

// What the manual's guide to Dapper sets up once, before the first query: Dapper matches the columns in snake_case to
// the models' properties, and reads a timestamptz, which the project's type override makes a DateTimeOffset, through
// UtcDateTimeOffsetHandler.  Its data source calls neither UseNodaTime() nor UseNetTopologySuite(), which
// Methods.StoreDataSource.CreateDataSource calls for the generated methods: after UseNodaTime(), Npgsql gives Dapper
// an Instant for every timestamptz, which UtcDateTimeOffsetHandler cannot read.  See
// site/guides/dapper.md#models-through-dapper and site/guides/dapper.md#dapper-with-postgresql.
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
