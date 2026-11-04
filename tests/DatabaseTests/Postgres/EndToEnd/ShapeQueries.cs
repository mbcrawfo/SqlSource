namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The settings of query methods: the methods on the type, public, the model shapes, the suffixes, the model namespace
// and the collection type are the file header's.
[SqlSourceGenerate(Source = "Queries/Shapes.sql")]
internal static partial class ShapeQueries;
