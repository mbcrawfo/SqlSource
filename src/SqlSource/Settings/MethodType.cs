namespace SqlSource.Settings;

/// <summary>
/// Which methods a query gets, as the <c>method-type</c> generator parameter names it.  The members are in the order
/// of the parameter's words, so the index of a word is the member's value.
/// </summary>
internal enum MethodType
{
    /// <summary><c>async</c>: the query gets asynchronous methods.</summary>
    Async = 0,

    /// <summary><c>sync</c>: the query gets synchronous methods.</summary>
    Sync = 1,

    /// <summary><c>both</c>: the query gets a synchronous method and an asynchronous one.</summary>
    Both = 2,
}
