namespace SqlSource.Tests.Postgres.EndToEnd;

// The project's output, codegen, with the header's type overrides to an enum, which the data source maps, and to two
// record structs of the project, and the column types of the header and the queries' markers to two more, each read
// and sent through its converters: extension methods of the connection in NoteQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NoteQueries;
