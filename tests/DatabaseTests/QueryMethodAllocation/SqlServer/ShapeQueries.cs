namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The claim of the SQL Server end-to-end project's ShapeQueries, repeated so that the methods measured here are the
// ones that project runs: the model shapes, a suffix and the model namespace by the attribute, with the methods on the
// type, each taking the connection first; the file's collection type, IReadOnlyList, takes the place of the
// attribute's.
[SqlSourceGenerate(
    Path = "../../SqlServer/EndToEnd/Queries/Shapes.sql",
    OutputModelType = GeneratorModelType.Record,
    OutputModelSuffix = "View",
    InputModelType = GeneratorModelType.SealedClass,
    ModelNamespace = "SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer.Shapes",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.List
)]
internal static partial class ShapeQueries;
