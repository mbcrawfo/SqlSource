namespace SqlSource.Tests.Postgres.EndToEnd.Types;

// The C# type that the schema's domain email is read as, by the type override of Queries/Notes.sql.
internal readonly record struct Email(string Value);
