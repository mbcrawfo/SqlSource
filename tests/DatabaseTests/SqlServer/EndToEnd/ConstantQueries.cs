namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The output sql by a file's header: a constant, no models, and no sidecar.  The header's -- member-location: puts the
// constant on the type.
[SqlSourceGenerate(Source = "Queries/Constants.sql")]
internal static partial class ConstantQueries;
