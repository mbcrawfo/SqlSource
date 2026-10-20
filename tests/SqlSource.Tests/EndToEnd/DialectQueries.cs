namespace SqlSource.Tests.EndToEnd;

// One folder whose four files get their dialect in the three ways there are: from the project's property, from the
// metadata of the file's item, one of them with an option, and from a marker in the file.
[SqlSourceGenerate(Path = "Dialects", SqlLocation = SqlLocation.Direct)]
internal static partial class DialectQueries;
