namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The project's output, codegen, with the header's type overrides to an enum, which the data source maps, and to two
// record structs of the project, and the column types of the header and the queries' markers to two more, each read
// and sent through its converters: extension methods of the connection in NoteQueriesExtensions.  The file's
// ForDapper copy, ListNoteKindsForDapper, puts its constant on the type by its -- member-location:, for a Dapper test.
[SqlSourceGenerate(Source = "Queries/Notes.sql")]
internal static partial class NoteQueries;
