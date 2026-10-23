// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The file of an attributed type: where its constants go, how it repeats the type, and the compilation it is run in.
public sealed class GeneratedSourceTests
{
    [Fact]
    public void Run_NoAttributedType_EmitsOnlyTheAttribute()
    {
        var run = GeneratorHarness.Run(OutputKind.Sql, "public class Sample;", GeneratorHarness.UsersFile);

        run.Sources.Keys.ShouldBe(["SqlSourceGenerateAttribute.g.cs"]);
        run.Sources["SqlSourceGenerateAttribute.g.cs"].ShouldBe(AttributeSource.Text);
        run.ShouldBeClean();
    }

    [Fact]
    public void Run_NestedLocation_PutsTheConstantsInAPrivateSqlClass()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate]
            public partial class Sample
            {
                public static string First => Sql.GetUser;

                public static string Second => Sql.ListUsers;
            }
            """,
            new SqlFile(
                GeneratorHarness.UsersPath,
                """
                -- Queries for users.
                -- name: GetUser
                -- summary: Loads one user by id.
                SELECT id, name -- the columns
                FROM users
                WHERE id = @id;

                -- name: ListUsers
                SELECT id, name FROM users;
                """
            )
        );

        run.ShouldBeClean();
        run.Sources.Keys.ShouldBe(["SqlSourceGenerateAttribute.g.cs", "App.Sample.g.cs"], ignoreOrder: true);
        run.Sources["App.Sample.g.cs"]
            .ShouldBe(
                $$"""
                {{GeneratedFile.Header}}
                namespace App
                {
                    partial class Sample
                    {
                        private static class Sql
                        {
                            /// <summary>
                            /// Loads one user by id.
                            /// </summary>
                            /// <remarks>
                            /// <code>
                            /// SELECT id, name
                            /// FROM users
                            /// WHERE id = @id;
                            /// </code>
                            /// </remarks>
                            public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";

                            /// <summary>
                            /// The <c>ListUsers</c> query from <c>Users.sql</c>.
                            /// </summary>
                            /// <remarks>
                            /// <code>
                            /// SELECT id, name FROM users;
                            /// </code>
                            /// </remarks>
                            public const string ListUsers = "SELECT id, name FROM users;";
                        }
                    }
                }

                """
            );
    }

    [Fact]
    public void Run_DirectLocation_PutsTheConstantsOnTheType()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public static partial class Sample;

            public static class Consumer
            {
                public const string Value = Sample.GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"]
            .ShouldBe(
                $$"""
                {{GeneratedFile.Header}}
                namespace App
                {
                    partial class Sample
                    {
                        /// <summary>
                        /// The <c>GetUser</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT 1;
                        /// </code>
                        /// </remarks>
                        public const string GetUser = "SELECT 1;";
                    }
                }

                """
            );
    }

    [Theory]
    [InlineData("public partial class Sample", "partial class Sample")]
    [InlineData("public static partial class Sample", "partial class Sample")]
    [InlineData("public abstract partial class Sample", "partial class Sample")]
    [InlineData("internal sealed partial class Sample", "partial class Sample")]
    [InlineData("public partial struct Sample", "partial struct Sample")]
    [InlineData("public readonly partial struct Sample", "partial struct Sample")]
    [InlineData("public ref partial struct Sample", "partial struct Sample")]
    [InlineData("public partial record Sample", "partial record Sample")]
    [InlineData("public partial record class Sample", "partial record Sample")]
    [InlineData("public partial record struct Sample", "partial record struct Sample")]
    [InlineData("public readonly partial record struct Sample", "partial record struct Sample")]
    [InlineData("public partial class Sample<T>", "partial class Sample<T>")]
    [InlineData(
        "public partial class Sample<TKey, TValue> where TKey : notnull where TValue : class, new()",
        "partial class Sample<TKey, TValue>"
    )]
    [InlineData(
        "public partial class Sample<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(0)] @in>",
        "partial class Sample<@in>"
    )]
    public void Run_EachKindOfType_RepeatsItsKeywordNameAndTypeParameters(string declaration, string expected)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            $$"""
            using SqlSource;

            namespace App;

            [SqlSourceGenerate]
            {{declaration}}
            {
                public static string Value => Sql.GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
        run.Sources.Single(source => source.Key.StartsWith("App.Sample", StringComparison.Ordinal))
            .Value.ShouldContain("namespace App\n{\n    " + expected + "\n    {\n        private static class Sql\n");
    }

    [Fact]
    public void Run_NestedType_RepeatsEachContainingType()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App.Data
            {
                public static partial class Outer
                {
                    public partial interface IMiddle<TKey, TValue>
                    {
                        [SqlSourceGenerate]
                        private partial record Inner
                        {
                            public static string Value => Sql.GetUser;
                        }
                    }
                }
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
        run.Sources["App.Data.Outer.IMiddle-2.Inner.g.cs"]
            .ShouldBe(
                $$"""
                {{GeneratedFile.Header}}
                namespace App.Data
                {
                    partial class Outer
                    {
                        partial interface IMiddle<TKey, TValue>
                        {
                            partial record Inner
                            {
                                private static class Sql
                                {
                                    /// <summary>
                                    /// The <c>GetUser</c> query from <c>Users.sql</c>.
                                    /// </summary>
                                    /// <remarks>
                                    /// <code>
                                    /// SELECT 1;
                                    /// </code>
                                    /// </remarks>
                                    public const string GetUser = "SELECT 1;";
                                }
                            }
                        }
                    }
                }

                """
            );
    }

    // CS8981 is reported from warning level 7 on, so it shows that the harness compiles at the highest level.
    [Fact]
    public void CreateCompilation_LowercaseTypeName_ReportsCS8981()
    {
        var compilation = GeneratorHarness.CreateCompilation([
            new SourceFile("/app/Repo/Lowercase.cs", "internal class lowercase { }\n"),
        ]);

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Select(diagnostic => diagnostic.Id)
            .ShouldContain("CS8981");
    }
}
