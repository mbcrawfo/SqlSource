namespace SqlSource.Generation.Methods;

/// <summary>
/// What an argument of a query's method is for, as <see cref="MethodArguments" /> gives them, in their order.
/// </summary>
internal enum ArgumentRole
{
    /// <summary>The connection the query runs on, which an extension method extends.</summary>
    Receiver,

    /// <summary>The input model, which gives the query's parameters: only a query with parameters has it.</summary>
    Input,

    /// <summary>The text of one of the query's tokens, which replaces it in the SQL.</summary>
    Token,

    /// <summary>The transaction the query runs in, or null.</summary>
    Transaction,

    /// <summary>The command's timeout in seconds, or null for the driver's.</summary>
    CommandTimeout,

    /// <summary>The token that cancels the query.</summary>
    Cancellation,
}
