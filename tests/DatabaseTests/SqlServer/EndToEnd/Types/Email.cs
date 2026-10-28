namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;

// The C# type that the schema's alias type dbo.Email is read as, by the type override of Queries/Notes.sql: a method
// reads it from the string that SqlClient gives an alias type over nvarchar and sends it as that string, through its
// converters.  Dapper reads it through EmailHandler.
internal readonly record struct Email(string Value)
{
    [SqlSourceConverter]
    public static Email FromDatabase(string value) => new(value);

    [SqlSourceConverter]
    public static string ToDatabase(Email email) => email.Value;
}
