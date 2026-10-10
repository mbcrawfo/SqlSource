namespace SqlSource.Tool.Describing;

/// <summary>
/// A session, or why there is none.  Exactly one of the two is set.
/// </summary>
internal sealed record OpenResult
{
    private OpenResult(IDescribeSession? session, DescribeFailure? failure)
    {
        Session = session;
        Failure = failure;
    }

    public IDescribeSession? Session { get; }

    public DescribeFailure? Failure { get; }

    public static OpenResult Opened(IDescribeSession session) => new(session, null);

    public static OpenResult Failed(DescribeFailure failure) => new(null, failure);
}
