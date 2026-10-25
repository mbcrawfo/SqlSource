namespace SqlSource.Tests.Postgres.EndToEnd;

// The SQL of Queries/Notes.sql as constants, for the test that runs it through Dapper: the output models, in the
// namespace of NoteQueries, whose models it shares.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class NoteSql;
