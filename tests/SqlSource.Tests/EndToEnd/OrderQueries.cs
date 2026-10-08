namespace SqlSource.Tests.EndToEnd;

// One file, direct location: the constants are members of the type itself.
[SqlSourceGenerate(Path = "Orders/Orders.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class OrderQueries;
