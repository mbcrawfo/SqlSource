namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The model shapes, a suffix and the model namespace by the attribute, with the settings of query methods.
[SqlSourceGenerate(
    Path = "Queries/Shapes.sql",
    SqlLocation = SqlLocation.Direct,
    OutputModelType = GeneratorModelType.Record,
    OutputModelSuffix = "View",
    InputModelType = GeneratorModelType.SealedClass,
    ModelNamespace = "SqlSource.DatabaseTests.Postgres.EndToEnd.Shapes",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.List
)]
internal static partial class ShapeQueries;
