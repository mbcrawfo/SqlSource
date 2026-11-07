// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// Where a query's members go, as its file's markers and settings files say: the defaults of each output and member
// location, the combinations C# allows, a file that sends its queries to several places, and a nested class that
// several files share.  Each run compiles with no error and no warning in the generated code.
// PlacementDiagnosticsTests has the combinations that are errors.
public sealed class PlacementTests
{
    private const string AtSql = "-- output: sql\n\n";

    // The type the compiler warns of a protected member of (CS0628), and the line that turns the warning off.
    private const string SealedSample =
        "using SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate(Source = \".\")]\n"
        + "    public sealed partial class Sample;\n}\n";

    private const string ProtectedPragma =
        "#pragma warning disable CS0628 // A file asks for protected members of a sealed type.\n";

    // A query at codegen, whose method takes an id and reads it.
    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
    );

    // The run is at CodeGen, which writes no member location, so the query at sql takes its output's default.
    [Fact]
    public void Run_SqlQueryWithNoLocation_IsAConstantOfAPrivateSqlClass()
    {
        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string Value => Sql.GetUser;
            }
            """,
            new SqlFile(GeneratorHarness.UsersPath, AtSql + SqlTexts.GetUser)
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain(
                "        private static partial class Sql\n        {\n            /// <summary>\n"
                    + "            /// The <c>GetUser</c> query from <c>Users.sql</c>.\n"
            );
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("            public const string GetUser = ");
    }

    [Fact]
    public void Run_CodeGenQueryWithNoLocation_IsAnInternalMethodOfTheExtensionClass()
    {
        var run = MethodRuns.Run(Users, [UserEntry(Users)]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("    internal static partial class SampleExtensions\n");
        methods.ShouldContain(
            "        internal static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
                + "            this global::System.Data.Common.DbConnection connection,\n"
        );
    }

    // On the type, the members are public when the type and every type around it are, and internal otherwise.
    [Theory]
    [InlineData("[SqlSourceGenerate(Source = \".\")]\npublic partial class Sample;", "public")]
    [InlineData("[SqlSourceGenerate(Source = \".\")]\ninternal partial class Sample;", "internal")]
    [InlineData(
        "internal partial class Outer\n{\n    [SqlSourceGenerate(Source = \".\")]\n    public partial class Sample;\n}",
        "internal"
    )]
    public void Run_ClassLocationWithNoVisibility_FollowsTheType(string declaration, string visibility)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            "using SqlSource;\n\nnamespace App;\n\n" + declaration + "\n",
            new SqlFile(GeneratorHarness.UsersPath, "-- member-location: class\n\n" + SqlTexts.GetUser)
        );

        run.ShouldBeClean();
        run.Sources.Values.ShouldContain(text => text.Contains(" " + visibility + " const string GetUser = "));
    }

    // C# allows a public member of an internal type, which is effectively internal.
    [Fact]
    public void Run_ClassLocationPublicOnAnInternalType_IsAllowed()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(Source = \".\")]\n"
                + "internal partial class Sample;\n",
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- member-location: class\n-- visibility: public\n\n" + SqlTexts.GetUser
            )
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain("        public const string GetUser = \"SELECT 1;\";\n");
    }

    // A public or internal nested class can be used outside the type.  A public one has a summary, which a project
    // that documents its API needs of it.
    [Theory]
    [InlineData("public", "        /// <summary>\n")]
    [InlineData("internal", "")]
    public void Run_NestedClassPublicOrInternal_IsReachableOutsideTheType(string visibility, string summary)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            /// <summary>Queries.</summary>
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample;

            internal static class Consumer
            {
                public const string Value = Sample.Sql.GetUser;
            }
            """,
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- member-location: nested-class\n-- visibility: " + visibility + "\n\n" + SqlTexts.GetUser
            )
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain(
                "    {\n"
                    + (
                        summary.Length == 0
                            ? string.Empty
                            : summary
                                + "        /// The members of the queries of <c>App.Sample</c> that its files put in "
                                + "this class.\n        /// </summary>\n"
                    )
                    + "        "
                    + visibility
                    + " static partial class Sql\n        {\n"
            );
    }

    // A protected class nested in a static class compiles, unlike a protected member of it.
    [Fact]
    public void Run_NestedClassProtectedInAStaticClass_IsAllowed()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(Source = \".\")]\n"
                + "public static partial class Sample;\n",
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- member-location: nested-class\n-- visibility: protected\n\n" + SqlTexts.GetUser
            )
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("        protected static partial class Sql\n");
    }

    // A protected member of a sealed record is legal C#, and the compiler's warning about it is turned off in the
    // generated file.
    [Fact]
    public void Run_ProtectedOnASealedRecord_CompilesWithoutWarnings()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(Source = \".\")]\n"
                + "public sealed partial record Sample;\n",
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- member-location: class\n-- visibility: protected\n\n" + SqlTexts.GetUser
            )
        );

        run.ShouldBeClean();
        var source = run.Sources[GeneratorHarness.SampleFile];
        source.ShouldContain("#pragma warning disable CS0628 // A file asks for protected members of a sealed type.\n");
        source.ShouldContain("        protected const string GetUser = \"SELECT 1;\";\n");
    }

    // A constant, a method on the type and a method in a protected nested class, each protected, of a sealed class.
    // Both generated files turn the compiler's warning off.
    [Fact]
    public void Run_ProtectedOnASealedClass_CompilesWithoutWarnings()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- member-location: class\n-- visibility: protected\n\n"
                + "-- name: GetName\n-- output: sql\nSELECT name FROM users;\n"
                + "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
                + "-- name: ListUsers\n-- member-location: nested-class\nSELECT id FROM users;\n"
        );
        const string Source =
            "using SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate(Source = \".\")]\n"
            + "    public sealed partial class Sample;\n}\n";

        var run = MethodRuns.Run(file, [UserEntry(file), Entry(file, "ListUsers", [], [Column("id")])], Source);

        run.ShouldBeClean();
        const string Pragma = "#pragma warning disable CS0628 // A file asks for protected members of a sealed type.\n";
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldContain(Pragma);
        type.ShouldContain("        protected const string GetName = \"SELECT name FROM users;\";\n");
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(Pragma);
        methods.ShouldContain(
            "        protected static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
        );
        methods.ShouldContain("        protected static partial class Sql\n");
        methods.ShouldContain(
            "            public static async global::System.Threading.Tasks.Task<global::App.ListUsersDto[]> "
                + "ListUsers(\n"
        );
    }

    // A sealed class whose only protected item is a nested class that holds constants: the type's file writes it, so
    // the type's file turns the compiler's warning off.
    [Fact]
    public void Run_ProtectedNestedClassOfConstantsOnASealedClass_CompilesWithoutWarnings()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            SealedSample,
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- member-location: nested-class\n-- visibility: protected\n\n" + SqlTexts.GetUser
            )
        );

        run.ShouldBeClean();
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldContain(ProtectedPragma);
        type.ShouldContain("        protected static partial class Sql\n");
    }

    // A sealed class whose only protected item is a nested class that holds query methods: the methods' file writes
    // it, so the methods' file turns the warning off.
    [Fact]
    public void Run_ProtectedNestedClassOfMethodsOnASealedClass_CompilesWithoutWarnings()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- member-location: nested-class\n-- visibility: protected\n\n" + Users.Text
        );

        var run = MethodRuns.Run(file, [UserEntry(file)], SealedSample);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(ProtectedPragma);
        methods.ShouldContain("        protected static partial class Sql\n");
    }

    // Two methods of one name in two classes do not clash: the method of Get on the type, which async-method-suffix
    // names GetAsync, and the method of GetAsync in the extension class.
    [Theory]
    [InlineData("class", "    partial class Sample\n    {\n")]
    [InlineData("nested-class", "        private static partial class Sql\n        {\n")]
    public void Run_SameMethodNameInTwoClasses_GeneratesBoth(string location, string host)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: Get\n-- generator: async-method-suffix\n-- member-location: "
                + location
                + "\nSELECT id FROM users;\n"
                + "-- name: GetAsync\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "Get", [], [Column("id")]), Entry(file, "GetAsync", [], [Column("id")])]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(host);
        methods.ShouldContain(" static async global::System.Threading.Tasks.Task<global::App.GetDto[]> GetAsync(\n");
        methods.ShouldContain("    internal static partial class SampleExtensions\n    {\n");
        methods.ShouldContain(
            "        internal static async global::System.Threading.Tasks.Task<global::App.GetAsyncDto[]> GetAsync(\n"
        );
    }

    // Only an extension method widens the extension class: a public method on the type leaves it internal.
    [Fact]
    public void Run_PublicMethodOnTheType_LeavesTheExtensionClassInternal()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- member-location: class\n-- visibility: public\n"
                + "SELECT id FROM users WHERE id = @id;\n"
                + "-- name: ListUsers\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(file, [UserEntry(file), Entry(file, "ListUsers", [], [Column("id")])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("    internal static partial class SampleExtensions\n");
        methods.ShouldNotContain("public static partial class SampleExtensions");
        methods.ShouldContain(
            "        public static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
        );
    }

    // In a nested class a query method cannot extend the connection, so it is a static method that takes it, beside
    // the constant of another query of the class, which the type's file writes.  Both files write the class.
    [Fact]
    public void Run_CodeGenQueryInTheNestedClass_IsAStaticMethodThatTakesTheConnection()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- member-location: nested-class\n\n"
                + "-- name: GetName\n-- output: sql\nSELECT name FROM users;\n"
                + Users.Text
        );
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    public static string Name => Sql.GetName;

                    public static System.Threading.Tasks.Task<GetUserDto[]> Load(
                        System.Data.Common.DbConnection connection
                    ) => Sql.GetUser(connection, new GetUserParams(1));
                }
            }
            """;

        var run = MethodRuns.Run(file, [UserEntry(file)], Source);

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain("        private static partial class Sql\n        {\n            /// <summary>\n");
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("            public const string GetName = ");
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("    partial class Sample\n    {\n        private static partial class Sql\n        {\n");
        methods.ShouldContain(
            "            public static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
                + "                global::System.Data.Common.DbConnection connection,\n"
        );
        methods.ShouldNotContain("SampleExtensions");
    }

    // The extension class takes the widest accessibility of its type's methods, and each method keeps its own.
    [Fact]
    public void Run_PublicAndInternalMethods_WidenTheExtensionClassToPublic()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- visibility: public\nSELECT id FROM users WHERE id = @id;\n"
                + "-- name: ListUsers\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(file, [UserEntry(file), Entry(file, "ListUsers", [], [Column("id")])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("    public static partial class SampleExtensions\n");
        methods.ShouldContain(
            "        public static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
        );
        methods.ShouldContain(
            "        internal static async global::System.Threading.Tasks.Task<global::App.ListUsersDto[]> ListUsers(\n"
        );
    }

    // One file sends a constant to the type, one to the class Sql and one each to the classes Users and Orders, in the
    // order of the classes' first queries.
    [Fact]
    public void Run_FileOfSeveralLocations_PutsEachQueryWhereItSays()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public static string[] All => [OnType, Sql.InSql, Users.GetUser, Orders.GetOrder];
            }
            """,
            new SqlFile(
                GeneratorHarness.UsersPath,
                """
                -- name: OnType
                SELECT 1;
                -- name: GetUser
                -- member-location: nested-class
                -- nested-class: Users
                SELECT 2;
                -- name: InSql
                -- member-location: nested-class
                SELECT 3;
                -- name: GetOrder
                -- member-location: nested-class
                -- nested-class: Orders
                SELECT 4;
                """
            )
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldBe(
                $$"""
                {{GeneratedFile.Header}}
                namespace App
                {
                    partial class Sample
                    {
                        /// <summary>
                        /// The <c>OnType</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT 1;
                        /// </code>
                        /// </remarks>
                        public const string OnType = "SELECT 1;";

                        private static partial class Users
                        {
                            /// <summary>
                            /// The <c>GetUser</c> query from <c>Users.sql</c>.
                            /// </summary>
                            /// <remarks>
                            /// <code>
                            /// SELECT 2;
                            /// </code>
                            /// </remarks>
                            public const string GetUser = "SELECT 2;";
                        }

                        private static partial class Sql
                        {
                            /// <summary>
                            /// The <c>InSql</c> query from <c>Users.sql</c>.
                            /// </summary>
                            /// <remarks>
                            /// <code>
                            /// SELECT 3;
                            /// </code>
                            /// </remarks>
                            public const string InSql = "SELECT 3;";
                        }

                        private static partial class Orders
                        {
                            /// <summary>
                            /// The <c>GetOrder</c> query from <c>Users.sql</c>.
                            /// </summary>
                            /// <remarks>
                            /// <code>
                            /// SELECT 4;
                            /// </code>
                            /// </remarks>
                            public const string GetOrder = "SELECT 4;";
                        }
                    }
                }

                """
            );
    }

    // Two files fill one class.  One writes its visibility and the other leaves it to the default, so the written one
    // is the class's, whichever file comes first.
    [Theory]
    [InlineData("/app/Repo/A.sql", "/app/Repo/B.sql")]
    [InlineData("/app/Repo/B.sql", "/app/Repo/A.sql")]
    public void Run_SharedNestedClassWithOneWrittenVisibility_TakesIt(string writing, string other)
    {
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            /// <summary>Queries.</summary>
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample;

            internal static class Consumer
            {
                public static string[] All => [Sample.Sql.Written, Sample.Sql.Unwritten];
            }
            """,
            new SqlFile(writing, "-- visibility: public\n\n-- name: Written\nSELECT 1;\n"),
            new SqlFile(other, "-- name: Unwritten\nSELECT 2;\n")
        );

        run.ShouldBeClean();
        var source = run.Sources[GeneratorHarness.SampleFile];
        source.ShouldContain("        public static partial class Sql\n");
        source.ShouldNotContain("private static partial class Sql");
    }

    // The type's one file has an error, so it gives no members, and the type keeps the empty class that its folder's
    // settings file names, so that a reference to a member of it reports the member and not the class.
    [Fact]
    public void Run_OnlyFileWithAnError_LeavesTheEmptyClassOfItsFolder()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile(GeneratorHarness.UsersPath, "-- name: 1x\nSELECT 1;\n"),
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"nestedClass": "Queries"}"""
            )
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC103 ");
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain(
                "    partial class Sample\n    {\n        private static partial class Queries\n        {\n        }\n"
            );
    }

    // A method whose models are internal compiles where no derived type of another assembly reaches it: a method that
    // is internal or private, in a class that is, or of a type that a level of internal, private or private protected
    // keeps in its assembly, and one of a type that is public with every type around it, whose models are public.  The
    // defaults always fit.
    [Theory]
    [InlineData("public", "", "protected", "-- member-location: class\n-- visibility: internal\n", "", "internal")]
    [InlineData("public", "", "protected", "-- member-location: class\n-- visibility: private\n", "", "private")]
    [InlineData("public", "", "protected", "-- member-location: class\n", "", "internal")]
    [InlineData(
        "public",
        "",
        "protected",
        "-- member-location: nested-class\n-- visibility: internal\n",
        "internal static partial class Sql\n",
        "public"
    )]
    [InlineData(
        "public",
        "",
        "protected",
        "-- member-location: nested-class\n",
        "private static partial class Sql\n",
        "public"
    )]
    [InlineData("public", "", "protected", "", "internal static partial class SampleExtensions\n", "internal")]
    [InlineData(
        "public",
        "",
        "protected internal",
        "-- member-location: class\n-- visibility: internal\n",
        "",
        "internal"
    )]
    [InlineData(
        "public",
        "",
        "private protected",
        "-- member-location: nested-class\n-- visibility: public\n",
        "public static partial class Sql\n",
        "public"
    )]
    [InlineData("public", "", "private protected", "-- member-location: class\n-- visibility: public\n", "", "public")]
    [InlineData("public", "", "internal", "-- member-location: class\n-- visibility: protected\n", "", "protected")]
    [InlineData(
        "public",
        "",
        "public",
        "-- member-location: nested-class\n-- visibility: public\n",
        "public static partial class Sql\n",
        "public"
    )]
    [InlineData("public", "", "public", "-- member-location: class\n-- visibility: protected\n", "", "protected")]
    [InlineData("internal", "", "protected", "-- member-location: class\n-- visibility: public\n", "", "public")]
    [InlineData(
        "internal",
        "",
        "protected internal",
        "-- member-location: nested-class\n-- visibility: public\n",
        "public static partial class Sql\n",
        "public"
    )]
    [InlineData("public", "internal", "protected", "-- member-location: class\n-- visibility: public\n", "", "public")]
    [InlineData(
        "public",
        "private protected",
        "protected",
        "-- member-location: nested-class\n-- visibility: protected\n",
        "protected static partial class Sql\n",
        "public"
    )]
    public void Run_MethodOfANestedTypeNotReachableAsProtected_Compiles(
        string outer,
        string middle,
        string accessibility,
        string markers,
        string host,
        string modifier
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n" + markers + "SELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(
            file,
            [UserEntry(file)],
            GeneratorHarness.NestedSampleSource(outer, middle, accessibility)
        );

        run.ShouldBeClean();
        var methods = run.Sources[
            middle.Length > 0 ? "App.Outer.Middle.Sample-Methods.g.cs" : "App.Outer.Sample-Methods.g.cs"
        ];
        methods.ShouldContain(host.Length > 0 ? host : "partial class Sample\n");
        methods.ShouldContain(
            " " + modifier + " static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
        );
    }

    // A model is public when a public type shares it, so the public extension method of a type that is not public
    // takes and returns public models when a public type claims its file too.  The types' namespaces differ, so that
    // their extension methods do not clash, and the models' namespace is one.
    [Fact]
    public void Run_PublicMethodOfAnInternalTypeWithSharedPublicModels_Compiles()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = "Users.sql")]
                public partial class Sample;
            }

            namespace App.Internal
            {
                [SqlSourceGenerate(Source = "Users.sql")]
                internal partial class Other;
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- model-namespace: App.Models\n\n-- name: GetUser\n-- visibility: public\n"
                + "SELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(Source, file, Sidecars.Of(file, UserEntry(file)));

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("    public static partial class SampleExtensions\n");
        run.Sources["App.Internal.Other-Methods.g.cs"]
            .ShouldContain("    public static partial class OtherExtensions\n");
    }

    // A constant or a token method takes no models, so it may be public or protected on a type that a derived type of
    // another assembly reaches, on the type or in its nested class.
    [Theory]
    [InlineData("-- member-location: class\n-- visibility: public\n", "SELECT 1;", " public const string GetUser = ")]
    [InlineData(
        "-- member-location: class\n-- visibility: protected\n",
        "SELECT {{t}};",
        " protected static string GetUser("
    )]
    [InlineData(
        "-- member-location: nested-class\n-- visibility: public\n",
        "SELECT 1;",
        " public static partial class Sql\n"
    )]
    [InlineData(
        "-- member-location: nested-class\n-- visibility: protected\n",
        "SELECT {{t}};",
        " protected static partial class Sql\n"
    )]
    public void Run_ConstantOrTokenMethodOfATypeReachableAsProtected_IsAllowed(
        string markers,
        string sql,
        string expected
    )
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.NestedSampleSource("public", "", "protected"),
            new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n" + markers + sql + "\n")
        );

        run.ShouldBeClean();
        run.Sources["App.Outer.Sample.g.cs"].ShouldContain(expected);
    }

    // The entry of GetUser, which takes an id and reads it, written for the file's hash.
    private static SidecarEntry UserEntry(SqlFile file) => Entry(file, "GetUser", [Parameter("id")], [Column("id")]);
}
