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

    // MSBuild's NormalizeDirectory ends a full path with a separator.  The compiler cuts a value at a # or a ;, so an
    // unescaped /repo/App#1/obj/ would arrive as /repo/App; the targets escape both, and a value that is not a rooted
    // folder, a raw cut value among them, names none here.
    [Theory]
    [InlineData("")]
    [InlineData("/repo/App")]
    [InlineData("obj/")]
    [InlineData("../obj/")]
    [InlineData("/")]
    [InlineData("/../")]
    public void Of_ValueThatIsNoFullFolder_NamesNoFolder(string value) =>
        BuildFolders.Of(value, value).ShouldBeSameAs(BuildFolders.None);

    // The targets write %, # and ; as %25, %23 and %3B, % first, so that the compiler does not cut the value.  A folder
    // named with a literal %23 is written %2523, and is decoded to %23, not to #.
    [Theory]
    [InlineData("/repo/My%23App/artifacts/obj/App/", "repo/My#App/artifacts/obj/App")]
    [InlineData("/repo/a%3Bb/obj/", "repo/a;b/obj")]
    [InlineData("/repo/a%2523b/obj/", "repo/a%23b/obj")]
    [InlineData("/repo/100%25/obj/", "repo/100%/obj")]
    [InlineData("/repo/a%23%3B%25b/obj/", "repo/a#;%b/obj")]
    [InlineData(@"C:\repo\a%23b\obj\", "C:/repo/a#b/obj")]
    public void Of_EscapedFolder_IsDecodedToTheRealFolder(string value, string expected) =>
        BuildFolders.Of(value, value).ShouldBe(new BuildFolders(expected, expected));

    // Only the three escapes the targets write are decoded; any other % stays as it is.
    [Theory]
    [InlineData("/repo/a%zzb/obj/", "repo/a%zzb/obj")]
    [InlineData("/repo/a%20b/obj/", "repo/a%20b/obj")]
    [InlineData("/repo/a%3/obj/", "repo/a%3/obj")]
    [InlineData("/repo/a%/obj/", "repo/a%/obj")]
    public void Of_OtherPercentSign_IsKept(string value, string expected) =>
        BuildFolders.Of(value, null).ShouldBe(new BuildFolders(expected, null));

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
