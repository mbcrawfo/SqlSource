// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The settings files a file reaches by the folders the build gives the generator in SqlSourceBuildFolders, a build
// input and no setting: a C# file under such a folder reaches none, a C# file under a folder the build does not give
// climbs to the file above the project's root, and a .sql file reaches its chain wherever it is.  Split from
// SettingsFileTests, which holds which level of the settings wins.
public sealed class SettingsFileBuildFoldersTests
{
    // The attribute of the assembly that the SDK writes under obj, here under the intermediate folder of the artifacts
    // layout, outside the project's folder /repo/App.
    private const string ArtifactsAssemblyInfo = "/repo/artifacts/obj/App/debug/App.AssemblyInfo.cs";

    // What the targets give a project in /repo/App whose IntermediateOutputPath is /repo/build/obj/Debug/, whose OutDir
    // is /repo/out/Debug/, and which writes the compiler's generated files to /repo/gen/: the base folders, the
    // intermediate folder, the output folder and the folder of the generated files, each after a |.
    private const string AllFoldersOfTheBuild =
        "|/repo/App/obj/|/repo/build/obj/Debug/|/repo/App/bin/|/repo/out/Debug/|/repo/gen/";

    // With obj outside the project, as the artifacts layout has it, the SDK's own Compile items are under
    // /repo/artifacts, whose chain climbs past the project's root file to the broken file of the repository.  The
    // build gives the generator the intermediate folder, and a compile path under it reaches nothing.
    [Fact]
    public void Run_CompileItemUnderTheIntermediateFolder_ReachesNoSettingsFile()
    {
        var run = RunOutsideTheProject(
            ArtifactsAssemblyInfo,
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/obj/App/" }
        );

        run.ShouldBeClean();
    }

    [Fact]
    public void Run_CompileItemUnderTheOutputFolder_ReachesNoSettingsFile()
    {
        var run = RunOutsideTheProject(
            "/repo/artifacts/bin/App/debug/Extra.cs",
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/bin/App/" }
        );

        run.ShouldBeClean();
    }

    // Each folder the build writes C# trees or its outputs into, outside the project: IntermediateOutputPath moved out
    // of obj, where the SDK's items and the compiler's own output are, OutDir, and the folder the compiler writes its
    // generated files to under EmitCompilerGeneratedFiles, where it roots their trees.  The property is what the
    // targets give for such a project, the base folders being in the project.
    [Theory]
    [InlineData("/repo/build/obj/Debug/App.AssemblyInfo.cs")]
    [InlineData("/repo/build/obj/Debug/SqlSource/SqlSource.SqlSourceGenerator/SqlSourceGenerateAttribute.g.cs")]
    [InlineData("/repo/out/Debug/Extra.cs")]
    [InlineData("/repo/gen/SqlSource/SqlSource.SqlSourceGenerator/SqlSourceGenerateAttribute.g.cs")]
    public void Run_CompileItemUnderAFolderOfTheBuildOutsideTheProject_ReachesNoSettingsFile(string compilePath)
    {
        var run = RunOutsideTheProject(
            compilePath,
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = AllFoldersOfTheBuild }
        );

        run.ShouldBeClean();
    }

    // The same, with the folder that holds the tree left out of the property: the tree reaches the file above the root.
    [Theory]
    [InlineData("/repo/build/obj/Debug/App.AssemblyInfo.cs", "|/repo/build/obj/Debug/")]
    [InlineData("/repo/out/Debug/Extra.cs", "|/repo/out/Debug/")]
    [InlineData("/repo/gen/SqlSource/SqlSource.SqlSourceGenerator/SqlSourceGenerateAttribute.g.cs", "|/repo/gen/")]
    public void Run_CompileItemUnderAFolderTheBuildDoesNotGive_ReachesTheFileAboveTheRoot(
        string compilePath,
        string folder
    )
    {
        var run = RunOutsideTheProject(
            compilePath,
            new Dictionary<string, string?>
            {
                ["SqlSourceBuildFolders"] = AllFoldersOfTheBuild.Replace(folder, "", StringComparison.Ordinal),
            }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // The same project without the build's folders: the SDK's item reaches the file above the project's root.
    [Fact]
    public void Run_CompileItemOutsideTheProjectWithoutTheBuildFolders_ReachesTheFileAboveTheRoot()
    {
        var run = RunOutsideTheProject(ArtifactsAssemblyInfo, null);

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // A package that ships C# files as content, as Polyfill does, adds Compile items under the folder NuGet restored it
    // to, the global packages folder in the home folder or a fallback folder, whose chain never meets the project's
    // root file.  The targets give the generator those folders with the build's, and a compile path under one reaches
    // nothing, so the broken file of the home folder is never checked.
    [Theory]
    [InlineData("/home/me/.nuget/packages/polyfill/7.12.0/contentFiles/cs/net10.0/Polyfill/Polyfill.cs")]
    [InlineData("/home/me/fallback/nullable/1.3.1/contentFiles/cs/netstandard2.0/Nullable/AllowNullAttribute.cs")]
    public void Run_CompileItemOfAPackage_ReachesNoSettingsFile(string compilePath)
    {
        var run = RunWithAPackageFile(
            compilePath,
            "|/home/me/repo/App/obj/|/home/me/.nuget/packages/|/home/me/fallback/"
        );

        run.ShouldBeClean();
    }

    // The same, with the package folders left out of the property: the package's file reaches the home folder's file.
    [Fact]
    public void Run_CompileItemOfAPackageWithoutThePackageFolders_ReachesTheFileAboveTheRoot()
    {
        var run = RunWithAPackageFile(
            "/home/me/.nuget/packages/polyfill/7.12.0/contentFiles/cs/net10.0/Polyfill/Polyfill.cs",
            "|/home/me/repo/App/obj/"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /home/me/sqlsource.json(1,");
    }

    // A folder is compared segment by segment, so objx is not under obj.
    [Fact]
    public void Run_CompileItemInAFolderThatOnlyStartsLikeTheIntermediateFolder_ReachesTheFileAboveTheRoot()
    {
        var run = RunOutsideTheProject(
            "/repo/artifacts/objx/App/debug/App.AssemblyInfo.cs",
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/obj/" }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // The targets escape a folder's # and ;, at which the compiler would cut the value, and the generator decodes them,
    // so a project under /repo/My#App keeps its build's files out.  The project's root file gives the output, since it
    // hides a project's file of the run.
    [Fact]
    public void Run_CompileItemUnderAnEscapedIntermediateFolder_ReachesNoSettingsFile()
    {
        var run = GeneratorHarness.Run(
            null,
            [
                new SourceFile("/repo/My#App/App/Sample.cs", GeneratorHarness.SampleSource(null)),
                new SourceFile(
                    "/repo/My#App/artifacts/obj/App/debug/App.AssemblyInfo.cs",
                    "[assembly: System.Reflection.AssemblyMetadata(\"Built\", \"yes\")]\n"
                ),
            ],
            [
                GeneratorHarness.SettingsFile("/repo/sqlsource.json", "{"),
                GeneratorHarness.SettingsFile(
                    "/repo/My#App/App/sqlsource.json", /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile("/repo/My#App/App/Q.sql", SqlTexts.GetUser),
            ],
            properties: new Dictionary<string, string?>
            {
                ["SqlSourceBuildFolders"] = "/repo/My%23App/artifacts/obj/App/",
            }
        );

        run.ShouldBeClean();
    }

    // A value the compiler cut at a # or a ;, as an unescaped /repo/App#1/obj/ would arrive, /repo/App.  A value that
    // does not end with a separator is no folder of the build, so the project's own C# file still reaches its folder's
    // file.
    [Fact]
    public void Run_IntermediateFolderCutShort_LeavesNoCompileItemOut()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile("/repo/App/Sample.cs", GeneratorHarness.SampleSource(null)),
                new SourceFile(
                    "/repo/App/Code/Helpers.cs",
                    "[assembly: System.Reflection.AssemblyMetadata(\"Helpers\", \"none\")]\n"
                ),
            ],
            [
                GeneratorHarness.SettingsFile(
                    "/repo/App/Code/sqlsource.json", /*lang=json,strict*/
                    """{"bogus":1}"""
                ),
                new SqlFile("/repo/App/Q.sql", SqlTexts.GetUser),
            ],
            properties: new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/App" }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC016 /repo/App/Code/sqlsource.json(1,");
    }

    // A .sql file is an item of the project wherever it is, so one under the intermediate folder still reaches the
    // files above it.  The project's root file gives the output, since it hides a project's file of the run.
    [Fact]
    public void Run_SqlFileUnderTheIntermediateFolder_ReachesTheFileAboveTheRoot()
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile("/repo/App/Sample.cs", GeneratorHarness.SampleSource(null))],
            [
                GeneratorHarness.SettingsFile("/repo/sqlsource.json", "{"),
                GeneratorHarness.SettingsFile(
                    "/repo/App/sqlsource.json", /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile("/repo/App/Q.sql", SqlTexts.GetUser),
                new SqlFile("/repo/artifacts/obj/App/Generated.sql", SqlTexts.GetUser),
            ],
            properties: new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/obj/App/" }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // The type and its .sql file in /repo/App, whose settings file is a root that gives the output, since it hides a
    // project's file of the run, and a broken settings file at the repository's root, /repo, with one more C# file at
    // the path given, under the properties given.
    private static GeneratorRun RunOutsideTheProject(
        string compilePath,
        IReadOnlyDictionary<string, string?>? properties
    ) =>
        GeneratorHarness.Run(
            null,
            [
                new SourceFile("/repo/App/Sample.cs", GeneratorHarness.SampleSource(null)),
                new SourceFile(compilePath, "[assembly: System.Reflection.AssemblyMetadata(\"Built\", \"yes\")]\n"),
            ],
            [
                GeneratorHarness.SettingsFile("/repo/sqlsource.json", "{"),
                GeneratorHarness.SettingsFile(
                    "/repo/App/sqlsource.json", /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile("/repo/App/Q.sql", SqlTexts.GetUser),
            ],
            properties: properties
        );

    // A project in /home/me/repo/App, whose root file gives the output, with a C# file of a package at the path given
    // and a broken file in the home folder above both, run with the build's folders given.
    private static GeneratorRun RunWithAPackageFile(string compilePath, string buildFolders) =>
        GeneratorHarness.Run(
            null,
            [
                new SourceFile("/home/me/repo/App/Sample.cs", GeneratorHarness.SampleSource(null)),
                new SourceFile(compilePath, "[assembly: System.Reflection.AssemblyMetadata(\"Package\", \"yes\")]\n"),
            ],
            [
                GeneratorHarness.SettingsFile("/home/me/sqlsource.json", "{"),
                GeneratorHarness.SettingsFile(
                    "/home/me/repo/App/sqlsource.json", /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile("/home/me/repo/App/Q.sql", SqlTexts.GetUser),
            ],
            properties: new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = buildFolders }
        );
}
