namespace SqlSource.Tests.EndToEnd;

// One file, direct mode: the constants are members of the type itself.
[SqlQueries(Path = "Orders/Orders.sql", Mode = SqlQueriesMode.Direct)]
internal static partial class OrderQueries;
