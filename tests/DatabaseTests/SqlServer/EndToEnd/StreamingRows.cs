namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// What StreamingQueryTests and StreamingBatchTests both read.
internal static class StreamingRows
{
    // The seed's three accounts.
    internal static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // The count of the numbers that a break leaves unread.  The query sorts them, so the server has all of them before
    // it sends the first and is still sending when the loop stops.
    internal const int ManyRows = 1_000_000;

    // The number of SQL Server's error "Divide by zero error encountered.".
    internal const int DivideByZero = 8134;
}
