namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The model shapes, a suffix and the model namespace by the attribute, with the methods on the type, each taking the
// connection first; the file's collection type, IReadOnlyList, takes the place of the attribute's.
[SqlSourceGenerate(
    Path = "Queries/Shapes.sql",
    OutputModelType = GeneratorModelType.Record,
    OutputModelSuffix = "View",
    InputModelType = GeneratorModelType.SealedClass,
    ModelNamespace = "SqlSource.DatabaseTests.SqlServer.EndToEnd.Shapes",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.List
)]
internal static partial class ShapeQueries;
