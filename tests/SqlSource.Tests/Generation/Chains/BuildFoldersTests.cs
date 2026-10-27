using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using Xunit;

namespace SqlSource.Tests.Generation.Chains;

public sealed class BuildFoldersTests
{
    private static BuildFolders Folders { get; } = BuildFolders.Of("/repo/artifacts/obj/App/|/repo/artifacts/bin/App/");

    [Fact]
    public void Read_Property_GivesEachFolderNormalised()
    {
        var folders = BuildFolders.Read(
            new Options(
                new Dictionary<string, string>
                {
                    ["build_property.SqlSourceBuildFolders"] = @"/repo/artifacts/obj/App/|C:\repo\artifacts\bin\App\",
                }
            )
        );

        folders.ShouldBe(Of("repo/artifacts/obj/App", "C:/repo/artifacts/bin/App"));
    }

    [Fact]
    public void Read_NoProperty_IsNone() => BuildFolders.Read(new Options([])).ShouldBeSameAs(BuildFolders.None);

    // MSBuild's NormalizeDirectory ends a full path with a separator.  The compiler cuts a value at a # or a ;, so an
    // unescaped /repo/App#1/obj/ would arrive as /repo/App; the targets escape both, and a value that is not a rooted
    // folder, a raw cut value among them, names none here.
    [Theory]
    [InlineData("")]
    [InlineData("|")]
    [InlineData("/repo/App")]
    [InlineData("obj/")]
    [InlineData("../obj/")]
    [InlineData("/")]
    [InlineData("/../")]
    public void Of_ValueThatIsNoFullFolder_NamesNoFolder(string value) =>
        BuildFolders.Of(value).ShouldBeSameAs(BuildFolders.None);

    [Fact]
    public void Of_Null_IsNone() => BuildFolders.Of(null).ShouldBeSameAs(BuildFolders.None);

    // The targets join every folder of the build, in a fixed order, with a | before each.
    [Fact]
    public void Of_SeveralEntries_GivesEachInOrder() =>
        BuildFolders
            .Of("|/repo/App/obj/|/repo/App/obj/Debug/net10.0/|/repo/App/bin/|/repo/App/bin/Debug/net10.0/|/repo/gen/")
            .ShouldBe(
                Of(
                    "repo/App/obj",
                    "repo/App/obj/Debug/net10.0",
                    "repo/App/bin",
                    "repo/App/bin/Debug/net10.0",
                    "repo/gen"
                )
            );

    // An empty entry, a base folder with no value, is left out, and so is an entry that is not a full folder, alone.
    [Theory]
    [InlineData("/repo/obj/||/repo/bin/")]
    [InlineData("/repo/obj/|/repo/App|/repo/bin/")]
    [InlineData("/repo/obj/|obj/|/repo/bin/")]
    [InlineData("/repo/obj/|/repo/bin/|/repo/App")]
    public void Of_EntryThatIsNoFolder_IsLeftOutAndTheOthersApply(string value) =>
        BuildFolders.Of(value).ShouldBe(Of("repo/obj", "repo/bin"));

    // The targets write %, |, # and ; as %25, %7C, %23 and %3B, % first, so that the compiler does not cut the value
    // and a | of a folder's name does not part it.  A folder named with a literal %23 is written %2523, and is decoded
    // to %23, not to #.
    [Theory]
    [InlineData("/repo/My%23App/artifacts/obj/App/", "repo/My#App/artifacts/obj/App")]
    [InlineData("/repo/a%3Bb/obj/", "repo/a;b/obj")]
    [InlineData("/repo/a%7Cb/obj/", "repo/a|b/obj")]
    [InlineData("/repo/a%7cb/obj/", "repo/a|b/obj")]
    [InlineData("/repo/a%2523b/obj/", "repo/a%23b/obj")]
    [InlineData("/repo/a%257Cb/obj/", "repo/a%7Cb/obj")]
    [InlineData("/repo/100%25/obj/", "repo/100%/obj")]
    [InlineData("/repo/a%23%3B%7C%25b/obj/", "repo/a#;|%b/obj")]
    [InlineData(@"C:\repo\a%23b\obj\", "C:/repo/a#b/obj")]
    public void Of_EscapedFolder_IsDecodedToTheRealFolder(string value, string expected) =>
        BuildFolders.Of(value).ShouldBe(Of(expected));

    // A | of a folder's name is escaped, so it parts no entry.
    [Fact]
    public void Of_EscapedDelimiter_PartsNoEntry() =>
        BuildFolders.Of("/repo/a%7Cb/obj/|/repo/bin/").ShouldBe(Of("repo/a|b/obj", "repo/bin"));

    // Only the four escapes the targets write are decoded; any other % stays as it is.
    [Theory]
    [InlineData("/repo/a%zzb/obj/", "repo/a%zzb/obj")]
    [InlineData("/repo/a%20b/obj/", "repo/a%20b/obj")]
    [InlineData("/repo/a%3/obj/", "repo/a%3/obj")]
    [InlineData("/repo/a%/obj/", "repo/a%/obj")]
    public void Of_OtherPercentSign_IsKept(string value, string expected) =>
        BuildFolders.Of(value).ShouldBe(Of(expected));

    [Theory]
    [InlineData("repo/artifacts/obj/App/debug/App.AssemblyInfo.cs")]
    [InlineData("REPO/artifacts/OBJ/app/x.cs")]
    [InlineData("repo/artifacts/bin/App/debug/Extra.cs")]
    public void Contains_PathUnderAnyFolder_IsTrue(string path) => Folders.Contains(path).ShouldBeTrue();

    [Theory]
    [InlineData("repo/artifacts/obj/Appx/x.cs")]
    [InlineData("repo/artifacts/objx/App/x.cs")]
    [InlineData("repo/artifacts/obj/App")]
    [InlineData("repo/artifacts/obj/x.cs")]
    [InlineData("repo/App/Sample.cs")]
    public void Contains_PathBesideOrAboveTheFolders_IsFalse(string path) => Folders.Contains(path).ShouldBeFalse();

    [Fact]
    public void Contains_NoFolder_IsFalse() => BuildFolders.None.Contains("repo/obj/x.cs").ShouldBeFalse();

    [Fact]
    public void ReachingPaths_PathsUnderAFolder_AreLeftOut() =>
        BuildFolders
            .Of("|/repo/App/obj/|")
            .ReachingPaths(["/repo/App/A.cs", "/repo/App/obj/G.cs", "/repo/App/objects/B.cs"])
            .ShouldBe([SqlPath.Normalize("/repo/App/A.cs"), SqlPath.Normalize("/repo/App/objects/B.cs")]);

    // The test driver sets no base folder for generated files, so the attribute's path is relative.
    [Fact]
    public void ReachingPaths_PathThatIsNotRooted_IsLeftOut() =>
        BuildFolders
            .None.ReachingPaths(["Gen/Attribute.g.cs", "/repo/A.cs"])
            .ShouldBe([SqlPath.Normalize("/repo/A.cs")]);

    [Fact]
    public void ReachingPaths_PathsWithBackslashes_AreNormalised() =>
        BuildFolders.None.ReachingPaths([@"C:\repo\A.cs"]).ShouldBe([SqlPath.Normalize(@"C:\repo\A.cs")]);

    private static BuildFolders Of(params string[] folders) => new(TestModels.Array(folders));

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
