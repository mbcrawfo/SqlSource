namespace SqlSource.Tests.EndToEnd;

// The files of the Parameters folder, with whatever MSBuild says about each.
[SqlSourceGenerate(Path = "Parameters", SqlLocation = SqlLocation.Direct)]
internal static partial class ParameterQueries;

// One of those files again, for a type whose attribute asks for comments.
[SqlSourceGenerate(Path = "Parameters/Shared.sql", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
internal static partial class KeptQueries;
