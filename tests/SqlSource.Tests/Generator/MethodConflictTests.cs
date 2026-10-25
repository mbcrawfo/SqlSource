// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
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

        Problems(run)
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

        Problems(run)
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

        Problems(run)
            .ShouldBe([
                "SQLSRC276 /app/Repo/Sample.cs(5,6)-(5,23): The query methods of 'App.Sample' cannot go into the "
                    + "extension class 'App.SampleExtensions', because the project has a type of that name.  Rename "
                    + "the type or what has the name, or set the attribute's MethodLocation to put the methods on the "
                    + "type.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
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

        Problems(run)
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

        Problems(run)
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

    // The diagnostics of a run whose generated code compiles with no error and no warning.
    private static IReadOnlyList<string> Problems(GeneratorRun run)
    {
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        return run.Diagnostics;
    }
}
