// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the sidecars: which sidecars an edit makes the generator read again.
public sealed class CachingSidecarTests
{
    private readonly CachingScene _scene = new();

    [Fact]
    public void Run_SidecarEdited_ReadsOnlyThatSidecarAndParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var stale = Sidecars.Of(ToSqlFile(_scene.Users), Sidecars.Entry("GetUser", "stale"));
        var edited = new InMemoryAdditionalText(stale.Path, stale.Text);
        var result = Run(driver.ReplaceAdditionalText(_scene.UsersSidecar, edited), compilation);

        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.FileParseInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_SqlFileEdited_ReadsNoSidecar()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)["Users.sql"]
            .ShouldBe(IncrementalStepRunReason.Modified);
        Reasons<SidecarInput>(result, TrackingNames.SidecarInput, input => input.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    [Fact]
    public void Run_MethodBodyEdited_ReadsNoSidecar()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        Reasons<SidecarInput>(result, TrackingNames.SidecarInput, input => input.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Cached]);
        Reasons<EquatableArray<FileNeeds>>(result, TrackingNames.TypeNeeds, NeedsKey)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Checks(result).Values.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A sidecar with no .sql file beside it, which no type can claim.
    [Fact]
    public void Run_SidecarAddedForAFileNoTypeClaims_ReadsNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRunWithSidecars(compilation);

        var result = Run(
            driver.AddAdditionalTexts([new InMemoryAdditionalText("/app/Migrations/001_init.sql.json", "{\n")]),
            compilation
        );

        // The new sidecar is not listed, here or under SidecarFile: its input is null, so nothing is read for it, and
        // the driver tracks a step only when something it gave reaches an output.  The others are the inputs they
        // were.
        Reasons<SidecarInput>(result, TrackingNames.SidecarInput, input => input.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Cached]);
        result.Diagnostics.ShouldBeEmpty();
    }

    // Two items of one path, as a project that lists a sidecar itself beside the package's glob would give.  Each
    // item is an input of its own, and the collected value keeps the first, so every step after it sees the file once.
    [Fact]
    public void Run_SameSidecarListedTwice_KeepsOneSidecar()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var again = new InMemoryAdditionalText(
            _scene.UsersSidecar.Path,
            _scene.UsersSidecar.GetText(TestContext.Current.CancellationToken)!.ToString()
        );

        var result = GeneratorHarness
            .CreateDriver(
                [_scene.Users, _scene.Orders, _scene.UsersSidecar, _scene.OrdersSidecar, again],
                new TestOptionsProvider(OutputKind.Models, null, "postgres")
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken)
            .GetRunResult();

        var sidecars =
            (EquatableArray<SidecarFile>)Outputs(result, TrackingNames.Sidecars).ShouldHaveSingleItem().Value;
        sidecars.Select(file => file.SqlPath).ShouldBe(["app/Orders/Orders.sql", "app/Users/Users.sql"]);
    }
}
