namespace SqlSource.Tool.Describing;

/// <summary>
/// A description, or why there is none.  Exactly one of the two is set.
/// </summary>
internal sealed record DescribeResult
{
    private DescribeResult(QueryDescription? description, DescribeFailure? failure)
    {
        Description = description;
        Failure = failure;
    }

    public QueryDescription? Description { get; }

    public DescribeFailure? Failure { get; }

    public static DescribeResult Described(QueryDescription description) => new(description, null);

    public static DescribeResult Failed(DescribeFailure failure) => new(null, failure);
}
