// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Converters;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The methods of the project marked [SqlSourceConverter]: SQLSRC277 at one that is not a converter, and SQLSRC278 at
// two of one edge.  Each run has converters and no .sql file, so what is reported is the converters' alone, and is
// at Models unless it says otherwise.
public sealed class ConverterDiagnosticsTests
{
    // The line of the marked method in FaultySource.
    private const int FaultyMethodLine = 13;

    [Fact]
    public void Run_ValidConverters_ReportNothing()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            """
            using SqlSource;

            namespace App;

            public readonly record struct Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Email email) => email.Value;
            }

            public readonly record struct UserId(long Value);

            internal static class Conversions
            {
                [SqlSourceConverter]
                public static UserId ToUserId(long value) => new(value);

                [SqlSourceConverter]
                public static long FromUserId(UserId id) => id.Value;
            }
            """
        );

        run.ShouldBeClean();
        var converters = StepOutputs.Single<ConverterSet>(run, TrackingNames.Converters).Converters;
        converters.Select(converter => converter.Location?.LineSpan.Start.Line + 1).ShouldBe([11, 22, 19, 8]);
        converters
            .Select(converter => converter with { Location = null })
            .ShouldBe([
                new Converter(
                    "global::App.Email.ToDatabase",
                    "App.Email.ToDatabase(App.Email)",
                    "global::App.Email",
                    true,
                    "string",
                    false,
                    EquatableArray<string>.Empty,
                    null,
                    ConverterOrigin.Project,
                    null
                ),
                new Converter(
                    "global::App.Conversions.FromUserId",
                    "App.Conversions.FromUserId(App.UserId)",
                    "global::App.UserId",
                    true,
                    "long",
                    true,
                    EquatableArray<string>.Empty,
                    null,
                    ConverterOrigin.Project,
                    null
                ),
                new Converter(
                    "global::App.Conversions.ToUserId",
                    "App.Conversions.ToUserId(long)",
                    "long",
                    true,
                    "global::App.UserId",
                    true,
                    EquatableArray<string>.Empty,
                    null,
                    ConverterOrigin.Project,
                    null
                ),
                new Converter(
                    "global::App.Email.FromDatabase",
                    "App.Email.FromDatabase(string)",
                    "string",
                    false,
                    "global::App.Email",
                    true,
                    EquatableArray<string>.Empty,
                    null,
                    ConverterOrigin.Project,
                    null
                ),
            ]);
    }

    [Theory]
    [InlineData(
        "public class Conversions",
        "public Email ToEmail(string value) => new(value);",
        "App.Conversions.ToEmail(string)",
        "it is not static"
    )]
    [InlineData(
        "public class Conversions",
        "public void Log(string value) { }",
        "App.Conversions.Log(string)",
        "it is not static"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email ToEmail<T>(string value) => new(value);",
        "App.Conversions.ToEmail<T>(string)",
        "it is generic"
    )]
    [InlineData(
        "public static class Conversions<T>",
        "public static Email ToEmail(string value) => new(value);",
        "App.Conversions<T>.ToEmail(string)",
        "it is in a generic type"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email Empty() => new(string.Empty);",
        "App.Conversions.Empty()",
        "it does not take exactly one argument"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email ToEmail(string value, int length) => new(value);",
        "App.Conversions.ToEmail(string, int)",
        "it does not take exactly one argument"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email ToEmail(ref string value) => new(value);",
        "App.Conversions.ToEmail(ref string)",
        "its argument is ref, out, in, params or optional"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email ToEmail(out string value) { value = string.Empty; return new(value); }",
        "App.Conversions.ToEmail(out string)",
        "its argument is ref, out, in, params or optional"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email ToEmail(in string value) => new(value);",
        "App.Conversions.ToEmail(in string)",
        "its argument is ref, out, in, params or optional"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email ToEmail(params string[] values) => new(values[0]);",
        "App.Conversions.ToEmail(params string[])",
        "its argument is ref, out, in, params or optional"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email ToEmail(string value = \"\") => new(value);",
        "App.Conversions.ToEmail(string)",
        "its argument is ref, out, in, params or optional"
    )]
    [InlineData(
        "public static class Conversions",
        "public static void ToEmail(string value) { }",
        "App.Conversions.ToEmail(string)",
        "it returns nothing"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Task ToEmail(string value) => Task.CompletedTask;",
        "App.Conversions.ToEmail(string)",
        "it returns nothing"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Task<Email> ToEmail(string value) => Task.FromResult(new Email(value));",
        "App.Conversions.ToEmail(string)",
        "it returns nothing"
    )]
    [InlineData(
        "public static class Conversions",
        "public static ValueTask ToEmail(string value) => default;",
        "App.Conversions.ToEmail(string)",
        "it returns nothing"
    )]
    [InlineData(
        "public static class Conversions",
        "public static ValueTask<Email> ToEmail(string value) => new(new Email(value));",
        "App.Conversions.ToEmail(string)",
        "it returns nothing"
    )]
    [InlineData(
        "public static class Conversions",
        "public static Email? ToEmail(string value) => new(value);",
        "App.Conversions.ToEmail(string)",
        "its return type is annotated as nullable"
    )]
    [InlineData(
        "public static class Conversions",
        "public static string? FromEmail(Email email) => email.Value;",
        "App.Conversions.FromEmail(App.Email)",
        "its return type is annotated as nullable"
    )]
    [InlineData(
        "public static class Conversions",
        "private static Email ToEmail(string value) => new(value);",
        "App.Conversions.ToEmail(string)",
        "this project cannot call it"
    )]
    [InlineData(
        "file static class Conversions",
        "public static Email ToEmail(string value) => new(value);",
        "App.Conversions.ToEmail(string)",
        "this project cannot call it"
    )]
    [InlineData(
        "public static class Conversions",
        "[Obsolete(\"Use another.\", true)] public static Email ToEmail(string value) => new(value);",
        "App.Conversions.ToEmail(string)",
        "it is marked [Obsolete] as an error"
    )]
    [InlineData(
        "[Obsolete(\"Use another.\", true)] public static class Conversions",
        "public static Email ToEmail(string value) => new(value);",
        "App.Conversions.ToEmail(string)",
        "it is marked [Obsolete] as an error"
    )]
    public void Run_MarkedMethodWithAFault_ReportsSQLSRC277AtIt(
        string type,
        string method,
        string displayName,
        string fault
    )
    {
        var run = GeneratorHarness.Run(OutputKind.Models, FaultySource(type, method));

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith($"SQLSRC277 /app/Repo/Sample.cs({FaultyMethodLine},");
        diagnostic.ShouldEndWith(
            $": The method '{displayName}' is marked [SqlSourceConverter], but {fault}.  A converter is a static "
                + "method of one argument that returns the converted value."
        );
    }

    [Fact]
    public void Run_ObsoleteAsAWarning_ReportsNothing()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            FaultySource(
                "[Obsolete(\"Use another.\")] public static class Conversions",
                "[Obsolete] public static Email ToEmail(string value) => new(value);"
            )
        );

        run.Diagnostics.ShouldBeEmpty();
        var converter = StepOutputs
            .Single<ConverterSet>(run, TrackingNames.Converters)
            .Converters.ShouldHaveSingleItem();
        converter.Fault.ShouldBeNull();
        converter.WarningIds.ShouldBe(["CS0612", "CS0618"]);
    }

    [Fact]
    public void Run_TwoConvertersOfOneEdge_ReportSQLSRC278AtBoth()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            """
            using SqlSource;

            namespace App;

            public readonly record struct Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);
            }

            public static class Conversions
            {
                [SqlSourceConverter]
                public static Email ToEmail(string value) => new(value);
            }
            """
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC278 /app/Repo/Sample.cs(14,25)-(14,32): The converters 'App.Conversions.ToEmail(string)' and "
                + "'App.Email.FromDatabase(string)' both convert 'string' to 'App.Email'.  Keep one.",
            "SQLSRC278 /app/Repo/Sample.cs(8,25)-(8,37): The converters 'App.Email.FromDatabase(string)' and "
                + "'App.Conversions.ToEmail(string)' both convert 'string' to 'App.Email'.  Keep one.",
        ]);
    }

    [Fact]
    public void Run_OneTypeToItself_IsNotADuplicate()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            """
            using System;
            using SqlSource;

            namespace App;

            public static class Conversions
            {
                [SqlSourceConverter]
                public static DateTime Kind(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }
            """
        );

        run.ShouldBeClean();
        var converter = StepOutputs
            .Single<ConverterSet>(run, TrackingNames.Converters)
            .Converters.ShouldHaveSingleItem();
        converter.ArgumentText.ShouldBe("global::System.DateTime");
        converter.ReturnText.ShouldBe("global::System.DateTime");
    }

    // The converters are found whatever the output, so a CodeGen project without a query gets the error too.
    [Fact]
    public void Run_ConverterUnderCodeGenWithoutQueries_ReportsSQLSRC277()
    {
        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            FaultySource("public static class Conversions", "public static void ToEmail(string value) { }")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC277 /app/Repo/Sample.cs(13,24)-(13,31): The method 'App.Conversions.ToEmail(string)' is marked "
                + "[SqlSourceConverter], but it returns nothing.  A converter is a static method of one argument that "
                + "returns the converted value.",
        ]);
    }

    // A type that holds one marked method, at FaultyMethodLine.
    private static string FaultySource(string type, string method) =>
        "#nullable enable\n"
        + "using System;\n"
        + "using System.Threading.Tasks;\n"
        + "using SqlSource;\n"
        + "\n"
        + "namespace App;\n"
        + "\n"
        + "public readonly record struct Email(string Value);\n"
        + "\n"
        + type
        + "\n"
        + "{\n"
        + "    [SqlSourceConverter]\n"
        + "    "
        + method
        + "\n"
        + "}\n";
}
