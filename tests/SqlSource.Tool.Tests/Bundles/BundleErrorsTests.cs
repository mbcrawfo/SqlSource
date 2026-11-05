using System.Linq;
using Shouldly;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleErrorsTests
{
    private const string Path = "/work/report.zip";

    // A control character of each kind that a crafted bundle may hold, and how it is printed: C0's escape and bell, a
    // character of C1, DEL, and a line break.
    public static TheoryData<string, string> Controls =>
        new()
        {
            { "\u001b", "\\u001B" },
            { "\u0007", "\\u0007" },
            { "\u009b", "\\u009B" },
            { "\u007f", "\\u007F" },
            { "\n", "\\u000A" },
        };

    [Theory]
    [MemberData(nameof(Controls))]
    public void CannotBeRead_ReasonWithAControlCharacter_EscapesItAndKeepsTheTab(string control, string escape)
    {
        var error = BundleErrors.CannotBeRead(Path, $"the kind is 'x{control}[2J\ty'");

        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Path.ShouldBe(Path);
        error.Arguments.ToArray().ShouldBe([$"the kind is 'x{escape}[2J\ty'"]);
    }

    [Fact]
    public void OfNewerTool_Numbers_AreItsArguments() =>
        BundleErrors
            .OfNewerTool(Path, "captureVersion", 2, 1)
            .Arguments.ToArray()
            .ShouldBe(["captureVersion", "2", "1"]);

    [Fact]
    public void CallMissing_MadeByAVersion_NamesItAndThisVersion()
    {
        var error = BundleErrors.CallMissing(Path, "columns", BundleErrors.MadeBy("1.4.0"));

        error.Descriptor.Id.ShouldBe("SQLSRC290");
        error.Path.ShouldBe(Path);
        error.Arguments.ToArray().ShouldBe(["columns", "sqlsource 1.4.0", Cli.Version]);
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void CallMissing_MethodWithAControlCharacter_EscapesItAndKeepsTheTab(string control, string escape) =>
        BundleErrors
            .CallMissing(Path, $"col{control}\tumns", BundleErrors.MadeBy("1.4.0"))
            .Arguments.ToArray()
            .ShouldBe([$"col{escape}\tumns", "sqlsource 1.4.0", Cli.Version]);

    [Theory]
    [MemberData(nameof(Controls))]
    public void CallMissing_ToolVersionWithAControlCharacter_EscapesItAndKeepsTheTab(string control, string escape) =>
        BundleErrors
            .CallMissing(Path, "columns", BundleErrors.MadeBy($"1.4{control}[3A\t0"))
            .Arguments.ToArray()
            .ShouldBe(["columns", $"sqlsource 1.4{escape}[3A\t0", Cli.Version]);

    [Fact]
    public void MadeBy_NoVersion_IsAnotherVersion() =>
        BundleErrors.MadeBy(null).ShouldBe("another version of sqlsource");

    [Theory]
    [MemberData(nameof(Controls))]
    public void NoDescriber_EngineWithAControlCharacter_EscapesItAndKeepsTheTab(string control, string escape)
    {
        var error = BundleErrors.NoDescriber(Path, $"x{control}]0;pwned\tsql");

        error.Descriptor.Id.ShouldBe("SQLSRC291");
        error.Arguments.ToArray().ShouldBe([$"x{escape}]0;pwned\tsql"]);
    }
}
