namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The settings of query methods: the methods on the type, by MethodLocation.  The model shapes, the suffixes, the model
// namespace and the collection type are the file header's.
[SqlSourceGenerate(
    Source = "Queries/Shapes.sql",
    SqlLocation = SqlLocation.Direct,
    MethodLocation = MethodLocation.Public
)]
internal static partial class ShapeQueries;
