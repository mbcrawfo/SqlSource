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

        // Only the edited settings file was read again.  Every file's input was resolved again from the new set, and
        // only the input of the file in the edited folder changed.
        SettingsFileReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_usersSettings.Path] = IncrementalStepRunReason.Modified,
                    [_ordersSettings.Path] = IncrementalStepRunReason.Cached,
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
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.ParsedFiles).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    [Fact]
    public void Run_SettingsFileAdded_ParsesOnlyTheFilesItReaches()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSettingsFiles(compilation, _ordersSettings);

        var result = Run(driver.AddAdditionalTexts([_usersSettings]), compilation);

        SettingsFileReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_usersSettings.Path] = IncrementalStepRunReason.New,
                    [_ordersSettings.Path] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        _scene.ShouldHaveReadOnly(result, _scene.Users, IncrementalStepRunReason.Unchanged);
    }

    // Why each settings file was read, by its path.
    private static Dictionary<string, IncrementalStepRunReason> SettingsFileReasons(GeneratorDriverRunResult result) =>
        Reasons<ListedSettingsFile>(result, TrackingNames.SettingsFile, file => file.Path);
}
