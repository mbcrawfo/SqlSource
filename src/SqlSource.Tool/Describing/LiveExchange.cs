using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The exchange of a real run: it asks the engine and keeps nothing.
/// </summary>
internal sealed class LiveExchange : IDescribeExchange
{
    private LiveExchange() { }

    public static LiveExchange Instance { get; } = new();

    public Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    ) => live(request, cancellationToken);
}
