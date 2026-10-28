namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// A level held in a tinyint, which a query of SQL Server's AllTypes.sql reads and sends by a type override.  SqlClient
// reads an enum only from a column of its own underlying type, so it is over byte.  It is here, and not in one test
// project, since the end-to-end project and the SqlClient 6.1 project both compile that query.
internal enum Level : byte
{
    Low = 1,

    High = 255,
}
