namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The project's output, codegen, with the type overrides of the header, the alias type dbo.Email to a record struct of
// the project, read and sent through its converters, and time to TimeOnly, which SqlClient reads and sends itself, and
// a query's that gives sys.time back to the map.  The column types of the header and the queries' markers give two
// more record structs, read and sent through their converters.  Extension methods of the connection in
// NoteQueriesExtensions.
[SqlSourceGenerate(Source = "Queries/Notes.sql")]
internal static partial class NoteQueries;
