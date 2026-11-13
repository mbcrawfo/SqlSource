#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/SqlServer/EndToEnd/Types/Email.cs, in its namespace, since a test project
// compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
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
