using System.Threading;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;

// The C# type that ListAccountIdsCancellingOnRead of Queries/Methods.sql reads its int as, by its type override, so
// that RunTimeArgumentTests can cancel a token between two rows: the generated method gives each row's int to
// FromDatabase, which cancels Source, and the ReadAsync of the next row sees the token cancelled.  Source is held for
// each async flow: the generated method runs in the flow of the test that set it, and a test run beside it sees its
// own, or none.
internal readonly record struct CancelOnRead(int Value)
{
    private static readonly AsyncLocal<CancellationTokenSource?> FlowSource = new();

    public static CancellationTokenSource? Source
    {
        get => FlowSource.Value;
        set => FlowSource.Value = value;
    }

    [SqlSourceConverter]
    public static CancelOnRead FromDatabase(int value)
    {
        Source?.Cancel();
        return new(value);
    }

    [SqlSourceConverter]
    public static int ToDatabase(CancelOnRead value) => value.Value;
}
