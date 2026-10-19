using Dapper;
using Npgsql;
using SqlSource.Tests.Postgres.EndToEnd.Types;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The data sources that the Dapper tests run the end-to-end queries and the realistic corpus on, set up as the
// manual's recipe tells a project to and no further: Dapper matches snake_case columns to properties and has a handler
// for each type that a type override names and Npgsql does not give, and "app" maps the enum that its queries read as
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

    public static NpgsqlDataSource App(PostgresContainer postgres)
    {
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionStringOf(PostgresContainer.AppDatabase));
        _ = builder.MapEnum<NoteKind>();
        return builder.Build();
    }

    public static NpgsqlDataSource Billing(PostgresContainer postgres) =>
        NpgsqlDataSource.Create(postgres.ConnectionStringOf(PostgresContainer.BillingDatabase));

    // The realistic corpus's database, which the run through Dapper reads with the same settings and no mapping: its
    // models take the base type map, so an enum is a string.
    public static NpgsqlDataSource Realistic(PostgresContainer postgres) =>
        NpgsqlDataSource.Create(postgres.RealisticConnectionString);
}
