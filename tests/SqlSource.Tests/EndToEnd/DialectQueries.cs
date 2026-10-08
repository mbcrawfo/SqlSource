namespace SqlSource.Tests.EndToEnd;

// One folder whose three files get their dialect in the three ways there are: from the project's property, from the
// metadata of the file's item, and from a marker in the file.
[SqlSourceGenerate(Path = "Dialects", SqlLocation = SqlLocation.Direct)]
internal static partial class DialectQueries;
