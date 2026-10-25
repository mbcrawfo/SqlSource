// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Types;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the resolution of the mapped types: what an edit makes the generator resolve again, and what of the
// types and their converters it then hands on.
public sealed class CachingMappedTypeTests
{
    // A library whose mapped type has two converters.
    private const string LibrarySource = """
        namespace Lib;

        public readonly record struct Email(string Value)
        {
            [SqlSource.SqlSourceConverter]
            public static Email From(string v) => new(v);

            [SqlSource.SqlSourceConverter]
            public static string To(Email e) => e.Value;
        }
        """;

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
        AllReasons(result, TrackingNames.ResolvedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        var (types, reason) = Outputs(result, TrackingNames.MappedTypes).ShouldHaveSingleItem();
        reason.ShouldBe(IncrementalStepRunReason.Cached);
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
        (
            (EquatableArray<Converter>)Outputs(result, TrackingNames.MappedTypeConverters).ShouldHaveSingleItem().Value
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
        AllReasons(result, TrackingNames.ResolvedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Cached]);
        ((EquatableArray<string>)Outputs(result, TrackingNames.MappedTypeNames).ShouldHaveSingleItem().Value).ShouldBe([
            "global::System.Guid",
        ]);
    }

    // The property maps a type of a library with converters.  An edit of a comment in the project's source resolves
    // the type and reads its converters again, equal, so neither they nor the project's converters run again.
    [Fact]
    public void Run_EditOfACommentOutsideTheMappedType_LeavesMappedTypeConvertersUnchanged()
    {
        var compilation = GeneratorHarness.CreateCompilation(
            Sources,
            references: [GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibrarySource)]
        );
        var driver = _scene.FirstRun(compilation, _scene.Users, EmailOverride());
        var before = Outputs(driver.GetRunResult(), TrackingNames.MappedTypeConverters).ShouldHaveSingleItem().Value;
        ((EquatableArray<Converter>)before)
            .Select(converter => converter.Method)
            .ShouldBe(["global::Lib.Email.To", "global::Lib.Email.From"]);

        var result = Run(driver, WithUsersSource(compilation, UsersSource + "\n// A comment.\n"));

        AllReasons(result, TrackingNames.ResolvedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Cached]);
        var (converters, reason) = Outputs(result, TrackingNames.MappedTypeConverters).ShouldHaveSingleItem();
        reason.ShouldBe(IncrementalStepRunReason.Cached);
        converters.ShouldBe(before);
        AllReasons(result, TrackingNames.Converters).ShouldBe([IncrementalStepRunReason.Cached]);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A converter of the library changes, and the type it is on does not: the converters run again and the mapped
    // types, which the models read, give an equal value.
    [Fact]
    public void Run_ConverterOfAReferenceChanged_LeavesMappedTypesUnchanged()
    {
        var library = GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibrarySource);
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [library]);
        var driver = _scene.FirstRun(compilation, _scene.Users, EmailOverride());

        var changed = GeneratorHarness.CreateReference(
            OutputKind.Sql,
            "Lib",
            LibrarySource.Replace("public static string To(", "public static string ToText(", StringComparison.Ordinal)
        );
        var result = Run(driver, compilation.ReplaceReference(library, changed));

        AllReasons(result, TrackingNames.ResolvedTypes).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MappedTypeConverters).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.Converters).ShouldBe([IncrementalStepRunReason.Modified]);
        ((EquatableArray<Converter>)Outputs(result, TrackingNames.MappedTypeConverters).ShouldHaveSingleItem().Value)
            .Select(converter => converter.Method)
            .ShouldBe(["global::Lib.Email.ToText", "global::Lib.Email.From"]);
    }

    private static TestOptionsProvider EmailOverride() =>
        new(
            OutputKind.Sql,
            null,
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "text=Lib.Email" }
        );

    // The compilation with UserQueries' file replaced by the source.
    private static Compilation WithUsersSource(Compilation compilation, string source) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
            CSharpSyntaxTree.ParseText(
                source,
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
}
