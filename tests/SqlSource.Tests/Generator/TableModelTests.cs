// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The model of a query whose entry matches a table: named after the table, shared by every query that matches it,
// unless no-table-models is in effect or the query names its model.  Each run is at Models under postgres on Npgsql
// 10, with an entry written for each query's current hash, and every model must compile without a warning.
public sealed class TableModelTests
{
    private const string APath = "/app/Repo/A.sql";

    private const string BPath = "/app/Repo/B.sql";

    private const string GetBName = "/app/Repo/B.sql(1,10)-(1,14)";

    private const string Help257 = "  Give one of the two queries another model name, or make the two the same.";

    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
    );

    private static readonly SidecarTable PublicUsers = new("public", "users");

    // The levels that give the generator parameters of a query, from the least specific.
    public static TheoryData<SettingLevel> Levels =>
        [SettingLevel.Property, SettingLevel.Metadata, SettingLevel.Attribute, SettingLevel.Header, SettingLevel.Query];

    [Fact]
    public void Run_QueryThatMatchesATable_GetsAModelNamedAfterTheTable()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("id")], [Column("id")], PublicUsers));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserParams-Model.g.cs", "App.UsersDto-Model.g.cs"]);
        run.Sources["App.UsersDto-Model.g.cs"]
            .ShouldBe(
                $$"""
                {{GeneratedFile.ModelHeader}}
                namespace App
                {
                    /// <summary>
                    /// A row of the table <c>public.users</c>, read by the query <c>GetUser</c> from <c>Users.sql</c>.
                    /// </summary>
                    /// <param name="Id">The column <c>id</c>, <c>integer</c>.</param>
                    public sealed record UsersDto(int Id);
                }

                """
            );
    }

    // The table's summary comes after the query's -- summary:.
    [Fact]
    public void Run_TableModelOfAQueryWithASummary_IsDocumentedWithItFirst()
    {
        var file = Users with { Text = "-- name: GetUser\n-- summary: Loads a user.\nSELECT id FROM users;\n" };

        var run = Run(file, Entry(file, "GetUser", [], [Column("id")], PublicUsers));

        run.ShouldBeClean();
        run.Sources["App.UsersDto-Model.g.cs"]
            .ShouldContain(
                "    /// Loads a user.\n"
                    + "    /// A row of the table <c>public.users</c>, read by the query <c>GetUser</c> from "
                    + "<c>Users.sql</c>.\n"
            );
    }

    [Fact]
    public void Run_TableOfAnotherSchema_HasTheSchemaBeforeItsName()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id")], new SidecarTable("billing", "invoices")));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.BillingInvoicesDto-Model.g.cs"]);
        run.Sources["App.BillingInvoicesDto-Model.g.cs"]
            .ShouldContain("    /// A row of the table <c>billing.invoices</c>, read by the query ");
        run.Sources["App.BillingInvoicesDto-Model.g.cs"].ShouldContain("public sealed record BillingInvoicesDto(");
    }

    [Fact]
    public void Run_OutputSuffix_FollowsTheTablesName()
    {
        var run = Run(
            Users,
            [Entry(Users, "GetUser", [], [Column("id")], new SidecarTable("public", "order_lines"))],
            new Dictionary<string, string?> { ["SqlSourceOutputModelSuffix"] = "Row" }
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.OrderLinesRow-Model.g.cs"]);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Run_NoTableModelsAtEachLevel_NamesTheModelAfterTheQuery(SettingLevel level)
    {
        var settings = new LevelSettings().At(level, Setting.GeneratorParameters, "no-table-models");
        var file = settings.File(Users);

        var run = Run(
            settings.Source(Source),
            [file, Sidecars.Of(file, Entry(file, "GetUser", [], [Column("id")], PublicUsers))],
            settings.Properties
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserDto-Model.g.cs"]);
        run.Sources["App.GetUserDto-Model.g.cs"]
            .ShouldContain("    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.\n");
    }

    // A parameter list that does not name no-table-models leaves table models on, whatever else it holds.
    [Fact]
    public void Run_OtherParameters_LeaveTableModelsOn()
    {
        var run = Run(
            Users,
            [Entry(Users, "GetUser", [], [Column("id")], PublicUsers)],
            new Dictionary<string, string?> { ["SqlSourceGeneratorParameters"] = "sort-output" }
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.UsersDto-Model.g.cs"]);
    }

    [Fact]
    public void Run_OutputModelMarkerOnAMatch_WinsOverTheTable()
    {
        var file = Users with { Text = "-- name: GetUser\n-- output-model: UserRow\nSELECT id FROM users;\n" };

        var run = Run(file, Entry(file, "GetUser", [], [Column("id")], PublicUsers));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.UserRow-Model.g.cs"]);
        run.Sources["App.UserRow-Model.g.cs"]
            .ShouldContain("    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.\n");
    }

    // The input model is the query's, whatever the table.
    [Fact]
    public void Run_InputModelMarkerOnAMatch_NamesTheInputModelAlone()
    {
        var file = Users with
        {
            Text = "-- name: GetUser\n-- input-model: UserKey\nSELECT id FROM users WHERE id = @id;\n",
        };

        var run = Run(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")], PublicUsers));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.UserKey-Model.g.cs", "App.UsersDto-Model.g.cs"]);
    }

    // The user can do nothing about the table's name, so the query's model is named after the query, with no error.
    [Theory]
    [InlineData("public", "order-lines")]
    [InlineData("my-schema", "orders")]
    [InlineData("public", "2fa")]
    [InlineData("public", "users?")]
    [InlineData("public", "users!")]
    [InlineData("billing?", "invoices")]
    [InlineData("public", "")]
    [InlineData(null, "users")]
    [InlineData("public", null)]
    public void Run_TableThatGivesNoIdentifier_NamesTheModelAfterTheQuery(string? schema, string? table)
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id")], new SidecarTable(schema, table)));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserDto-Model.g.cs"]);
        run.Sources["App.GetUserDto-Model.g.cs"]
            .ShouldContain("    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.\n");
    }

    [Fact]
    public void Run_TwoQueriesThatMatchOneTable_ShareOneModelThatNamesBoth()
    {
        var (a, b) = OfOneTable();

        var run = Run(Source, [a, Sidecar(a, "GetA", [Column("id")]), b, Sidecar(b, "GetB", [Column("id")])]);

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.UsersDto-Model.g.cs"]);
        run.Sources["App.UsersDto-Model.g.cs"]
            .ShouldContain(
                "    /// A row of the table <c>public.users</c>, read by the queries <c>GetA</c> from <c>A.sql</c> "
                    + "and <c>GetB</c> from <c>B.sql</c>.\n"
            );
    }

    [Fact]
    public void Run_TwoQueriesThatMatchOneTableAndDiffer_AreSqlsrc257AtTheLaterOne()
    {
        var (a, b) = OfOneTable("", "-- type-override: int4=long\n");

        var run = Run(Source, [a, Sidecar(a, "GetA", [Column("id")]), b, Sidecar(b, "GetB", [Column("id")])]);

        run.Diagnostics.ShouldBe([
            $"SQLSRC257 {GetBName}: The output model 'App.UsersDto' of the query 'GetB' is not the same as the output "
                + "model of that name of the query 'GetA' in A.sql: the property 'Id' is 'long' here and 'int' "
                + $"there.{Help257}",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["App.UsersDto-Model.g.cs"]);
        run.Sources["App.UsersDto-Model.g.cs"].ShouldContain("record UsersDto(int Id);");
        run.Sources["App.UsersDto-Model.g.cs"].ShouldContain("read by the query <c>GetA</c> from <c>A.sql</c>.\n");
    }

    // The names of both tables are ABC, and the models agree, so the two share one model that names both tables.
    [Fact]
    public void Run_TwoTablesOfOneModelName_ShareOneModelThatNamesBothTables()
    {
        var (a, b) = OfOneTable();

        var run = Run(
            Source,
            [
                a,
                Sidecar(a, "GetA", new SidecarTable("a_b", "c"), [Column("id")]),
                b,
                Sidecar(b, "GetB", new SidecarTable("a", "b_c"), [Column("id")]),
            ]
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.ABCDto-Model.g.cs"]);
        run.Sources["App.ABCDto-Model.g.cs"]
            .ShouldContain(
                "    /// A row of the tables <c>a_b.c</c> and <c>a.b_c</c>, read by the queries <c>GetA</c> from "
                    + "<c>A.sql</c> and <c>GetB</c> from <c>B.sql</c>.\n"
            );
    }

    // Three tables, each once although a table is read by two queries.
    [Fact]
    public void Run_ThreeTablesOfOneModelName_AreNamedOnceEach()
    {
        var c = new SqlFile("/app/Repo/C.sql", "-- name: GetC\nSELECT id FROM users;\n");
        var d = new SqlFile("/app/Repo/D.sql", "-- name: GetD\nSELECT id FROM users;\n");
        var (a, b) = OfOneTable();

        var run = Run(
            Source,
            [
                a,
                Sidecar(a, "GetA", new SidecarTable("a_b", "c"), [Column("id")]),
                b,
                Sidecar(b, "GetB", new SidecarTable("a", "b_c"), [Column("id")]),
                c,
                Sidecar(c, "GetC", new SidecarTable("a_b", "c"), [Column("id")]),
                d,
                Sidecar(d, "GetD", new SidecarTable("A_b", "c"), [Column("id")]),
            ]
        );

        run.ShouldBeClean();
        run.Sources["App.ABCDto-Model.g.cs"]
            .ShouldContain(
                "    /// A row of the tables <c>a_b.c</c>, <c>a.b_c</c> and <c>A_b.c</c>, read by the queries "
                    + "<c>GetA</c> from <c>A.sql</c>, <c>GetB</c> from <c>B.sql</c>, <c>GetC</c> from <c>C.sql</c> "
                    + "and <c>GetD</c> from <c>D.sql</c>.\n"
            );
    }

    // A model of each namespace, and one when the namespace setting gives them one.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_TwoNamespacesThatMatchOneTable_HaveAModelEachUnlessTheyShareANamespace(bool withNamespace)
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

        var run = Run(
            source,
            [Users, Sidecars.Of(Users, Entry(Users, "GetUser", [], [Column("id")], PublicUsers))],
            withNamespace ? new Dictionary<string, string?> { ["SqlSourceModelNamespace"] = "Shared" } : null
        );

        run.ShouldBeClean();
        ModelFiles(run)
            .ShouldBe(
                withNamespace
                    ? ["Shared.UsersDto-Model.g.cs"]
                    : ["App.UsersDto-Model.g.cs", "Other.UsersDto-Model.g.cs"]
            );
    }

    // The table model users and the model of a query Users are both UsersDto: one model when they are the same.
    [Fact]
    public void Run_TableModelAndAQueryOfTheSameName_ShareOneModelWhenTheyAreEqual()
    {
        var (a, b) = OfATableAndAQueryNamedUsers();

        var run = Run(
            Source,
            [a, Sidecar(a, "GetA", PublicUsers, [Column("id")]), b, Sidecar(b, "Users", null, [Column("id")])]
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.UsersDto-Model.g.cs"]);
        run.Sources["App.UsersDto-Model.g.cs"]
            .ShouldContain(
                "    /// A row of the table <c>public.users</c>, read by the query <c>GetA</c> from <c>A.sql</c>. "
                    + "Also a row of the query <c>Users</c> from <c>B.sql</c>.\n"
            );
    }

    [Fact]
    public void Run_TableModelAndAQueryOfTheSameNameThatDiffer_AreSqlsrc257AtTheLaterOne()
    {
        var (a, b) = OfATableAndAQueryNamedUsers();

        var run = Run(
            Source,
            [a, Sidecar(a, "GetA", PublicUsers, [Column("id")]), b, Sidecar(b, "Users", null, [Column("name")])]
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC257 {GetBName.Replace("(1,10)-(1,14)", "(1,10)-(1,15)", StringComparison.Ordinal)}: The output "
                + "model 'App.UsersDto' of the query 'Users' is not the same as the output model of that name of the "
                + "query 'GetA' in A.sql: its property 1 is 'Name' here and 'Id' there."
                + Help257,
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["App.UsersDto-Model.g.cs"]);
    }

    // GetA in A and GetB in B, each reading public.users, then the markers given.
    private static (SqlFile A, SqlFile B) OfOneTable(string aMarkers = "", string bMarkers = "") =>
        (
            new SqlFile(APath, $"-- name: GetA\n{aMarkers}SELECT id FROM users;\n"),
            new SqlFile(BPath, $"-- name: GetB\n{bMarkers}SELECT id FROM users;\n")
        );

    // GetA in A reads public.users, and the query Users in B reads nothing a table gives a name to.
    private static (SqlFile A, SqlFile B) OfATableAndAQueryNamedUsers() =>
        (
            new SqlFile(APath, "-- name: GetA\nSELECT id FROM users;\n"),
            new SqlFile(BPath, "-- name: Users\nSELECT 1;\n")
        );

    // The sidecar of the file with an entry of the query that has rows of the columns of public.users.
    private static SqlFile Sidecar(SqlFile file, string name, SidecarColumn[] columns) =>
        Sidecar(file, name, PublicUsers, columns);

    private static SqlFile Sidecar(SqlFile file, string name, SidecarTable? table, SidecarColumn[] columns) =>
        Sidecars.Of(file, Entry(file, name, [], columns, table));
}
