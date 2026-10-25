namespace SqlSource.Tests.SqlServer.EndToEnd;

// The project's output, codegen, with the type overrides of the header and of a query: the alias type dbo.Email to a
// record struct of the project, read and sent through its converters, and time to TimeOnly, which SqlClient reads and
// sends itself.  Extension methods of the connection in NoteQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NoteQueries;
