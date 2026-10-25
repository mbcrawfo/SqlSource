namespace SqlSource.Tests.Postgres.EndToEnd.Types;

// The C# type that the schema's domain email is read as, by the type override of Queries/Notes.sql: a method reads it
// from the string that Npgsql gives a domain over text and sends it as that string, through its converters.
internal readonly record struct Email(string Value)
{
    [SqlSourceConverter]
    public static Email FromDatabase(string value) => new(value);

    [SqlSourceConverter]
    public static string ToDatabase(Email email) => email.Value;
}
