using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using SqlSource.Generation.Chains;
using Xunit;

namespace SqlSource.Tests.Generation.Chains;

public sealed class BuildFoldersTests
{
    private static BuildFolders Folders { get; } =
        BuildFolders.Of("/repo/artifacts/obj/App/", "/repo/artifacts/bin/App/");

    [Fact]
    public void Read_BothProperties_GivesBothFoldersNormalised()
    {
        var folders = BuildFolders.Read(
            new Options(
                new Dictionary<string, string>
                {
                    ["build_property.SqlSourceIntermediateFolder"] = "/repo/artifacts/obj/App/",
                    ["build_property.SqlSourceOutputFolder"] = @"C:\repo\artifacts\bin\App\",
                }
            )
        );

        folders.ShouldBe(new BuildFolders("repo/artifacts/obj/App", "C:/repo/artifacts/bin/App"));
    }

    [Fact]
    public void Read_NoProperty_IsNone() => BuildFolders.Read(new Options([])).ShouldBeSameAs(BuildFolders.None);

    // MSBuild's NormalizeDirectory ends a full path with a separator.  The compiler cuts a value at a # or a ;, so
    // /repo/App#1/obj/ arrives as /repo/App and /repo/a/#b/obj/ as /repo/a/; the targets give no value for such a
    // folder, and a value that is not a rooted folder names none here either.
    [Theory]
    [InlineData("")]
    [InlineData("/repo/App")]
    [InlineData("obj/")]
    [InlineData("../obj/")]
    [InlineData("/")]
    [InlineData("/../")]
    public void Of_ValueThatIsNoFullFolder_NamesNoFolder(string value) =>
        BuildFolders.Of(value, value).ShouldBeSameAs(BuildFolders.None);

    [Fact]
    public void Of_OneFolder_KeepsIt()
    {
        BuildFolders.Of("/repo/obj/", null).ShouldBe(new BuildFolders("repo/obj", null));
        BuildFolders.Of(null, "/repo/bin/").ShouldBe(new BuildFolders(null, "repo/bin"));
    }

    [Theory]
    [InlineData("repo/artifacts/obj/App/debug/App.AssemblyInfo.cs")]
    [InlineData("REPO/artifacts/OBJ/app/x.cs")]
    [InlineData("repo/artifacts/bin/App/debug/Extra.cs")]
    public void Contains_PathUnderEitherFolder_IsTrue(string path) => Folders.Contains(path).ShouldBeTrue();

    [Theory]
    [InlineData("repo/artifacts/obj/Appx/x.cs")]
    [InlineData("repo/artifacts/objx/App/x.cs")]
    [InlineData("repo/artifacts/obj/App")]
    [InlineData("repo/artifacts/obj/x.cs")]
    [InlineData("repo/App/Sample.cs")]
    public void Contains_PathBesideOrAboveTheFolders_IsFalse(string path) => Folders.Contains(path).ShouldBeFalse();

    [Fact]
    public void Contains_NoFolder_IsFalse() => BuildFolders.None.Contains("repo/obj/x.cs").ShouldBeFalse();

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
