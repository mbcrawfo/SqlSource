namespace SqlSource.Tests.SqlServer.EndToEnd;

// The output codegen by the attribute, and the type overrides of the header and of a query.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.CodeGen)]
internal static partial class NoteQueries;
