using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Processes;

// The variables that a program gets from the real runner.  A program gets the ones of this process, so a test
// gives this process some, and is of the collection that runs alone for that.
[Collection(nameof(ChangesTheEnvironment))]
public sealed class ProcessRunnerEnvironmentTests
{
    private const string Removed = "SQLSOURCE_TEST_REMOVED";

    private const string Kept = "SQLSOURCE_TEST_KEPT";

    private const string Family = "SQLSOURCE_TEST_FAMILY_";

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

    // A name is cut off whole: the prefix ends where the name of the family does.
    [Fact]
    public async Task Run_PrefixRemoved_TakesEveryVariableThatStartsWithItInAnyCase()
    {
        var print = OperatingSystem.IsWindows() ? Programs.Request("cmd", "/c", "set") : Programs.Request("env");
        Programs.SkipUnlessOnPath(print.Program);
        var request = print with { RemovedPrefixes = [Family, "SQLSOURCE_TEST_NEVER_SET_"] };
        string[] removed = [Family + "ONE", "sqlsource_test_family_two"];
        string[] kept = ["SQLSOURCE_TEST_FAMILY", Kept];
        foreach (var variable in removed.Concat(kept))
        {
            Environment.SetEnvironmentVariable(variable, "value");
        }

        try
        {
            var result = await new ProcessRunner().RunAsync(request, TestContext.Current.CancellationToken);

            result.ExitCode.ShouldBe(0, result.Error);
            var lines = result.Output.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            lines.ShouldNotContain(line => line.StartsWith(Family, StringComparison.OrdinalIgnoreCase));
            lines.ShouldContain("SQLSOURCE_TEST_FAMILY=value");
            lines.ShouldContain(Kept + "=value");
        }
        finally
        {
            foreach (var variable in removed.Concat(kept))
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
    }
}
