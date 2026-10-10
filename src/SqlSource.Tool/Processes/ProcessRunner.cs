using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Processes;

/// <summary>
/// The runner of a real run, over <see cref="Process" />.
/// </summary>
internal sealed class ProcessRunner : IProcessRunner
{
    // Without a byte order mark: the encoding is also what the process is told its input has.
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(request.Program)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in request.Arguments)
        {
            // ArgumentList quotes each argument as the operating system needs, so a path with a space is one.
            start.ArgumentList.Add(argument);
        }

        foreach (var name in request.RemovedVariables)
        {
            _ = start.Environment.Remove(name);
        }

        foreach (var (name, value) in request.SetVariables)
        {
            start.Environment[name] = value;
        }

        using var process = new Process { StartInfo = start };
        try
        {
            _ = process.Start();
        }
        catch (Win32Exception exception)
        {
            // No such program, or no such working directory.
            return new ProcessResult(ProcessResult.NotStarted, "", exception.Message);
        }

        try
        {
            // A program that asks a question would wait for an answer for ever.  It finds the end of its input.
            process.StandardInput.Close();

            // Both are read while the program runs: one that fills an output nobody reads stops and waits.
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            // MSBuild starts processes of its own, and they would go on without this one.
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It ended between the cancellation and here.
        }
    }
}
