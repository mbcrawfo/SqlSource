namespace SqlSource.Tests.SqlServer.EndToEnd;

// The project's output, codegen, with the type overrides of the header and of a query: the alias type dbo.Email to a
// record struct of the project, read and sent through its converters, and time to TimeOnly, which SqlClient reads and
// sends itself.  The column types of the project and of the header and the queries' markers give two more record
// structs, read and sent through their converters.  Extension methods of the connection in NoteQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NoteQueries;
