namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The C# type that the schema's alias type dbo.Email is read as, by the type override of Queries/Notes.sql.
internal readonly record struct Email(string Value);
