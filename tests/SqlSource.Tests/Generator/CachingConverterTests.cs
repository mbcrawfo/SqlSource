// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Converters;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;

namespace SqlSource.Tests.Generator;

// The caching of the project's converters: what an edit to a converter, or to the code beside it, makes the generator
// find, plan and write again.  UserQueries and OrderQueries are in the namespace App and at CodeGen under postgres, so
// each type has one method, in its extension class.  Users overrides int4 with App.UserId, whose converters are on
// App.Conversions, so UserQueries' method reads its column through ToUserId; Orders has no override.
public sealed class CachingConverterTests
{
    private const string UserMethods = "App.UserQueries-Methods.g.cs";

    private const string OrderMethods = "App.OrderQueries-Methods.g.cs";

    private const string ConversionsPath = "/app/Conversions.cs";

    // UserId's converters come before UserId, so that an edit of UserId moves no converter.
    private const string ConversionsSource = """
        using SqlSource;

        namespace App;

        public static class Conversions
        {
            [SqlSourceConverter]
            public static UserId ToUserId(int value) => new(value);

            [SqlSourceConverter]
            public static int FromUserId(UserId id) => id.Value;
        }

        public readonly record struct UserId(int Value);
        """;

    private const string LastConverter = "public static int FromUserId(UserId id) => id.Value;";

    private const StringComparison Ordinal = StringComparison.Ordinal;

    private const string OrderIdConverter = "public static OrderId ToOrderId(long value) => new(value);";

    private readonly CachingScene _scene = new();

    // Users with the override of int4.
    private readonly InMemoryAdditionalText _users;

    public CachingConverterTests()
    {
        _users = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- type-override: int4=App.UserId\n-- name: GetUser\nSELECT 1;\n"
        );
    }

    // The body of the last converter changes, and its name keeps its place, so the transform gives an equal record,
    // and nothing after it runs again.
    [Fact]
    public void Run_EditOfAConvertersBody_LeavesEverythingUnchanged()
    {
        var compilation = Compilation(ConversionsSource);
        var driver = FirstRun(compilation);

        var result = Run(
            driver,
            WithConversions(compilation, ConversionsSource.Replace("=> id.Value;", "=> id.Value + 0;", Ordinal))
        );

        result.Diagnostics.ShouldBeEmpty();
        ProjectConverterReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["global::App.Conversions.ToUserId"] = IncrementalStepRunReason.Unchanged,
                    ["global::App.Conversions.FromUserId"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Converters).ShouldBe([IncrementalStepRunReason.Cached]);
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(Expect(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached), ignoreOrder: true);
        ShouldAllBeCached(OutputReasons(result));
    }

    // The converter that UserQueries' method reads its column through is renamed, so the methods of UserQueries are
    // planned and written again, with the new name, and OrderQueries' input is gathered again, equal.  No file is
    // typed again, and no model is read or written again.
    [Fact]
    public void Run_EditOfAConvertersSignature_RunsTheMethodsOfTheTypeThatUsesIt()
    {
        var compilation = Compilation(ConversionsSource);
        var driver = FirstRun(compilation);
        MethodsOf(driver.GetRunResult(), UserMethods).ShouldContain("global::App.Conversions.ToUserId(");

        var result = Run(
            driver,
            WithConversions(
                compilation,
                ConversionsSource.Replace("UserId ToUserId(int value)", "UserId FromDatabase(int value)", Ordinal)
            )
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.Converters).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(Expect(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Unchanged), ignoreOrder: true);
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(Expect(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached), ignoreOrder: true);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(Expect(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached), ignoreOrder: true);
        AllReasons(result, TrackingNames.TypedFile)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.FileModels)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ModelOutput)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        var methods = MethodsOf(result, UserMethods);
        methods.ShouldContain("global::App.Conversions.FromDatabase(");
        methods.ShouldNotContain("ToUserId");
    }

    // UserId, the type the override chose, gains a member, and has no converter of its own.  The converters of its
    // file are read again, equal, and the type is resolved again, equal, so no type's methods run again.
    [Fact]
    public void Run_EditOfATypeWithNoConverter_RunsNothingOfTheMethods()
    {
        var compilation = Compilation(ConversionsSource);
        var driver = FirstRun(compilation);

        var result = Run(
            driver,
            WithConversions(
                compilation,
                ConversionsSource.Replace(
                    "public readonly record struct UserId(int Value);",
                    "public readonly record struct UserId(int Value)\n{\n    public bool IsEmpty => Value == 0;\n}",
                    Ordinal
                )
            )
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.ProjectConverters)
            .ShouldBe([IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.ResolvedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.Converters).ShouldBe([IncrementalStepRunReason.Cached]);
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(Expect(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached), ignoreOrder: true);
        ShouldAllBeCached(OutputReasons(result));
    }

    // A converter of OrderId, which no override chose, is added after the others: the project's converters change,
    // and no type's slice of them does.
    [Fact]
    public void Run_NewConverterOfAnUnusedType_LeavesMethodsInputUnchanged()
    {
        var compilation = Compilation(ConversionsSource);
        var driver = FirstRun(compilation);

        var result = Run(
            driver,
            WithConversions(
                compilation,
                ConversionsSource.Replace(
                    LastConverter,
                    LastConverter + "\n\n    [SqlSourceConverter]\n    " + OrderIdConverter,
                    Ordinal
                ) + "\n\npublic readonly record struct OrderId(long Value);\n"
            )
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.Converters).ShouldBe([IncrementalStepRunReason.Modified]);
        ConvertersOf(result).Select(converter => converter.Method).ShouldContain("global::App.Conversions.ToOrderId");
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                Expect(IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Unchanged),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(Expect(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached), ignoreOrder: true);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(Expect(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached), ignoreOrder: true);
    }

    // Conversions has a method marked as a converter that returns nothing, SQLSRC277 at its name.  A line added above
    // the class moves every converter down a line: their locations change, and SQLSRC277 is reported at the new line,
    // while each type's slice, which carries no location, stays equal, and no method is planned or written again.
    [Fact]
    public void Run_EditOfAConverterAboveAnother_MovesOnlyTheirDiagnostics()
    {
        var source = ConversionsSource.Replace(
            LastConverter,
            LastConverter + "\n\n    [SqlSourceConverter]\n    public static void Log(string text) { }",
            Ordinal
        );
        var compilation = Compilation(source);
        var driver = _scene.FirstRunOfMethodsWith(compilation, _users);
        var first = driver.GetRunResult();
        var before = first.Diagnostics.ShouldHaveSingleItem();
        before.Id.ShouldBe("SQLSRC277");
        var linesBefore = ProjectLines(first);

        var result = Run(
            driver,
            WithConversions(
                compilation,
                source.Replace(
                    "public static class Conversions",
                    "// A line above the converters.\npublic static class Conversions",
                    Ordinal
                )
            )
        );

        var after = result.Diagnostics.ShouldHaveSingleItem();
        after.Id.ShouldBe("SQLSRC277");
        after
            .Location.GetLineSpan()
            .StartLinePosition.Line.ShouldBe(before.Location.GetLineSpan().StartLinePosition.Line + 1);
        AllReasons(result, TrackingNames.ProjectConverters)
            .ShouldBe([
                IncrementalStepRunReason.Modified,
                IncrementalStepRunReason.Modified,
                IncrementalStepRunReason.Modified,
            ]);
        AllReasons(result, TrackingNames.Converters).ShouldBe([IncrementalStepRunReason.Modified]);
        ProjectLines(result).ShouldBe(linesBefore.Select(line => line + 1));
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                Expect(IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Unchanged),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(Expect(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached), ignoreOrder: true);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(Expect(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached), ignoreOrder: true);
    }

    // Both types' sources, and Conversions with this source.
    private static CSharpCompilation Compilation(string conversions) =>
        MethodsCompilation(TypeSource("App", "UserQueries"))
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    conversions,
                    GeneratorHarness.ParseOptions,
                    ConversionsPath,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );

    // The compilation with this source of Conversions in place of the one it has.
    private static Compilation WithConversions(Compilation compilation, string source) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == ConversionsPath),
            CSharpSyntaxTree.ParseText(
                source,
                GeneratorHarness.ParseOptions,
                ConversionsPath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    private GeneratorDriver FirstRun(Compilation compilation)
    {
        var driver = _scene.FirstRunOfMethodsWith(compilation, _users);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // Why each of the project's converters ran, by its method.
    private static Dictionary<string, IncrementalStepRunReason> ProjectConverterReasons(
        GeneratorDriverRunResult result
    ) => Reasons<Converter>(result, TrackingNames.ProjectConverters, converter => converter.Method);

    // Every converter of the project, as the run found them.
    private static EquatableArray<Converter> ConvertersOf(GeneratorDriverRunResult result) =>
        ((ConverterSet)Outputs(result, TrackingNames.Converters).ShouldHaveSingleItem().Value).Converters;

    // The line of each of the project's converters, in the set's order.
    private static int[] ProjectLines(GeneratorDriverRunResult result) =>
        [
            .. ConvertersOf(result)
                .Where(converter => converter.Origin == ConverterOrigin.Project)
                .Select(converter => converter.Location.ShouldNotBeNull().LineSpan.Start.Line),
        ];

    // Every reason is Cached, and there is at least one.
    private static void ShouldAllBeCached(IncrementalStepRunReason[] reasons)
    {
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // The reasons of UserQueries' and OrderQueries' methods at a step of their own.
    private static Dictionary<string, IncrementalStepRunReason> Expect(
        IncrementalStepRunReason users,
        IncrementalStepRunReason orders
    ) => new() { [UserMethods] = users, [OrderMethods] = orders };

    // The text of the file of a type's methods.
    private static string MethodsOf(GeneratorDriverRunResult result, string file) =>
        result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith(file, StringComparison.Ordinal)).ToString();
}
