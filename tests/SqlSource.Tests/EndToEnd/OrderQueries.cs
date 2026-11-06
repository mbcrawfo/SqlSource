namespace SqlSource.Tests.EndToEnd;

// One file whose header puts its members, constants and methods alike, on the type itself.
[SqlSourceGenerate(Source = "Orders/Orders.sql")]
internal static partial class OrderQueries;
