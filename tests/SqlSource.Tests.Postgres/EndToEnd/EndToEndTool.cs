using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The tool run in process, as the command line runs it, with the real "dotnet msbuild" and the PostgreSQL describer.
// The tool's own tests fake MSBuild, which these tests exist not to do.
internal static class EndToEndTool
{
    public static async Task<CliResult> RunAsync(string workingDirectory, params string[] args)
    {
        using var temp = new TempFolder();
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter { NewLine = "\n" };
        var host = Hosts.Real(workingDirectory, temp.Path) with
        {
            Out = output,
            Error = error,
            Describers = new DescriberRegistry([new PostgresDescriber()]),
        };

        var exitCode = await Cli.RunAsync(args, host, CancellationToken.None);

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }
}
