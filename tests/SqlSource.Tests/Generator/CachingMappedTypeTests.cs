// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the resolution of the mapped types: what an edit makes the generator resolve again.
public sealed class CachingMappedTypeTests
{
    private readonly CachingScene _scene = new();

    // A project whose property maps a type: the resolution runs on the new compilation, gives an equal value, and
    // nothing after it runs again.
    [Fact]
    public void Run_CSharpEdit_ResolvesTheMappedTypesAndNothingAfterThem()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(
            compilation,
            _scene.Users,
            new TestOptionsProvider(
                OutputKind.Sql,
                null,
                properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "uuid=System.Guid" }
            )
        );
        var before = Outputs(driver.GetRunResult(), TrackingNames.MappedTypes).ShouldHaveSingleItem().Value;
        ((EquatableArray<MappedType>)before).ShouldHaveSingleItem().Name.ShouldBe("global::System.Guid");

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.MappedTypeNames).ShouldBe([IncrementalStepRunReason.Cached]);
        var (types, reason) = Outputs(result, TrackingNames.MappedTypes).ShouldHaveSingleItem();
        reason.ShouldBe(IncrementalStepRunReason.Unchanged);
        types.ShouldBe(before);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_NoOverride_ResolvesNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);

        var result = _scene.FirstRun(OutputKind.Sql, compilation).GetRunResult();

        (
            (EquatableArray<string>)Outputs(result, TrackingNames.MappedTypeNames).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
        (
            (EquatableArray<MappedType>)Outputs(result, TrackingNames.MappedTypes).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
    }

    // The query's marker names a type.  An edit of its SQL parses the file again and leaves the names as they were.
    // The driver hands the generator a new compilation on every run, a .sql edit's too, so the resolution runs again
    // as the probe does, and gives an equal value: nothing after it runs.
    [Fact]
    public void Run_SqlEditThatLeavesTheMarkersAlone_ChangesNoMappedType()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- name: GetUser\n-- type-override: uuid = System.Guid\nSELECT 1;\n"
        );
        var driver = _scene.FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));

        var edited = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- name: GetUser\n-- type-override: uuid = System.Guid\nSELECT 1, 2;\n"
        );
        var result = Run(driver.ReplaceAdditionalText(users, edited), compilation);

        AllReasons(result, TrackingNames.ParsedFiles).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MappedTypeNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.SupportedFramework).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        ((EquatableArray<string>)Outputs(result, TrackingNames.MappedTypeNames).ShouldHaveSingleItem().Value).ShouldBe([
            "global::System.Guid",
        ]);
    }
}
