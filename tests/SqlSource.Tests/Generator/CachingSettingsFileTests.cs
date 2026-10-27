// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;

namespace SqlSource.Tests.Generator;

// The caching of the settings files: what an edit to a sqlsource.json, or one added, makes the generator read and
// parse again.  Only the dialect and keep-comments of a file's chain are inputs of its parse, so an edit that leaves
// them alone parses no file.
public sealed class CachingSettingsFileTests
{
    private readonly CachingScene _scene = new();

    private readonly InMemoryAdditionalText _usersSettings = new(
        "/app/Users/sqlsource.json",
        /*lang=json,strict*/
        """{"dialect":"mysql","inputModelSuffix":"In"}"""
    );

    private readonly InMemoryAdditionalText _ordersSettings = new(
        "/app/Orders/sqlsource.json", /*lang=json,strict*/
        """{"dialect":"mssql"}"""
    );

    [Fact]
    public void Run_SettingsFileEditChangesOneFoldersDialect_ParsesOnlyThatFoldersFiles()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _usersSettings, _ordersSettings);

        var edited = new InMemoryAdditionalText(
            _usersSettings.Path, /*lang=json,strict*/
            """{"dialect":"postgres","inputModelSuffix":"In"}"""
        );
        var result = Run(driver.ReplaceAdditionalText(_usersSettings, edited), compilation);

        // Only the edited settings file was read again: the other folder's and the project's, which gives the output,
        // were not.  Every file's input was resolved again from the new set, and only the input of the file in the
        // edited folder changed.
        SettingsFileReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_usersSettings.Path] = IncrementalStepRunReason.Modified,
                    [_ordersSettings.Path] = IncrementalStepRunReason.Cached,
                    [GeneratorHarness.ProjectSettingsPath] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.SettingsFiles).ShouldBe([IncrementalStepRunReason.Modified]);
        _scene.ShouldHaveReadOnly(result, _scene.Users, IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Run_SettingsFileEditLeavesTheStageOneValues_ParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _usersSettings, _ordersSettings);

        var edited = new InMemoryAdditionalText(
            _usersSettings.Path, /*lang=json,strict*/
            """{"dialect":"mysql","inputModelSuffix":"Args"}"""
        );
        var result = Run(driver.ReplaceAdditionalText(_usersSettings, edited), compilation);

        // The set changed, so every file's input was resolved again, and each came out equal.
        AllReasons(result, TrackingNames.SettingsFiles).ShouldBe([IncrementalStepRunReason.Modified]);
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Unchanged,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ParsedFile)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ParsedFiles).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    [Fact]
    public void Run_SettingsFileAdded_ParsesOnlyTheFilesItReaches()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _ordersSettings);

        var result = Run(driver.AddAdditionalTexts([_usersSettings]), compilation);

        // The added file alone is read: the other folder's and the project's, which gives the output, are not.
        SettingsFileReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_usersSettings.Path] = IncrementalStepRunReason.New,
                    [_ordersSettings.Path] = IncrementalStepRunReason.Cached,
                    [GeneratorHarness.ProjectSettingsPath] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        _scene.ShouldHaveReadOnly(result, _scene.Users, IncrementalStepRunReason.Unchanged);
    }

    // The edit adds no C# file, so the paths of the trees are equal and the settings files that a chain reaches are
    // not looked for again.  The properties are the same, so the build's folders are not read again either.
    [Fact]
    public void Run_CSharpEditThatAddsNoFile_ReachesNoSettingsFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _usersSettings, _ordersSettings);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.CompilePaths).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.BuildFolders).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ReachedSettingsFiles).ShouldBe([IncrementalStepRunReason.Cached]);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A folder of the build that holds no C# file of the scene: the folders are read again and changed, and the same
    // settings files are reached, so nothing is reported again and no file is parsed.  The folders of the build are a
    // property, an input of the build and no setting, and the output stays the project's settings file's.
    [Fact]
    public void Run_BuildFolderChanged_ReachesTheSameSettingsFiles()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _usersSettings, _ordersSettings);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    properties: new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/app/obj/" }
                )
            ),
            compilation
        );

        AllReasons(result, TrackingNames.BuildFolders).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.ReachedSettingsFiles).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.ParsedFile)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
    }

    // What the IDE does when any property or an .editorconfig changes: new options, and the same folders of the build.
    // The folders are read again and come out equal, and nothing after them runs.
    [Fact]
    public void Run_OptionsReplacedWithoutChangingTheBuildFolders_EmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _usersSettings, _ordersSettings);

        var result = Run(driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider()), compilation);

        AllReasons(result, TrackingNames.BuildFolders).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.ReachedSettingsFiles).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.FileParseInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // The counterpart of Run_OptionsReplacedWithoutChangingTheBuildFolders_EmitsNothingAgain for a settings file: what
    // the IDE does when one is saved without a change, a new text of the same value.  The file is read again and gives
    // an equal value, and nothing after it runs: the step named SettingsFile is the one after the filter of nulls,
    // which reports such a value as taken from the previous run.
    [Fact]
    public void Run_SettingsFileReplacedWithTheSameText_EmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _usersSettings, _ordersSettings);

        var again = new InMemoryAdditionalText(
            _usersSettings.Path,
            _usersSettings.GetText(TestContext.Current.CancellationToken)!.ToString()
        );
        var result = Run(driver.ReplaceAdditionalText(_usersSettings, again), compilation);

        SettingsFileReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_usersSettings.Path] = IncrementalStepRunReason.Cached,
                    [_ordersSettings.Path] = IncrementalStepRunReason.Cached,
                    [GeneratorHarness.ProjectSettingsPath] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.SettingsFiles).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.FileParseInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // The edit leaves the dialect alone, so no file is parsed again, and the file's new error is reported.
    [Fact]
    public void Run_SettingsFileEditedToAnError_ReportsItWithoutParsing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _usersSettings, _ordersSettings);

        var edited = new InMemoryAdditionalText(
            _usersSettings.Path, /*lang=json,strict*/
            """{"dialect":"mysql","inputModelSuffix":"In","bogus":1}"""
        );
        var result = Run(driver.ReplaceAdditionalText(_usersSettings, edited), compilation);

        AllReasons(result, TrackingNames.ParsedFile)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        // The same files are reached, and the new set is reported from.
        AllReasons(result, TrackingNames.ReachedSettingsFiles).ShouldBe([IncrementalStepRunReason.Unchanged]);
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("SQLSRC016");
        diagnostic.Location.GetLineSpan().Path.ShouldBe(_usersSettings.Path);
    }

    // Why each settings file was read, by its path.
    private static Dictionary<string, IncrementalStepRunReason> SettingsFileReasons(GeneratorDriverRunResult result) =>
        Reasons<ListedSettingsFile>(result, TrackingNames.SettingsFile, file => file.Path);
}
