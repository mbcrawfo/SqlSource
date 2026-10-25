using System.Threading;

namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The C# type that ListAccountIdsCancellingOnRead of Queries/Methods.sql reads its int as, by its type override, so
// that RunTimeArgumentTests can cancel a token between two rows: the generated method gives each row's int to
// FromDatabase, which cancels Source, and the ReadAsync of the next row sees the token cancelled.  Source is the
// state of one test alone, which sets it before its call and puts it back to null in a finally.
internal readonly record struct CancelOnRead(int Value)
{
    public static CancellationTokenSource? Source { get; set; }

    [SqlSourceConverter]
    public static CancelOnRead FromDatabase(int value)
    {
        Source?.Cancel();
        return new(value);
    }

    [SqlSourceConverter]
    public static int ToDatabase(CancelOnRead value) => value.Value;
}
