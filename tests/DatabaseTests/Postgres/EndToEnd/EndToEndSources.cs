using Dapper;
using Npgsql;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The data sources that the tests run the end-to-end queries on, through their methods or through Dapper, and the
// realistic corpus on, set up as the manual's recipe tells a project to and no further: Dapper matches snake_case
// columns to properties and has a handler for each type that a type override of a query it reads names and Npgsql does
// not give, Email's by the recipe's handler that calls its converters, and "app" maps the enum that its queries read as
// a C# enum.  A test disposes the source it is given.
internal static class EndToEndSources
{
    // Dapper's settings are the process's, so they are made once, before the first source.
    static EndToEndSources()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new EmailHandler());
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
    }

    public static NpgsqlDataSource App(PostgresDatabases postgres)
    {
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionStringOf(PostgresDatabases.AppDatabase));
        _ = builder.MapEnum<NoteKind>();
        return builder.Build();
    }

    // "app" as App sets it up, with no command timeout in place of Npgsql's default of 30 seconds, for a test whose
    // command waits on a lock until the test acts: only the call's own commandTimeout or token ends that wait.
    public static NpgsqlDataSource AppWithoutCommandTimeout(PostgresDatabases postgres)
    {
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionStringOf(PostgresDatabases.AppDatabase));
        builder.ConnectionStringBuilder.CommandTimeout = 0;
        _ = builder.MapEnum<NoteKind>();
        return builder.Build();
    }

    // "app" as App sets it up, with Multiplexing=true, which Npgsql refuses synchronous commands on outside a
    // transaction: SynchronousMethodTests pins what its synchronous methods do on it.
    public static NpgsqlDataSource AppMultiplexing(PostgresDatabases postgres)
    {
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionStringOf(PostgresDatabases.AppDatabase));
        builder.ConnectionStringBuilder.Multiplexing = true;
        _ = builder.MapEnum<NoteKind>();
        return builder.Build();
    }

    // "app" as App sets it up, with Npgsql.NodaTime's plugin, which reads and writes the NodaTime types of the queries
    // of NodaTime/, whose folder turns the library back on: a data source the application builds for those methods.
    public static NpgsqlDataSource AppWithNodaTime(PostgresDatabases postgres)
    {
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionStringOf(PostgresDatabases.AppDatabase));
        _ = builder.MapEnum<NoteKind>();
        _ = builder.UseNodaTime();
        return builder.Build();
    }

    // "gis" of the PostGIS container, with Npgsql.NetTopologySuite's plugin, which reads and writes the geometries of
    // the queries of NetTopologySuite/, whose folder turns the library back on.
    public static NpgsqlDataSource GisWithNetTopologySuite(PostgisDatabases postgis)
    {
        var builder = new NpgsqlDataSourceBuilder(postgis.ConnectionStringOf(PostgisDatabases.GisDatabase));
        _ = builder.UseNetTopologySuite();
        return builder.Build();
    }

    // The connection string of "app", for a repository that makes its own connections of it: a bare NpgsqlConnection
    // has none of App's mappings, so a query on it reads no note_kind as NoteKind.
    public static string AppConnectionString(PostgresDatabases postgres) =>
        postgres.ConnectionStringOf(PostgresDatabases.AppDatabase);

    public static NpgsqlDataSource Billing(PostgresDatabases postgres) =>
        NpgsqlDataSource.Create(postgres.ConnectionStringOf(PostgresDatabases.BillingDatabase));

    // The realistic corpus's database, which the runs through the methods and through Dapper read with the same
    // settings and no mapping: its models take the base type map, so an enum is a string.
    public static NpgsqlDataSource Realistic(PostgresDatabases postgres) =>
        NpgsqlDataSource.Create(postgres.RealisticConnectionString);
}
