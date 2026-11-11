namespace SqlSource.Generation.Methods;

/// <summary>
/// Whether a query's method awaits its calls and returns a task, or makes the synchronous calls and returns its
/// result, the method's <see cref="QueryMethod.Synchrony" />.  The default is asynchronous.
/// </summary>
internal enum MethodSynchrony
{
    /// <summary>
    /// The method awaits each call, takes a cancellation token and returns a task of its result, or streams its rows.
    /// </summary>
    Asynchronous = 0,

    /// <summary>
    /// The method makes the synchronous call for each, takes no cancellation token and returns its result, or nothing;
    /// it never streams.
    /// </summary>
    Synchronous = 1,
}
