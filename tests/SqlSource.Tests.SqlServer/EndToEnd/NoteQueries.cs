namespace SqlSource.Tests.SqlServer.EndToEnd;

// The output models by the attribute, and the type overrides of the header and of a query.  Sub-phases 8.3 and 8.4 set
// it back to CodeGen, when the generated methods are what the tests run.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class NoteQueries;
