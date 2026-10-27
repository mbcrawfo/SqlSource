namespace SqlSource.Tests.EndToEnd;

// The files of the Parameters folder, under the generator parameters of the settings file of the EndToEnd folder,
// sqlsource.json, or of their header.
[SqlSourceGenerate(Path = "Parameters", SqlLocation = SqlLocation.Direct)]
internal static partial class ParameterQueries;

// One of those files again, for a type whose attribute asks for comments.
[SqlSourceGenerate(Path = "Parameters/Shared.sql", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
internal static partial class KeptQueries;
