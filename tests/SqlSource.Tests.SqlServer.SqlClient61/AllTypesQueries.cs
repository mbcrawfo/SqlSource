namespace SqlSource.Tests.SqlServer.SqlClient61;

// The queries of every type of the type map that SQL Server 2017 has, linked from the end-to-end project, and their
// models under SqlClient 6.1.
[SqlSourceGenerate(
    Path = "../SqlSource.Tests.SqlServer/EndToEnd/Queries/AllTypes.sql",
    SqlLocation = SqlLocation.Direct
)]
internal static partial class AllTypesQueries;

// The types that SQL Server 2025 brings, json and vector, linked from the end-to-end project.
[SqlSourceGenerate(
    Path = "../SqlSource.Tests.SqlServer/EndToEnd/Queries/NewTypes.sql",
    SqlLocation = SqlLocation.Direct
)]
internal static partial class NewTypeQueries;
