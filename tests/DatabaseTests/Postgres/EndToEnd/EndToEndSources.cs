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

    // "app" as App sets it up, with Npgsql.NodaTime's plugin, which reads and writes the NodaTime types of the queries
    // of NodaTime/, whose folder leaves the library on: a data source the application builds for those methods.
    public static NpgsqlDataSource AppWithNodaTime(PostgresDatabases postgres)
    {
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionStringOf(PostgresDatabases.AppDatabase));
        _ = builder.MapEnum<NoteKind>();
        _ = builder.UseNodaTime();
        return builder.Build();
    }

    public static NpgsqlDataSource Billing(PostgresDatabases postgres) =>
        NpgsqlDataSource.Create(postgres.ConnectionStringOf(PostgresDatabases.BillingDatabase));

    // The realistic corpus's database, which the runs through the methods and through Dapper read with the same
    // settings and no mapping: its models take the base type map, so an enum is a string.
    public static NpgsqlDataSource Realistic(PostgresDatabases postgres) =>
        NpgsqlDataSource.Create(postgres.RealisticConnectionString);
}
