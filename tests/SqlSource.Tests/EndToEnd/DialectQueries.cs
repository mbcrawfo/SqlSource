namespace SqlSource.Tests.EndToEnd;

// One folder whose four files get their dialect from the project's settings file or from a marker in their header, one
// of them with an option and one below a comment that the project's dialect reads.
[SqlSourceGenerate(Path = "Dialects", SqlLocation = SqlLocation.Direct)]
internal static partial class DialectQueries;
