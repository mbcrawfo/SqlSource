namespace SqlSource.Tests.SqlServer.EndToEnd;

// The output sql by a file's header: a constant, no models, and no sidecar.
[SqlSourceGenerate(Path = "Queries/Constants.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class ConstantQueries;
