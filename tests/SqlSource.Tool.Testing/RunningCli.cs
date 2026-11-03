using System.Threading.Tasks;

namespace SqlSource.Tool.Testing;

// A run that CliRun.Start started and that may still be going: what it has written so far, read at any time from any
// thread, and the task of its end.  Lines end with "\n" on every operating system.
internal sealed class RunningCli(SharedWriter output, SharedWriter error, Task<CliResult> ended)
{
    public string Output => output.ToString();

    public string Errors => error.ToString();

    public Task<CliResult> Ended => ended;
}
