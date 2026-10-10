using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Tests;

// An exchange that asks the engine, as the live one does, and keeps the name of each question.
internal sealed class RecordingExchange : IDescribeExchange
{
    public List<string> Methods { get; } = [];

    public Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        Methods.Add(method);
        return live(request, cancellationToken);
    }
}
