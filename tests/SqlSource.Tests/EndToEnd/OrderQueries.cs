namespace SqlSource.Tests.EndToEnd;

// One file whose header puts the members on the type: the constants are members of the type itself.
[SqlSourceGenerate(Source = "Orders/Orders.sql")]
internal static partial class OrderQueries;
