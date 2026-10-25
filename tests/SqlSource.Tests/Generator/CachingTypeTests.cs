// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;

namespace SqlSource.Tests.Generator;

// The caching of the typing of the queries: which files an edit makes the generator type again.
public sealed class CachingTypeTests
{
    private readonly CachingScene _scene = new();

    // The probe and the resolution run on the new compilation and give equal values, so no file is typed again.
    [Fact]
    public void Run_CSharpEdit_TypesNoQuery()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null));
        Outputs(driver.GetRunResult(), TrackingNames.FileTypes)
            .Select(output => (FileTypes)output.Value)
            .ShouldAllBe(types => types.Queries.Count == 1);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // The edit makes the entry of Users stale, so Users has no types now, and Orders is not typed again.
    [Fact]
    public void Run_SqlFileEdited_TypesOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null));

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        // Every file gathered its input again, since the parsed files changed, and Orders' is equal.
        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql").Queries.ShouldBeEmpty();
    }

    // The same edit under mssql: Users has no entry now, and Orders is not typed again.
    [Fact]
    public void Run_SqlFileEditedUnderMssql_TypesOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [SqlClientReferences.SqlClient7]);
        var driver = _scene.FirstRunWithSqlServerSidecars(compilation);

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql").Queries.ShouldBeEmpty();
    }

    // The new entry of Users has a column of text, and Orders is not typed again.
    [Fact]
    public void Run_SidecarEdited_TypesOnlyItsFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null), usersSidecar);

        var edited = TypedSidecar(_scene.Users, "GetUser", "text");
        var result = Run(driver.ReplaceAdditionalText(usersSidecar, edited), compilation);

        // Orders' check was taken from the previous run, and so was every other input of its types.
        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("string");
    }

    // Each file's map of overrides comes from the property, so every file is typed again, and each column of int4
    // now has the property's type.
    [Fact]
    public void Run_TypeOverridesPropertyChanged_TypesEveryFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null));

        var result = Run(driver.WithUpdatedAnalyzerConfigOptions(OptionsWithTypeOverrides("int4=long")), compilation);

        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("long");
    }

    // The probe and the resolution of the property's one type run on the new compilation and give equal values, so
    // a project with a type override types no file again either.
    [Fact]
    public void Run_MethodBodyEditedWithATypeOverride_TypesNoQuery()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithTypes(compilation, OptionsWithTypeOverrides("int4=long"));

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.ResolvedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("long");
    }

    // The property maps int4 to a type the project does not have, so Users, whose column is of int4, is SQLSRC250.
    // A new file declares the type: its resolution changes, its converters, none, do not, and Users is typed again
    // without the error.  Every file
    // with a usable entry carries the resolved types, so Orders, whose column is of text, is typed again too, and
    // gives an equal value, which nothing after it reads again.
    [Fact]
    public void Run_CSharpEditAddsTheMappedType_TypesTheFileThatUsesItAnew()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = GeneratorHarness
            .CreateDriver(
                [
                    _scene.Users,
                    _scene.Orders,
                    TypedSidecar(_scene.Users, "GetUser", "int4"),
                    TypedSidecar(_scene.Orders, "GetOrder", "text"),
                ],
                OptionsWithTypeOverrides("int4=MyApp.Missing")
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC250"]);

        var result = Run(
            driver,
            compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    "namespace MyApp;\n\npublic sealed class Missing;\n",
                    GeneratorHarness.ParseOptions,
                    "/app/MyApp/Missing.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
        );

        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MappedTypeConverters).ShouldBe([IncrementalStepRunReason.Unchanged]);
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::MyApp.Missing");
    }

    // Without Npgsql, date has no C# type, SQLSRC249.  The reference changes the probe, and every file is typed
    // again, with the type of Npgsql 10.
    [Fact]
    public void Run_NpgsqlReferenceAdded_TypesEveryFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = GeneratorHarness
            .CreateDriver(
                [
                    _scene.Users,
                    _scene.Orders,
                    TypedSidecar(_scene.Users, "GetUser", "date"),
                    TypedSidecar(_scene.Orders, "GetOrder", "date"),
                ],
                OptionsWithTypeOverrides(null)
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC249", "SQLSRC249"]);

        var result = Run(driver, compilation.AddReferences(NpgsqlReferences.Npgsql10));

        AllReasons(result, TrackingNames.NpgsqlProbe).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::System.DateOnly");
    }
}
