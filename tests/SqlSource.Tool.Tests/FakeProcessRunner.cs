using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Tests;

// Stands in for "dotnet msbuild": answers the evaluation of a project with JSON, and the manifest target by writing
// the file it was asked for.  It reads the command line as MSBuild would, so a test of the tool's arguments looks at
// Requests.
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly ConcurrentQueue<ProcessRequest> _requests = new();

    private int _running;

    private int _mostAtOnce;

    // The projects by the full path of their file, as the tool gives it.  A project that is not here is answered by
    // Default.
    public Dictionary<string, FakeProject> Projects { get; } = [];

    public FakeProject Default { get; set; } = new();

    // Runs before each answer, while the request counts as running.
    public Func<ProcessRequest, CancellationToken, Task>? BeforeAnswer { get; set; }

    // Every request, in the order they came.
    public IReadOnlyList<ProcessRequest> Requests => [.. _requests];

    // The largest number of requests that were running at one time.
    public int MostAtOnce => _mostAtOnce;

    // The value of a "-p:Name=value" switch of a request, as MSBuild reads it, or null when it has none.
    public static string? PropertyOf(ProcessRequest request, string name)
    {
        var prefix = "-p:" + name + "=";
        var argument = request.Arguments.LastOrDefault(argument =>
            argument.StartsWith(prefix, StringComparison.Ordinal)
        );
        if (argument is null)
        {
            return null;
        }

        var value = argument[prefix.Length..];
        if (value.AsSpan().IndexOfAny(';', ',') >= 0)
        {
            throw new InvalidOperationException($"MSB1006: MSBuild would split the value of -p:{name} at a ; or a ,");
        }

        return Uri.UnescapeDataString(value);
    }

    public static bool IsManifestRun(ProcessRequest request) =>
        request.Arguments.Contains("-t:" + ProjectEvaluator.ManifestTarget);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);
        var running = Interlocked.Increment(ref _running);
        int most;
        while (running > (most = Volatile.Read(ref _mostAtOnce)))
        {
            _ = Interlocked.CompareExchange(ref _mostAtOnce, running, most);
        }

        try
        {
            if (BeforeAnswer is not null)
            {
                await BeforeAnswer(request, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var project = Projects.GetValueOrDefault(request.Arguments[1], Default);
            return IsManifestRun(request) ? project.WriteManifest(request) : project.Evaluate(request);
        }
        finally
        {
            _ = Interlocked.Decrement(ref _running);
        }
    }
}
