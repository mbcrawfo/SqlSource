using System.Linq;
using Shouldly;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleErrorsTests
{
    private const string Path = "/work/report.zip";

    [Fact]
    public void CannotBeRead_ReasonWithALineBreak_IsAtThePathOnOneLine()
    {
        var error = BundleErrors.CannotBeRead(Path, "the zip\nis cut short");

        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Path.ShouldBe(Path);
        error.Arguments.ToArray().ShouldBe(["the zip is cut short"]);
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

    [Fact]
    public void MadeBy_NoVersion_IsAnotherVersion() =>
        BundleErrors.MadeBy(null).ShouldBe("another version of sqlsource");

    [Fact]
    public void NoDescriber_Engine_IsNamedOnOneLine()
    {
        var error = BundleErrors.NoDescriber(Path, "my\nsql");

        error.Descriptor.Id.ShouldBe("SQLSRC291");
        error.Arguments.ToArray().ShouldBe(["my sql"]);
    }
}
