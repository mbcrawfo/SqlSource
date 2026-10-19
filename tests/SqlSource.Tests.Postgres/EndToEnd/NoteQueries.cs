namespace SqlSource.Tests.Postgres.EndToEnd;

// The output codegen by the attribute, and the header's type overrides to an enum and a record struct of the project.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.CodeGen)]
internal static partial class NoteQueries;
