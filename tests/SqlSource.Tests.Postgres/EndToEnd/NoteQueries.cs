namespace SqlSource.Tests.Postgres.EndToEnd;

// The output models by the attribute, and the header's type overrides to an enum and a record struct of the project.
// Sub-phases 8.3 and 8.4 set it back to CodeGen, when the generated methods are what the tests run.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class NoteQueries;
