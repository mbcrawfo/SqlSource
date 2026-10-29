// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ConverterCases;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// A converter or a mapped type that is obsolete or experimental: the generated code that calls it disables the warning
// of its attribute, so that a project builds as it did.  Each run is at CodeGen under postgres on Npgsql 10.  The
// cases are shared with ConverterMethodTests through ConverterCases.
public sealed class ConverterObsoleteTests
{
    [Fact]
    public void Run_ObsoleteConverter_DisablesItsWarning()
    {
        var source = AppSourceOf(Struct)
            .Replace(
                "        public static Email FromDatabase",
                "        [System.Obsolete(\"Read emails another way.\")]\n        public static Email FromDatabase",
                StringComparison.Ordinal
            );

        var run = RunEmailColumn(Struct, source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("#nullable enable\n#pragma warning disable CS0612, CS0618 ");
        methods.ShouldContain(NullableEmailRead);
    }

    // A parameter's converter warns where the parameter is bound, so its ids are the method's even when no column
    // reads the type.
    [Fact]
    public void Run_ObsoleteWriteConverter_DisablesItsWarning()
    {
        var source = AppSourceOf(Struct)
            .Replace(
                "        public static string ToDatabase",
                "        [System.Obsolete(\"Write emails another way.\")]\n        public static string ToDatabase",
                StringComparison.Ordinal
            );

        var run = RunEmailParameter(Struct, nullable: true, source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("#nullable enable\n#pragma warning disable CS0612, CS0618 ");
        methods.ShouldContain(EmailWrite);
    }

    // Phase 8.1's methods name a mapped type in GetFieldValue<T>, and an obsolete one warns there as it would in a
    // model.
    [Fact]
    public void Run_ObsoleteMappedTypeUnderCodeGen_DisablesItsWarning()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                [System.Obsolete("Use another type.")]
                public readonly record struct Email(string Value);
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.email=App.Email\n-- name: GetEmail\nSELECT email FROM users;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "GetEmail", [], [Column("email", Email, nullable: true)])], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("#nullable enable\n#pragma warning disable CS0612, CS0618 ");
        methods.ShouldContain("reader.GetFieldValue<global::App.Email>(0)");
    }

    // An [Experimental] converter is an error under its id unless the id is disabled, which only the errors of the
    // compilation would show.
    [Fact]
    public void Run_ExperimentalConverter_DisablesItsId()
    {
        var source = AppSourceOf(Struct)
            .Replace(
                "        public static Email FromDatabase",
                "        [System.Diagnostics.CodeAnalysis.Experimental(\"APP001\")]\n"
                    + "        public static Email FromDatabase",
                StringComparison.Ordinal
            );

        var run = RunEmailColumn(Struct, source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("#nullable enable\n#pragma warning disable APP001 ");
        methods.ShouldContain(NullableEmailRead);
    }

    // A converter of an obsolete type warns as the type does, so the type's ids are the converter's.
    [Fact]
    public void Run_ConverterOfAnObsoleteType_DisablesItsWarning()
    {
        var source = AppSourceOf(Struct)
            .Replace(
                "    public static class Conversions",
                "    [System.Obsolete(\"Convert another way.\")]\n    public static class Conversions",
                StringComparison.Ordinal
            );
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: int8=App.UserId\n-- name: FindId\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(
            file,
            [
                Entry(
                    file,
                    "FindId",
                    [Parameter("id", Catalog("int8"), nullable: false)],
                    [Column("id", Catalog("int8"))]
                ),
            ],
            source
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("#nullable enable\n#pragma warning disable CS0612, CS0618 ");
        methods.ShouldContain("parameter.TypedValue = global::App.Conversions.FromUserId(parameters.Id);\n");
        methods.ShouldContain("(global::App.Conversions.ToUserId(reader.GetFieldValue<long>(0)))");
    }

    // Methods on the type start as the type's file does, and the pragma of their warnings follows its own.
    [Fact]
    public void Run_ObsoleteMappedTypeWithMethodsOnTheType_DisablesItsWarning()
    {
        var source =
            MethodRuns.SampleSource("MethodLocation = MethodLocation.Public")
            + """

                namespace App
                {
                    [System.Obsolete("Use another type.")]
                    public readonly record struct Email(string Value);
                }
                """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.email=App.Email\n-- name: GetEmail\nSELECT email FROM users;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "GetEmail", [], [Column("email", Email, nullable: true)])], source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldStartWith(
            "// <auto-generated/>\n#nullable enable\n"
                + "#pragma warning disable CS0108, CS0114 // A query can have the name of an inherited member.\n"
                + "#pragma warning disable CS0612, CS0618 "
                + "// A converter or a mapped type is obsolete or experimental.\n"
                + "\n"
        );
        methods.ShouldContain("partial class Sample\n");
        methods.ShouldContain("reader.GetFieldValue<global::App.Email>(0)");
    }
}
