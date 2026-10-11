using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Tests.Processes;

// The variables that a program gets from the real runner.  A program gets the ones of this process, so the test
// gives this process two, and is of the collection that runs alone for that.
[Collection(nameof(ChangesTheEnvironment))]
public class ProcessRunnerEnvironmentTests
{
    private const string Removed = "SQLSOURCE_TEST_REMOVED";

    private const string Kept = "SQLSOURCE_TEST_KEPT";

    [Fact]
    public async Task Run_VariableSet_ReachesTheProgramAndVariableRemovedDoesNot()
    {
        // Prints the environment, one variable on a line.
        var print = OperatingSystem.IsWindows() ? Programs.Request("cmd", "/c", "set") : Programs.Request("env");
        Programs.SkipUnlessOnPath(print.Program);
        var request = print with
        {
            SetVariables = ImmutableDictionary<string, string>.Empty.Add("SQLSOURCE_TEST_SET", "set"),
            RemovedVariables = [Removed, "SQLSOURCE_TEST_NEVER_SET"],
        };
        Environment.SetEnvironmentVariable(Removed, "removed");
        Environment.SetEnvironmentVariable(Kept, "kept");
        try
        {
            var result = await new ProcessRunner().RunAsync(request, TestContext.Current.CancellationToken);

            result.ExitCode.ShouldBe(0, result.Error);
            result.Output.ShouldContain("SQLSOURCE_TEST_SET=set");
            result.Output.ShouldContain(Kept + "=kept");
            result.Output.ShouldNotContain(Removed);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Removed, null);
            Environment.SetEnvironmentVariable(Kept, null);
        }
    }
}
