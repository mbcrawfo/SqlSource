// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Models;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The models that two queries or two types share by their full name, the differences that keep them apart
// (SQLSRC257), the names that cannot be used (SQLSRC258), a property named as its model (SQLSRC256) and a type that is
// not public in a public model (SQLSRC251).  Each run is at Models under postgres on Npgsql 10, with an entry written
// for each query's current hash.
public sealed class SharedModelTests
{
    private const string APath = "/app/Repo/A.sql";

    private const string BPath = "/app/Repo/B.sql";

    // The name of GetUser in Users, of GetA in A and of GetB in B.
    private const string GetUserName = $"{GeneratorHarness.UsersPath}(1,10)-(1,17)";

    private const string GetAName = "/app/Repo/A.sql(1,10)-(1,14)";

    private const string GetBName = "/app/Repo/B.sql(1,10)-(1,14)";

    private const string Help257 = "  Give one of the two queries another model name, or make the two the same.";

    private const string Help258 = "  Give the query another model name, or set another model namespace.";

    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
    );

    [Fact]
    public void Run_TwoQueriesOfOneModelName_ShareOneModel()
    {
        var (a, b) = OfOneName("", "");

        var run = Run(Source, [a, Sidecar(a, "GetA", [Column("id")]), b, Sidecar(b, "GetB", [Column("id")])]);

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.UserRow-Model.g.cs"]);
        run.Sources["App.UserRow-Model.g.cs"]
            .ShouldContain(
                "    /// A row of the queries <c>GetA</c> from <c>A.sql</c> and <c>GetB</c> from <c>B.sql</c>.\n"
            );
    }

    // The first query's summary and its columns' documentation, although the second's column comes from elsewhere.
    [Fact]
    public void Run_SharedModel_IsDocumentedByTheFirstQuery()
    {
        var (a, b) = OfOneName("-- summary: Loads a user.\n", "-- summary: Loads an account.\n");

        var run = Run(
            Source,
            [
                a,
                Sidecar(a, "GetA", [Column("id", origin: new SidecarOrigin("public", "users", "id"))]),
                b,
                Sidecar(b, "GetB", [Column("id", origin: new SidecarOrigin("public", "accounts", "id"))]),
            ]
        );

        run.ShouldBeClean();
        run.Sources["App.UserRow-Model.g.cs"]
            .ShouldBe(
                $$"""
                {{GeneratedFile.ModelHeader}}
                namespace App
                {
                    /// <summary>
                    /// Loads a user.
                    /// A row of the queries <c>GetA</c> from <c>A.sql</c> and <c>GetB</c> from <c>B.sql</c>.
                    /// </summary>
                    /// <param name="Id">The column <c>id</c>, <c>integer</c>, from <c>public.users.id</c>.</param>
                    public sealed record UserRow(int Id);
                }

                """
            );
    }

    // The first difference is reported at the later query's marker, and the model is the first query's alone, named
    // after it alone.
    [Fact]
    public void Run_ModelsOfOneNameThatDifferInKind_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "-- output-model-type: class\n",
            [Column("id")],
            [Column("id")],
            "it is a 'class' here and a 'sealed record' there"
        );

    [Fact]
    public void Run_ModelsOfOneNameOfWhichTheLaterIsSorted_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "-- generator: sort-output\n",
            [Column("id")],
            [Column("id")],
            "its properties are sorted here and not there"
        );

    [Fact]
    public void Run_ModelsOfOneNameOfWhichTheEarlierIsSorted_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "-- generator: sort-output\n",
            "",
            [Column("id")],
            [Column("id")],
            "its properties are sorted there and not here"
        );

    [Fact]
    public void Run_ModelsOfOneNameOfWhichTheLaterHasMoreProperties_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "",
            [Column("id")],
            [Column("id"), Column("name")],
            "it has 2 properties here and 1 there"
        );

    [Fact]
    public void Run_ModelsOfOneNameOfWhichTheLaterHasFewerProperties_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "",
            [Column("id"), Column("name")],
            [Column("id")],
            "it has 1 property here and 2 there"
        );

    [Fact]
    public void Run_ModelsOfOneNameThatDifferInAPropertysName_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "",
            [Column("id")],
            [Column("name")],
            "its property 1 is 'Name' here and 'Id' there"
        );

    [Fact]
    public void Run_ModelsOfOneNameThatDifferInAPropertysType_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "",
            [Column("id")],
            [Column("id", Text)],
            "the property 'Id' is 'string' here and 'int' there"
        );

    [Fact]
    public void Run_ModelsOfOneNameOfWhichTheLaterIsNullable_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "",
            [Column("id")],
            [Column("id", nullable: true)],
            "the property 'Id' is nullable here and not there"
        );

    [Fact]
    public void Run_ModelsOfOneNameOfWhichTheEarlierIsNullable_AreSqlsrc257AtTheLaterMarker() =>
        ShouldBeSqlsrc257AtTheLaterMarker(
            "",
            "",
            [Column("id", nullable: true)],
            [Column("id")],
            "the property 'Id' is nullable there and not here"
        );

    // GetB has no marker, so its own name is where the error is.
    [Fact]
    public void Run_LaterQueryWithoutAMarker_IsSqlsrc257AtItsName()
    {
        var a = new SqlFile(APath, "-- name: GetA\n-- output-model: GetBDto\nSELECT 1;\n");
        var b = new SqlFile(BPath, "-- name: GetB\nSELECT 2;\n");

        var run = Run(Source, [a, Sidecar(a, "GetA", [Column("id")]), b, Sidecar(b, "GetB", [Column("name")])]);

        run.Diagnostics.ShouldBe([
            $"SQLSRC257 {GetBName}: The output model 'App.GetBDto' of the query 'GetB' is not the same as the output "
                + "model of that name of the query 'GetA' in A.sql: its property 1 is 'Name' here and 'Id' "
                + $"there.{Help257}",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["App.GetBDto-Model.g.cs"]);
        run.Sources["App.GetBDto-Model.g.cs"].ShouldContain("record GetBDto(int Id);");
    }

    [Fact]
    public void Run_InputAndOutputModelOfOneName_AreSqlsrc257()
    {
        var a = new SqlFile(APath, "-- name: GetA\n-- output-model: User\nSELECT 1;\n");
        var b = new SqlFile(BPath, "-- name: GetB\n-- input-model: User\nSELECT id FROM users WHERE id = @id;\n");

        var run = Run(
            Source,
            [
                a,
                Sidecar(a, "GetA", [Column("id")]),
                b,
                Sidecars.Of(b, Entry(b, "GetB", [Parameter("id")], [Column("id")])),
            ]
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC257 /app/Repo/B.sql(2,1)-(2,21): The input model 'App.User' of the query 'GetB' is not the same as "
                + "the output model of that name of the query 'GetA' in A.sql: one is an input model and the other an "
                + $"output model.{Help257}",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["App.GetBDto-Model.g.cs", "App.User-Model.g.cs"]);
        run.Sources["App.User-Model.g.cs"].ShouldContain("    /// A row of the query <c>GetA</c> from <c>A.sql</c>.\n");
    }

    // Equal suffixes give one query's two models one name: the two are one query, so its type is named.
    [Fact]
    public void Run_EqualSuffixes_AreSqlsrc257ForTheType()
    {
        var run = Run(
            Source,
            [Users, Sidecars.Of(Users, Entry(Users, "GetUser", [Parameter("id")], [Column("id")]))],
            new Dictionary<string, string?>
            {
                ["SqlSourceInputModelSuffix"] = "Model",
                ["SqlSourceOutputModelSuffix"] = "Model",
            }
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC257 {GetUserName}: The output model 'App.GetUserModel' of the query 'GetUser' is not the same as "
                + "the input model of that name of the query 'GetUser' in Users.sql, for the type App.Sample: one is "
                + $"an input model and the other an output model.{Help257}",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["App.GetUserModel-Model.g.cs"]);
        run.Sources["App.GetUserModel-Model.g.cs"].ShouldContain("/// The parameters of the query <c>GetUser</c>");
    }

    // Repo comes before Repo<T> whatever the order of their declarations, so the same type's request is the first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Run_GenericAndPlainTypeOfOneName_AreOrderedByArity(bool genericFirst)
    {
        const string Generic = """
                [SqlSourceGenerate(OutputModelType = GeneratorModelType.Class)]
                public partial class Repo<T>;
            """;
        const string Plain = """
                [SqlSourceGenerate]
                public partial class Repo;
            """;
        var source =
            "using SqlSource;\n\nnamespace App\n{\n"
            + (genericFirst ? Generic + "\n\n" + Plain : Plain + "\n\n" + Generic)
            + "\n}\n";

        var run = Run(source, [Users, Sidecar(Users, "GetUser", [Column("id")])]);

        run.Diagnostics.ShouldBe([
            $"SQLSRC257 {GetUserName}: The output model 'App.GetUserDto' of the query 'GetUser' is not the same as the "
                + "output model of that name of the query 'GetUser' in Users.sql, for the type App.Repo: it is a "
                + $"'class' here and a 'sealed record' there.{Help257}",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.GetUserDto-Model.g.cs"].ShouldContain("public sealed record GetUserDto(int Id);");
    }

    // The earlier request is of a generic type, which the message names as it is declared.
    [Fact]
    public void Run_TwoGenericTypesOfOneName_NameTheEarlierWithItsTypeParameters()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(OutputModelType = GeneratorModelType.Class)]
                public partial class Repo<TKey, TValue>;

                [SqlSourceGenerate]
                public partial class Repo<TKey>;
            }
            """;

        var run = Run(source, [Users, Sidecar(Users, "GetUser", [Column("id")])]);

        run.Diagnostics.ShouldBe([
            $"SQLSRC257 {GetUserName}: The output model 'App.GetUserDto' of the query 'GetUser' is not the same as the "
                + "output model of that name of the query 'GetUser' in Users.sql, for the type App.Repo<TKey>: it is a "
                + $"'class' here and a 'sealed record' there.{Help257}",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.GetUserDto-Model.g.cs"].ShouldContain("public sealed record GetUserDto(int Id);");
    }

    [Fact]
    public void Run_TwoTypesOfOneNamespace_ShareOneModel()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class First;

                [SqlSourceGenerate]
                public partial class Second;
            }
            """;

        var run = Run(source, [Users, Sidecar(Users, "GetUser", [Column("id")])]);

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserDto-Model.g.cs"]);
        run.Sources["App.GetUserDto-Model.g.cs"]
            .ShouldContain("    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.\n");
    }

    [Fact]
    public void Run_TwoTypesOfTwoNamespaces_HaveAModelEach()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class First;
            }

            namespace Other
            {
                [SqlSourceGenerate]
                public partial class Second;
            }
            """;

        var run = Run(source, [Users, Sidecar(Users, "GetUser", [Column("id")])]);

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserDto-Model.g.cs", "Other.GetUserDto-Model.g.cs"]);
    }

    // The first type is internal, and the model takes the wider accessibility of the two.
    [Fact]
    public void Run_PublicAndInternalTypeSharing_GiveAPublicModel()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                internal partial class First;

                [SqlSourceGenerate]
                public partial class Second;
            }
            """;

        var run = Run(source, [Users, Sidecar(Users, "GetUser", [Column("id")])]);

        run.ShouldBeClean();
        run.Sources["App.GetUserDto-Model.g.cs"].ShouldContain("    public sealed record GetUserDto(int Id);\n");
    }

    // C# tells the two names apart, and the compiler, which compares the names of generated files ignoring case,
    // would throw on two files whose names are equal so: each file's name is made unique.
    [Fact]
    public void Run_FullNamesThatDifferByCase_GetFilesOfTheirOwn()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- output-model: UserDto\nSELECT 1;\n\n-- name: GetOther\n-- output-model: UserDTO\n"
                + "SELECT 2;\n"
        );

        var run = Run(
            Source,
            [
                file,
                Sidecars.Of(
                    file,
                    Entry(file, "GetUser", [], [Column("id")]),
                    Entry(file, "GetOther", [], [Column("other")])
                ),
            ]
        );

        run.ShouldBeClean();
        var files = ModelFiles(run);
        files.Length.ShouldBe(2);
        files.ShouldAllBe(name => name.StartsWith("App.User", StringComparison.OrdinalIgnoreCase));
        run.Sources[files.Single(name => name.StartsWith("App.UserDto", StringComparison.Ordinal))]
            .ShouldContain("record UserDto(int Id);");
        run.Sources[files.Single(name => name.StartsWith("App.UserDTO", StringComparison.Ordinal))]
            .ShouldContain("record UserDTO(int Other);");
    }

    [Fact]
    public void Run_ModelNamedAsATypeOfTheProject_IsSqlsrc258()
    {
        var source = Source + "\n\nnamespace App\n{\n    public class GetUserDto;\n}\n";

        var run = Run(source, [Users, Sidecar(Users, "GetUser", [Column("id")])]);

        AssertUnusable(
            run,
            $"SQLSRC258 {GetUserName}: The model 'App.GetUserDto' of the query 'GetUser' cannot be generated: a type "
                + $"of this project has that full name.{Help258}"
        );
    }

    [Fact]
    public void Run_ModelNamedAsTheAttributedType_IsSqlsrc258()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n-- output-model: Sample\nSELECT 1;\n");

        var run = Run(Source, [file, Sidecar(file, "GetUser", [Column("id")])]);

        AssertUnusable(
            run,
            $"SQLSRC258 {GetUserName}: The model 'App.Sample' of the query 'GetUser' cannot be generated: a type of "
                + $"this project has that full name.{Help258}"
        );
    }

    [Fact]
    public void Run_ModelNamedAsANamespaceOfTheProject_IsSqlsrc258()
    {
        var source = Source + "\n\nnamespace App.Rows\n{\n    public class Row;\n}\n";
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n-- output-model: Rows\nSELECT 1;\n");

        var run = Run(source, [file, Sidecar(file, "GetUser", [Column("id")])]);

        AssertUnusable(
            run,
            $"SQLSRC258 {GetUserName}: The model 'App.Rows' of the query 'GetUser' cannot be generated: it is a "
                + $"namespace of this project.{Help258}"
        );
    }

    [Fact]
    public void Run_ModelNamespaceThatIsATypeOfTheProject_IsSqlsrc258()
    {
        var source = Source + "\n\nnamespace App\n{\n    public class Queries;\n}\n";

        var run = Run(
            source,
            [Users, Sidecar(Users, "GetUser", [Column("id")])],
            new Dictionary<string, string?> { ["SqlSourceModelNamespace"] = "App.Queries" }
        );

        AssertUnusable(
            run,
            $"SQLSRC258 {GetUserName}: The model 'App.Queries.GetUserDto' of the query 'GetUser' cannot be generated: "
                + $"'App.Queries' is a type of this project, and a part of its namespace.{Help258}"
        );
    }

    // Each of the two is CS0101 for the other, so neither is generated.
    [Fact]
    public void Run_ModelNamedAsAnotherModelsNamespace_IsSqlsrc258ForBoth()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetA\n-- output-model: Data\nSELECT 1;\n\n-- name: GetB\n-- output-model: App.Data.Row\n"
                + "SELECT 2;\n"
        );

        var run = Run(
            Source,
            [file, Sidecars.Of(file, Entry(file, "GetA", [], [Column("id")]), Entry(file, "GetB", [], [Column("id")]))]
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC258 {GeneratorHarness.UsersPath}(1,10)-(1,14): The model 'App.Data' of the query 'GetA' cannot be "
                + $"generated: it is the namespace of another model.{Help258}",
            $"SQLSRC258 {GeneratorHarness.UsersPath}(5,10)-(5,14): The model 'App.Data.Row' of the query 'GetB' cannot "
                + $"be generated: 'App.Data' is another model, and a part of its namespace.{Help258}",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBeEmpty();
    }

    // CS8981.  The model is shared, so each query that joined it is told.
    [Fact]
    public void Run_ModelNamedInLowerCaseLettersAlone_IsSqlsrc258AtEachQuery()
    {
        var (a, b) = OfOneName("", "", "usersdto");

        var run = Run(Source, [a, Sidecar(a, "GetA", [Column("id")]), b, Sidecar(b, "GetB", [Column("id")])]);

        run.Diagnostics.ShouldBe(
            new[] { (At: GetAName, Name: "GetA"), (At: GetBName, Name: "GetB") }.Select(query =>
                $"SQLSRC258 {query.At}: The model 'App.usersdto' of the query '{query.Name}' cannot be generated: the "
                + $"compiler warns on a type name of lower-case ASCII letters alone.{Help258}"
            )
        );
        run.SeverityOf("SQLSRC258").ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBeEmpty();
    }

    // CS0542.
    [Fact]
    public void Run_ColumnNamedAsItsModel_IsSqlsrc256()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n-- output-model: Total\nSELECT 1;\n");

        var run = Run(Source, [file, Sidecar(file, "GetUser", [Column("total")])]);

        AssertUnusable(
            run,
            $"SQLSRC256 {GetUserName}: The column 'total' gives the property name 'Total', which is the name of its "
                + "model.  Give it an alias."
        );
    }

    [Fact]
    public void Run_ParameterNamedAsItsModel_IsSqlsrc256()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- input-model: Id\nSELECT 1 WHERE @id = 1;\n"
        );

        var run = Run(Source, [file, Sidecars.Of(file, Entry(file, "GetUser", [Parameter("id")], null))]);

        AssertUnusable(
            run,
            $"SQLSRC256 {GetUserName}: The parameter 'id' gives the property name 'Id', which is the name of its "
                + "model.  Rename it."
        );
    }

    [Fact]
    public void Run_InternalMappedTypeInAPublicModel_IsSqlsrc251AtTheQuery()
    {
        var run = Run(
            Source + "\n\nnamespace MyApp\n{\n    internal readonly struct Inner;\n}\n",
            [Users, Sidecar(Users, "GetUser", [Column("id")])],
            new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "int4=MyApp.Inner" }
        );

        AssertUnusable(
            run,
            $"SQLSRC251 {GetUserName}: The type override maps 'int4' to 'MyApp.Inner' for column 'id', in the property "
                + "SqlSourceTypeOverrides, and that type is not public, and the model 'App.GetUserDto' is.  Make the "
                + "type public, or make each type that claims the query's file internal."
        );
    }

    // Each property whose type is not public is an error of its own.
    [Fact]
    public void Run_TwoInternalMappedTypesInAPublicModel_AreTwoSqlsrc251AtTheQuery()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT id, name FROM users;\n");

        var run = Run(
            Source
                + "\n\nnamespace MyApp\n{\n    internal readonly struct Inner;\n\n"
                + "    internal sealed class Secret;\n}\n",
            [file, Sidecar(file, "GetUser", [Column("id"), Column("name", Text)])],
            new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "int4=MyApp.Inner text=MyApp.Secret" }
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC251 {GetUserName}: The type override maps 'int4' to 'MyApp.Inner' for column 'id', in the property "
                + "SqlSourceTypeOverrides, and that type is not public, and the model 'App.GetUserDto' is.  Make the "
                + "type public, or make each type that claims the query's file internal.",
            $"SQLSRC251 {GetUserName}: The type override maps 'text' to 'MyApp.Secret' for column 'name', in the "
                + "property SqlSourceTypeOverrides, and that type is not public, and the model 'App.GetUserDto' is.  "
                + "Make the type public, or make each type that claims the query's file internal.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBeEmpty();
    }

    // The two queries share the model, and each is told of its own column, key and place.
    [Fact]
    public void Run_InternalMappedTypeInASharedModel_IsSqlsrc251OfEachQuerysOwnColumn()
    {
        var a = new SqlFile(
            APath,
            "-- name: GetA\n-- output-model: UserRow\n-- type-override: int4 = App.UserKey\nSELECT 1;\n"
        );
        var b = new SqlFile(BPath, "-- name: GetB\n-- output-model: UserRow\nSELECT 2;\n");

        var run = Run(
            Source + "\n\nnamespace App\n{\n    internal readonly struct UserKey;\n}\n",
            [a, Sidecar(a, "GetA", [Column("id")]), b, Sidecar(b, "GetB", [Column("Id", Text)])],
            new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "text=App.UserKey" }
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC251 {GetAName}: The type override maps 'int4' to 'App.UserKey' for column 'id', in a "
                + "-- type-override: marker of the query, and that type is not public, and the model 'App.UserRow' "
                + "is.  Make the type public, or make each type that claims the query's file internal.",
            $"SQLSRC251 {GetBName}: The type override maps 'text' to 'App.UserKey' for column 'Id', in the property "
                + "SqlSourceTypeOverrides, and that type is not public, and the model 'App.UserRow' is.  Make the type "
                + "public, or make each type that claims the query's file internal.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBeEmpty();
    }

    [Fact]
    public void Run_ColumnsNamedAsASharedModel_AreSqlsrc256OfEachQuerysOwnColumn()
    {
        var (a, b) = OfOneName("", "", "Total");

        var run = Run(Source, [a, Sidecar(a, "GetA", [Column("total")]), b, Sidecar(b, "GetB", [Column("Total")])]);

        run.Diagnostics.ShouldBe([
            $"SQLSRC256 {GetAName}: The column 'total' gives the property name 'Total', which is the name of its "
                + "model.  Give it an alias.",
            $"SQLSRC256 {GetBName}: The column 'Total' gives the property name 'Total', which is the name of its "
                + "model.  Give it an alias.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBeEmpty();
    }

    [Fact]
    public void Run_InternalMappedTypeInAnInternalModel_Compiles()
    {
        var source = Source.Replace("public partial class", "internal partial class", StringComparison.Ordinal);

        var run = Run(
            source + "\n\nnamespace MyApp\n{\n    internal readonly struct Inner;\n}\n",
            [Users, Sidecar(Users, "GetUser", [Column("id")])],
            new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "int4=MyApp.Inner" }
        );

        run.ShouldBeClean();
        run.Sources["App.GetUserDto-Model.g.cs"]
            .ShouldContain("    internal sealed record GetUserDto(global::MyApp.Inner Id);\n");
    }

    // The models step names each query's model for the methods, and each query's entry gives its column names.
    [Fact]
    public void Run_TableModelOfTwoQueries_IsAssignedToEach()
    {
        var a = new SqlFile(APath, "-- name: GetA\nSELECT id, name FROM users;\n");
        var b = new SqlFile(BPath, "-- name: GetB\nSELECT id, name FROM users;\n");
        var table = new SidecarTable("public", "users");
        SidecarColumn[] columns = [Column("id"), Column("name", Text)];
        var run = Run(
            Source,
            [
                a,
                Sidecars.Of(a, Entry(a, "GetA", [], columns, table)),
                b,
                Sidecars.Of(b, Entry(b, "GetB", [], columns, table)),
            ]
        );

        run.ShouldBeClean();
        var assignments = StepOutputs.Single<ModelSet>(run, TrackingNames.Models).Assignments;
        var files = StepOutputs.Of<FileModels>(run, TrackingNames.FileModels);
        files.Length.ShouldBe(2);
        assignments.Count.ShouldBe(2);
        foreach (var file in files)
        {
            var query = file.Queries.ShouldHaveSingleItem();
            query.ColumnNames.ShouldBe(["id", "name"]);
            var assignment = assignments
                .Where(item => item.NormalizedPath == file.NormalizedPath)
                .ShouldHaveSingleItem();
            assignment.ClaimingType.ShouldBe("App.Sample");
            assignment.QueryIndex.ShouldBe(query.Index);
            assignment.Role.ShouldBe(ModelRole.Output);
            assignment.TypeText.ShouldBe("global::App.UsersDto");
            assignment.Kind.ShouldBe(ModelKind.SealedRecord);
        }
    }

    // GetA in A and GetB in B, each with the -- output-model: marker of the name, then the markers given.
    private static (SqlFile A, SqlFile B) OfOneName(string aMarkers, string bMarkers, string name = "UserRow") =>
        (
            new SqlFile(APath, $"-- name: GetA\n-- output-model: {name}\n{aMarkers}SELECT 1;\n"),
            new SqlFile(BPath, $"-- name: GetB\n-- output-model: {name}\n{bMarkers}SELECT 2;\n")
        );

    // GetA in A and GetB in B, of one model name, with the markers and the columns given, are SQLSRC257 at B's
    // marker with the message, and the model is GetA's alone.
    private static void ShouldBeSqlsrc257AtTheLaterMarker(
        string aMarkers,
        string bMarkers,
        SidecarColumn[] aColumns,
        SidecarColumn[] bColumns,
        string message
    )
    {
        var (a, b) = OfOneName(aMarkers, bMarkers);

        var run = Run(Source, [a, Sidecar(a, "GetA", aColumns), b, Sidecar(b, "GetB", bColumns)]);

        run.Diagnostics.ShouldBe([
            "SQLSRC257 /app/Repo/B.sql(2,1)-(2,25): The output model 'App.UserRow' of the query 'GetB' is not the same "
                + $"as the output model of that name of the query 'GetA' in A.sql: {message}.{Help257}",
        ]);
        run.SeverityOf("SQLSRC257").ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["App.UserRow-Model.g.cs"]);
        run.Sources["App.UserRow-Model.g.cs"]
            .ShouldContain("    /// A row of the query <c>GetA</c> from <c>A.sql</c>.\n");
    }

    // The one error, and no model, so that nothing reaches the compiler.
    private static void AssertUnusable(GeneratorRun run, string diagnostic)
    {
        run.Diagnostics.ShouldBe([diagnostic]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBeEmpty();
    }

    // The sidecar of the file with an entry of the query that has rows of the columns and no parameters.
    private static SqlFile Sidecar(SqlFile file, string name, SidecarColumn[] columns) =>
        Sidecars.Of(file, Entry(file, name, [], columns));
}
