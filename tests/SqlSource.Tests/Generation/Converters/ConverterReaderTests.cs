using System;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generation.Converters;

// The faults of a marked method that only a C# newer than the generator tests' C# 12 can declare, so these tests are
// outside tests/SqlSource.Tests/Generator/, which RoslynFloor compiles on Roslyn 4.8.0.
public sealed class ConverterReaderTests
{
    // A C# 14 extension block is a type of its own, which the generated code cannot call a member of by name, so each
    // marked method in it is SQLSRC277 and the query that would convert through it gets no method.
    [Fact]
    public void Run_ConvertersInAnExtensionBlock_ReportsSqlsrc277AtEachMethod()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                public readonly record struct UserId(long Value);

                public static class UserIdConversions
                {
                    extension(UserId)
                    {
                        [SqlSourceConverter]
                        public static UserId FromDatabase(long value) => new(value);

                        [SqlSourceConverter]
                        public static long ToDatabase(UserId id) => id.Value;
                    }
                }
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: int8=App.UserId\n-- name: GetId\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(
            file,
            [
                Entry(
                    file,
                    "GetId",
                    [Parameter("id", Catalog("int8"), nullable: false)],
                    [Column("id", Catalog("int8"))]
                ),
            ],
            Source,
            languageVersion: LanguageVersion.Latest
        );

        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Diagnostics.ShouldBe(
            [
                "SQLSRC277 /app/Repo/Sample.cs(15,34)-(15,46): The method "
                    + "'App.UserIdConversions.extension(App.UserId).FromDatabase(long)' is marked "
                    + "[SqlSourceConverter], but it is in an extension block.  A converter is a static method of one "
                    + "argument that returns the converted value.",
                "SQLSRC277 /app/Repo/Sample.cs(18,32)-(18,42): The method "
                    + "'App.UserIdConversions.extension(App.UserId).ToDatabase(App.UserId)' is marked "
                    + "[SqlSourceConverter], but it is in an extension block.  A converter is a static method of one "
                    + "argument that returns the converted value.",
            ],
            ignoreOrder: true
        );
        run.Sources.Values.ShouldAllBe(source => !source.Contains("UserIdConversions", StringComparison.Ordinal));
    }
}
