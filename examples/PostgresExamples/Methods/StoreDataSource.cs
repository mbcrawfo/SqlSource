using Npgsql;

namespace PostgresExamples.Methods;

// The data source of the connections given to the classes of Methods/.  The project references Npgsql's plugins for
// NodaTime and NetTopologySuite, so the methods read and send their types, which Npgsql reads only on a data source
// taught them by UseNodaTime() and UseNetTopologySuite().  DapperSetup's data source calls neither, since after
// UseNodaTime() Npgsql gives Dapper an Instant for every timestamptz, which the models' DateTimeOffset cannot take.
// See site/postgresql.md, "What a method reads and throws".
public static class StoreDataSource
{
    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _ = builder.UseNodaTime();
        _ = builder.UseNetTopologySuite();
        return builder.Build();
    }
}
