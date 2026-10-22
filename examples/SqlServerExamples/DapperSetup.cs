using Dapper;
using Microsoft.Data.SqlClient;

namespace SqlServerExamples;

// What the manual's recipe for Dapper sets up once, before the first query: Dapper matches the columns in snake_case to
// the models' properties, and reads a date, which the project's type override makes a DateOnly, through
// DateOnlyHandler.  See docs/manual/recipes.md, "Run the models through Dapper".
public static class DapperSetup
{
    static DapperSetup()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
    }

    public static SqlConnection CreateConnection(string connectionString) => new(connectionString);
}
