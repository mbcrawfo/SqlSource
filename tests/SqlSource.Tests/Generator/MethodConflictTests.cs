// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The methods of two types compared: two extension methods that a call could not tell apart (SQLSRC274), and an
// extension class that cannot be written (SQLSRC276).  Neither leaves an error in the compilation.  Each run is at
// CodeGen under postgres on Npgsql 10.
public sealed class MethodConflictTests
{
    private const string APath = "/app/Repo/A.sql";

    private const string BPath = "/app/Repo/B.sql";

    // Two types of one name nested in two types give one extension class, whatever their methods' parameters.
    private const string NestedSource = """
        using SqlSource;

        namespace App
        {
            public partial class First
            {
                [SqlSourceGenerate(Path = "A.sql")]
                public partial class Queries;
            }

            public partial class Second
            {
                [SqlSourceGenerate(Path = "B.sql")]
                public partial class Queries;
            }
        }
        """;

    // Two types of one namespace, whose extension classes are FirstExtensions and SecondExtensions.
    private const string TwoTypesSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Path = "A.sql")]
            public partial class First;

            [SqlSourceGenerate(Path = "B.sql")]
            public partial class Second;
        }
        """;

    [Fact]
    public void Run_TwoTypesOfOneExtensionClassWithOneMethodName_IsSqlsrc274AtBoth()
    {
        var a = new SqlFile(
            APath,
            "-- name: Get\n-- input-model: GetUserFilter\nSELECT id FROM users WHERE id = @id;\n"
        );
        var b = new SqlFile(BPath, "-- name: Get\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(
            NestedSource,
            a,
            Sidecars.Of(a, Entry(a, "Get", [Parameter("id")], [Column("id")])),
            b,
            Sidecars.Of(b, Entry(b, "Get", [], [Column("id")]))
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/A.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'Get', because "
                    + "the method of the query 'Get' of 'App.Second.Queries' has that name too, in the class "
                    + "'App.QueriesExtensions'.  Rename the query.",
                "SQLSRC274 /app/Repo/B.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'Get', because "
                    + "the method of the query 'Get' of 'App.First.Queries' has that name too, in the class "
                    + "'App.QueriesExtensions'.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain("App.First.Queries-Methods.g.cs");
        run.Sources.Keys.ShouldNotContain("App.Second.Queries-Methods.g.cs");
    }

    [Fact]
    public void Run_TwoExtensionClassesOfOneNamespaceWithOneSignature_IsSqlsrc274AtBoth()
    {
        var a = new SqlFile(APath, "-- name: Get\nSELECT id FROM users;\n-- name: List\nSELECT id FROM users;\n");
        var b = new SqlFile(BPath, "-- name: Get\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(
            TwoTypesSource,
            a,
            Sidecars.Of(a, Entry(a, "Get", [], [Column("id")]), Entry(a, "List", [], [Column("id")])),
            b,
            Sidecars.Of(b, Entry(b, "Get", [], [Column("id")]))
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/A.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'Get', because "
                    + "the method of the query 'Get' of 'App.Second', in the class 'App.SecondExtensions' of the same "
                    + "namespace, has that name and the same parameters.  Rename the query.",
                "SQLSRC274 /app/Repo/B.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'Get', because "
                    + "the method of the query 'Get' of 'App.First', in the class 'App.FirstExtensions' of the same "
                    + "namespace, has that name and the same parameters.  Rename the query.",
            ]);

        // The other method of the first type is still written.
        var first = run.Sources["App.First-Methods.g.cs"];
        first.ShouldContain(" List(\n");
        first.ShouldNotContain(" Get(\n");
        run.Sources.Keys.ShouldNotContain("App.Second-Methods.g.cs");
    }

    // Two types of one namespace that claim one file give each of its queries two methods of one signature: the query
    // gets one SQLSRC274 for each type, each naming the other.
    [Fact]
    public void Run_TwoTypesOfOneNamespaceClaimingOneFile_IsSqlsrc274OnceForEachType()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class First;

                [SqlSourceGenerate]
                public partial class Second;
            }
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Get\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "Get", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'Get', "
                    + "because the method of the query 'Get' of 'App.First', in the class 'App.FirstExtensions' of the "
                    + "same namespace, has that name and the same parameters.  Rename the query.",
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'Get', "
                    + "because the method of the query 'Get' of 'App.Second', in the class 'App.SecondExtensions' of "
                    + "the same namespace, has that name and the same parameters.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain("App.First-Methods.g.cs");
        run.Sources.Keys.ShouldNotContain("App.Second-Methods.g.cs");
    }

    [Fact]
    public void Run_SameNameWithOtherParameters_InTwoExtensionClasses_Compiles()
    {
        var a = new SqlFile(APath, "-- name: Get\nSELECT id FROM users WHERE id = @id;\n");
        var b = new SqlFile(BPath, "-- name: Get\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(
            TwoTypesSource,
            a,
            Sidecars.Of(a, Entry(a, "Get", [Parameter("id")], [Column("id")])),
            b,
            Sidecars.Of(b, Entry(b, "Get", [], [Column("id")]))
        );

        run.ShouldBeClean();
        run.Sources["App.First-Methods.g.cs"].ShouldContain(" Get(\n");
        run.Sources["App.Second-Methods.g.cs"].ShouldContain(" Get(\n");
    }

    [Fact]
    public void Run_ExtensionClassNamedAsAProjectType_IsSqlsrc276()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                public static class SampleExtensions;
            }
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Get\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "Get", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC276 /app/Repo/Sample.cs(5,6)-(5,23): The query methods of 'App.Sample' cannot go into the "
                    + "extension class 'App.SampleExtensions', because the project has a type of that name.  Rename "
                    + "the type or what has the name, or set the attribute's MethodLocation to put the methods on the "
                    + "type.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // A type of the global namespace gives a class of the global namespace, which the project's type of its name takes.
    [Fact]
    public void Run_ExtensionClassOfTheGlobalNamespaceNamedAsAProjectType_IsSqlsrc276()
    {
        const string Source = """
            using SqlSource;

            [SqlSourceGenerate]
            public partial class Sample;

            public static class SampleExtensions;
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Get\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "Get", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC276 /app/Repo/Sample.cs(3,2)-(3,19): The query methods of 'Sample' cannot go into the "
                    + "extension class 'SampleExtensions', because the project has a type of that name.  Rename the "
                    + "type or what has the name, or set the attribute's MethodLocation to put the methods on the "
                    + "type.",
            ]);
        run.Sources.Keys.ShouldNotContain("Sample-Methods.g.cs");
    }

    // The model is generated, and the class that would share its name, or that of the model's namespace, is not.
    [Theory]
    [InlineData("App.SampleExtensions", "a model of that name")]
    [InlineData("App.SampleExtensions.Row", "a model in a namespace of that name")]
    public void Run_ModelNamedAsTheExtensionClass_IsSqlsrc276WithNoCompilerError(string model, string what)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: Get\n-- output-model: " + model + "\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "Get", [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                "SQLSRC276 /app/Repo/Sample.cs(5,6)-(5,23): The query methods of 'App.Sample' cannot go into the "
                    + "extension class 'App.SampleExtensions', because SqlSource writes "
                    + what
                    + ".  Rename the type or what has the name, or set the attribute's MethodLocation to put the "
                    + "methods on the type.",
            ]);
        run.Sources.Keys.ShouldContain(model + "-Model.g.cs");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_TwoTypesOfOneExtensionClassWithTwoAccessibilities_IsSqlsrc276AtBoth()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public partial class First
                {
                    [SqlSourceGenerate(Path = "A.sql")]
                    public partial class Queries;
                }

                internal partial class Second
                {
                    [SqlSourceGenerate(Path = "B.sql")]
                    public partial class Queries;
                }
            }
            """;
        var a = new SqlFile(APath, "-- name: GetA\nSELECT id FROM users;\n");
        var b = new SqlFile(BPath, "-- name: GetB\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(
            Source,
            a,
            Sidecars.Of(a, Entry(a, "GetA", [], [Column("id")])),
            b,
            Sidecars.Of(b, Entry(b, "GetB", [], [Column("id")]))
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC276 /app/Repo/Sample.cs(7,10)-(7,43): The query methods of 'App.First.Queries' cannot go into "
                    + "the extension class 'App.QueriesExtensions', because 'App.Second.Queries' puts its methods "
                    + "there too, and the class would be public for one and internal for the other.  Rename the type "
                    + "or what has the name, or set the attribute's MethodLocation to put the methods on the type.",
                "SQLSRC276 /app/Repo/Sample.cs(13,10)-(13,43): The query methods of 'App.Second.Queries' cannot go "
                    + "into the extension class 'App.QueriesExtensions', because 'App.First.Queries' puts its methods "
                    + "there too, and the class would be public for one and internal for the other.  Rename the type "
                    + "or what has the name, or set the attribute's MethodLocation to put the methods on the type.",
            ]);
        run.Sources.Keys.ShouldNotContain("App.First.Queries-Methods.g.cs");
        run.Sources.Keys.ShouldNotContain("App.Second.Queries-Methods.g.cs");
    }

    [Fact]
    public void Run_TwoTypesOfOneExtensionClassAndOneAccessibility_SharePartialDeclarations()
    {
        var a = new SqlFile(APath, "-- name: GetA\nSELECT id FROM users;\n");
        var b = new SqlFile(BPath, "-- name: GetB\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(
            NestedSource,
            a,
            Sidecars.Of(a, Entry(a, "GetA", [], [Column("id")])),
            b,
            Sidecars.Of(b, Entry(b, "GetB", [], [Column("id")]))
        );

        run.ShouldBeClean();
        run.Sources["App.First.Queries-Methods.g.cs"].ShouldContain("public static partial class QueriesExtensions\n");
        run.Sources["App.First.Queries-Methods.g.cs"].ShouldContain(" GetA(\n");
        run.Sources["App.Second.Queries-Methods.g.cs"].ShouldContain("public static partial class QueriesExtensions\n");
        run.Sources["App.Second.Queries-Methods.g.cs"].ShouldContain(" GetB(\n");
    }
}
