#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/Postgres/EndToEnd/Types/Email.cs, in its namespace, since a test project
// compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The C# type that the schema's domain email is read as, by the type override of Queries/Notes.sql: a method reads it
// from the string that Npgsql gives a domain over text and sends it as that string, through its converters.
internal readonly record struct Email(string Value)
{
    [SqlSourceConverter]
    public static Email FromDatabase(string value) => new(value);

    [SqlSourceConverter]
    public static string ToDatabase(Email email) => email.Value;
}
