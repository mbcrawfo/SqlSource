namespace SqlSource.Tests.SqlServer.SqlClient61;

// The queries of every type of the type map that SQL Server 2017 has, linked from the end-to-end project, and their
// models under SqlClient 6.1, which Dapper reads.  The linked file's folder takes the end-to-end project's settings
// file, whose output is codegen, so the claim names the output models.
[SqlSourceGenerate(
    Path = "../SqlSource.Tests.SqlServer/EndToEnd/Queries/AllTypes.sql",
    Output = GeneratorOutput.Models,
    SqlLocation = SqlLocation.Direct
)]
internal static partial class AllTypesQueries;

// The types that SQL Server 2025 brings, json and vector, linked from the end-to-end project.
[SqlSourceGenerate(
    Path = "../SqlSource.Tests.SqlServer/EndToEnd/Queries/NewTypes.sql",
    Output = GeneratorOutput.Models,
    SqlLocation = SqlLocation.Direct
)]
internal static partial class NewTypesQueries;

// The same two files a second time, at the output codegen, for their methods on SqlClient 6.1.  Each claim is in the
// namespace of the models' claim, so the two share the files' models, which the methods and Dapper both read.
[SqlSourceGenerate(
    Path = "../SqlSource.Tests.SqlServer/EndToEnd/Queries/AllTypes.sql",
    Output = GeneratorOutput.CodeGen
)]
internal static partial class AllTypesMethodQueries;

[SqlSourceGenerate(
    Path = "../SqlSource.Tests.SqlServer/EndToEnd/Queries/NewTypes.sql",
    Output = GeneratorOutput.CodeGen
)]
internal static partial class NewTypesMethodQueries;
