using System.Linq;
using Shouldly;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleErrorsTests
{
    [Fact]
    public void CannotBeRead_ReasonWithAControlCharacter_EscapesItAndKeepsTheTab()
    {
        var error = BundleErrors.CannotBeRead(BundleInfos.Path, "the kind is 'x\u001b[2J\ty'");

        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Path.ShouldBe(BundleInfos.Path);
        error.Arguments.ToArray().ShouldBe(["the kind is 'x\\u001B[2J\ty'"]);
    }

    [Fact]
    public void OfNewerTool_Numbers_AreItsArguments() =>
        BundleErrors
            .OfNewerTool(BundleInfos.Path, "captureVersion", 2, 1)
            .Arguments.ToArray()
            .ShouldBe(["captureVersion", "2", "1"]);

    [Fact]
    public void CallMissing_MadeByAVersion_NamesItAndThisVersion()
    {
        var error = BundleErrors.CallMissing(BundleInfos.Path, "columns", BundleErrors.MadeBy("1.4.0"));

        error.Descriptor.Id.ShouldBe("SQLSRC290");
        error.Path.ShouldBe(BundleInfos.Path);
        error.Arguments.ToArray().ShouldBe(["columns", "sqlsource 1.4.0", Cli.Version]);
    }

    [Fact]
    public void CallMissing_MethodWithAControlCharacter_EscapesItAndKeepsTheTab() =>
        BundleErrors
            .CallMissing(BundleInfos.Path, "col\u001b\tumns", BundleErrors.MadeBy("1.4.0"))
            .Arguments.ToArray()
            .ShouldBe(["col\\u001B\tumns", "sqlsource 1.4.0", Cli.Version]);

    [Fact]
    public void CallMissing_ToolVersionWithAControlCharacter_EscapesItAndKeepsTheTab() =>
        BundleErrors
            .CallMissing(BundleInfos.Path, "columns", BundleErrors.MadeBy("1.4\u001b[3A\t0"))
            .Arguments.ToArray()
            .ShouldBe(["columns", "sqlsource 1.4\\u001B[3A\t0", Cli.Version]);

    [Fact]
    public void MadeBy_NoVersion_IsAnotherVersion() =>
        BundleErrors.MadeBy(null).ShouldBe("another version of sqlsource");

    [Fact]
    public void NoDescriber_EngineWithAControlCharacter_EscapesItAndKeepsTheTab()
    {
        var error = BundleErrors.NoDescriber(BundleInfos.Path, "x\u001b]0;pwned\tsql");

        error.Descriptor.Id.ShouldBe("SQLSRC291");
        error.Arguments.ToArray().ShouldBe(["x\\u001B]0;pwned\tsql"]);
    }
}
