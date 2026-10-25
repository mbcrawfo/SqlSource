namespace SqlSource.Tests.SqlServer.EndToEnd;

// The second claims of the files whose SQL a Dapper test runs, at the output models: a query of the project's default,
// codegen, has a method and no constant.  Each is in the namespace of the file's codegen claim, so the two share the
// file's models, which the methods and Dapper both read.

// The SQL of Queries/AllTypes.sql, which AllTypesQueryTests and DapperRecipeTests run through Dapper.
[SqlSourceGenerate(Path = "Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class AllTypesDapperQueries;

// The SQL of Queries/NewTypes.sql, which NewTypesQueryTests runs through Dapper.
[SqlSourceGenerate(Path = "Queries/NewTypes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class NewTypesDapperQueries;

// The SQL of Queries/Writes.sql, whose AddNote DapperRecipeTests runs through Dapper.
[SqlSourceGenerate(Path = "Queries/Writes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class WriteDapperQueries;

// The SQL of Queries/Notes.sql, whose ListMarkedNames DapperRecipeTests runs through Dapper.
[SqlSourceGenerate(Path = "Queries/Notes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
internal static partial class NoteDapperQueries;
