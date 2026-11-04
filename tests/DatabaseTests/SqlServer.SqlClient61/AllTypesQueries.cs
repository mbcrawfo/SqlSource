namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The queries of every type of the type map that SQL Server 2017 has, linked from the end-to-end project: their
// methods, extension methods of the connection in AllTypesQueriesExtensions, and their models under SqlClient 6.1,
// which Dapper reads.  The linked file's folder takes the end-to-end project's settings file, whose output is codegen,
// which this claim keeps.  The file's ForDapper copies, at the output models, put their constants on the type by their
// -- member-location: markers, for the Dapper test.
[SqlSourceGenerate(Source = "../SqlServer/EndToEnd/Queries/AllTypes.sql")]
internal static partial class AllTypesQueries;

// The types that SQL Server 2025 brings, json and vector, linked from the end-to-end project: their methods, extension
// methods of the connection in NewTypesQueriesExtensions, and the constant of the file's ForDapper copy.
[SqlSourceGenerate(Source = "../SqlServer/EndToEnd/Queries/NewTypes.sql")]
internal static partial class NewTypesQueries;
