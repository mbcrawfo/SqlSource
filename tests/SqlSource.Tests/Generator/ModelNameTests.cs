// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Models;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The properties of each query's models, their names and nullability, and the errors of their names, read from what
// the step of the models gave.  Each run is at Models under postgres on Npgsql 10, with an entry written for each
// query's current hash.
public sealed class ModelNameTests
{
    private const string UsersPath = "/app/Repo/Users.sql";

    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;
        }
        """;

    // The name of GetUser in Users.
    private const string GetUserName = "/app/Repo/Users.sql(1,10)-(1,17)";

    private static readonly SqlFile Users = new(UsersPath, "-- name: GetUser\nSELECT 1;\n");

    private static readonly PostgresType Int4 = TestSidecars.Postgres(name: "integer", internalName: "int4");

    [Fact]
    public void Run_ColumnNames_ArePascalCase()
    {
        var run = Run(
            Users,
            Entry(Users, "GetUser", [], [Column("created_at"), Column("userId"), Column("ID"), Column("api_v2_url")])
        );

        run.Diagnostics.ShouldBeEmpty();
        NamesOf(QueryOf(run).Output).ShouldBe(["CreatedAt", "UserId", "ID", "ApiV2Url"]);
    }

    [Fact]
    public void Run_ParameterName_IsItsBareName()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("accountId"), Parameter("user_id")], null));

        run.Diagnostics.ShouldBeEmpty();
        NamesOf(QueryOf(run).Input).ShouldBe(["AccountId", "UserId"]);
    }

    [Fact]
    public void Run_ColumnNamedQuestionColumn_IsSqlsrc253()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id"), Column("?column?")]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC253 {GetUserName}: The column at position 2 of the query 'GetUser' has no name.  Give it an alias.",
        ]);
        run.SeverityOf("SQLSRC253").ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ColumnNotAnIdentifier_IsSqlsrc254()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("api-url")]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC254 {GetUserName}: The column 'api-url' gives the property name 'Api-url', which is not a C# "
                + "identifier.  Give it an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ParameterNotAnIdentifier_IsSqlsrc254WithRename()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("2fa")], null));

        run.Diagnostics.ShouldBe([
            $"SQLSRC254 {GetUserName}: The parameter '2fa' gives the property name '2fa', which is not a C# "
                + "identifier.  Rename it.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_TwoColumnsOneName_IsSqlsrc255()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id"), Column("id")]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC255 {GetUserName}: The column 'id' at position 1 and the column 'id' at position 2 give one "
                + "property name, 'Id', ignoring case.  Give one of them an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_UserIdBesideUserId_IsSqlsrc255()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("user_id"), Column("userId")]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC255 {GetUserName}: The column 'user_id' at position 1 and the column 'userId' at position 2 give "
                + "one property name, 'UserId', ignoring case.  Give one of them an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // The names differ only by case, which Dapper does not tell apart.
    [Fact]
    public void Run_TwoParametersOneName_IsSqlsrc255()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("a_b"), Parameter("aB")], null));

        run.Diagnostics.ShouldBe([
            $"SQLSRC255 {GetUserName}: The parameter 'a_b' at position 1 and the parameter 'aB' at position 2 give "
                + "one property name, 'AB', ignoring case.  Rename one of them.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("EqualityContract")]
    [InlineData("Equals")]
    [InlineData("GetHashCode")]
    [InlineData("ToString")]
    [InlineData("GetType")]
    [InlineData("MemberwiseClone")]
    [InlineData("Finalize")]
    [InlineData("ReferenceEquals")]
    [InlineData("PrintMembers")]
    [InlineData("Deconstruct")]
    [InlineData("Clone")]
    public void Run_ReservedName_IsSqlsrc256(string name)
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column(name)]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC256 {GetUserName}: The column '{name}' gives the property name '{name}', which a record or object "
                + "already has.  Give it an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ReservedParameterName_IsSqlsrc256WithRename()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("to_string")], null));

        run.Diagnostics.ShouldBe([
            $"SQLSRC256 {GetUserName}: The parameter 'to_string' gives the property name 'ToString', which a record "
                + "or object already has.  Rename it.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // The parser cannot see that a statement returns no rows, so the marker is reported once the entry says so.
    [Fact]
    public void Run_OutputModelOnNoRows_IsSqlsrc259AtTheMarker()
    {
        var file = new SqlFile(UsersPath, "-- name: DeleteUser\n-- output-model: UserRow\nDELETE FROM users;\n");

        var run = Run(file, Entry(file, "DeleteUser", [], null));

        run.Diagnostics.ShouldBe([
            "SQLSRC259 /app/Repo/Users.sql(2,1)-(2,25): The query 'DeleteUser' names an output model, and its "
                + "statement returns no rows.  Remove the -- output-model: marker.",
        ]);
        run.SeverityOf("SQLSRC259").ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // Every error of a query is reported, each at its name, its parameters' before its columns'.
    [Fact]
    public void Run_QueryWithSeveralErrors_ReportsEach()
    {
        var run = Run(
            Users,
            Entry(Users, "GetUser", [Parameter("2fa")], [Column("?column?"), Column("id"), Column("ID")])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC254 {GetUserName}: The parameter '2fa' gives the property name '2fa', which is not a C# "
                + "identifier.  Rename it.",
            $"SQLSRC253 {GetUserName}: The column at position 1 of the query 'GetUser' has no name.  Give it an alias.",
            $"SQLSRC255 {GetUserName}: The column 'id' at position 2 and the column 'ID' at position 3 give one "
                + "property name, 'Id', ignoring case.  Give one of them an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // A query whose names are good keeps its models beside one whose names are not.
    [Fact]
    public void Run_ErrorInOneQuery_LeavesTheOthersModels()
    {
        var file = new SqlFile(UsersPath, "-- name: GetUser\nSELECT 1;\n\n-- name: GetOther\nSELECT 2;\n");

        var run = Run(
            file,
            Entry(file, "GetUser", [], [Column("id"), Column("id")]),
            Entry(file, "GetOther", [], [Column("id")])
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC255 ");
        ModelsOf(run).Queries.ShouldHaveSingleItem().Name.ShouldBe("GetOther");
    }

    [Fact]
    public void Run_ColumnSuffixes_SetNullability()
    {
        var run = Run(
            Users,
            Entry(
                Users,
                "GetUser",
                [],
                [Column("a!", nullable: true), Column("b?", nullable: false), Column("c", nullable: null), Column("d")]
            )
        );

        run.Diagnostics.ShouldBeEmpty();
        QueryOf(run)
            .Output.ShouldNotBeNull()
            .Select(property => (property.Name, property.IsNullable))
            .ShouldBe([("A", false), ("B", true), ("C", true), ("D", false)]);
    }

    [Fact]
    public void Run_ParameterNullability_IsTrueOnly()
    {
        var run = Run(
            Users,
            Entry(
                Users,
                "GetUser",
                [Parameter("p", nullable: true), Parameter("q", nullable: false), Parameter("r", nullable: null)],
                null
            )
        );

        run.Diagnostics.ShouldBeEmpty();
        QueryOf(run)
            .Input.ShouldNotBeNull()
            .Select(property => (property.Name, property.IsNullable))
            .ShouldBe([("P", true), ("Q", false), ("R", false)]);
    }

    [Fact]
    public void Run_PropertyDocumentation_NamesTypeOriginAndFlags()
    {
        var numeric = TestSidecars.Postgres(name: "numeric(10,2)", internalName: "numeric");
        var run = Run(
            Users,
            Entry(
                Users,
                "GetUser",
                [Parameter("id")],
                [
                    Column("id!", origin: new SidecarOrigin("public", "users", "id"), identity: true),
                    Column("total", numeric, origin: new SidecarOrigin("public", "orders", "total"), computed: true),
                    Column("n"),
                    Column("name", origin: new SidecarOrigin("public", null, "name"), identity: false),
                ]
            )
        );

        run.Diagnostics.ShouldBeEmpty();
        var query = QueryOf(run);
        query
            .Input.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Documentation.ShouldBe("The parameter <c>id</c>, <c>integer</c>.");
        query
            .Output.ShouldNotBeNull()
            .Select(property => property.Documentation)
            .ShouldBe([
                "The column <c>id!</c>, <c>integer</c>, from <c>public.users.id</c>.  An identity column.",
                "The column <c>total</c>, <c>numeric(10,2)</c>, from <c>public.orders.total</c>.  A computed column.",
                "The column <c>n</c>, <c>integer</c>.",
                "The column <c>name</c>, <c>integer</c>.",
            ]);
    }

    [Fact]
    public void Run_PropertyDocumentation_IsEscaped()
    {
        var odd = TestSidecars.Postgres(
            name: "\"a<b>&c\"",
            schema: "public",
            internalName: "a<b>&c",
            kind: SidecarValues.Kind.Enum,
            labels: ["x"]
        );

        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id", odd)]));

        run.Diagnostics.ShouldBeEmpty();
        QueryOf(run)
            .Output.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Documentation.ShouldBe("The column <c>id</c>, <c>\"a&lt;b&gt;&amp;c\"</c>.");
    }

    [Fact]
    public void Run_Property_HasItsTypeAndSubject()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("user_id")], [Column("id")]));

        run.Diagnostics.ShouldBeEmpty();
        var query = QueryOf(run);
        var parameter = query.Input.ShouldNotBeNull().ShouldHaveSingleItem();
        parameter.Type.Text.ShouldBe("int");
        parameter.Subject.ShouldBe("parameter 'user_id'");
        var column = query.Output.ShouldNotBeNull().ShouldHaveSingleItem();
        column.Type.Text.ShouldBe("int");
        column.Subject.ShouldBe("column 'id'");
    }

    [Fact]
    public void Run_NoParameters_HasNoInputModel()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id")]));

        run.Diagnostics.ShouldBeEmpty();
        var query = QueryOf(run);
        query.Input.ShouldBeNull();
        _ = query.Output.ShouldNotBeNull().ShouldHaveSingleItem();
    }

    [Fact]
    public void Run_NoRows_HasNoOutputModel()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("id")], null));

        run.Diagnostics.ShouldBeEmpty();
        var query = QueryOf(run);
        _ = query.Input.ShouldNotBeNull().ShouldHaveSingleItem();
        query.Output.ShouldBeNull();
    }

    // SELECT FROM users returns rows of no column.
    [Fact]
    public void Run_NoColumns_HasAnEmptyOutputModel()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], []));

        run.Diagnostics.ShouldBeEmpty();
        QueryOf(run).Output.ShouldNotBeNull().ShouldBeEmpty();
    }

    // The query's type error is its one error, and its names are not looked at.
    [Fact]
    public void Run_QueryWithATypeError_HasNoModelsAndNoNameDiagnostic()
    {
        var regclass = TestSidecars.Postgres(name: "regclass", internalName: "regclass");

        var run = Run(Users, Entry(Users, "GetUser", [], [Column("relation", regclass), Column("?column?")]));

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith($"SQLSRC246 {GetUserName}: ");
        ModelsOf(run).Queries.ShouldBeEmpty();
        ModelsOf(run).Diagnostics.ShouldBeEmpty();
    }

    // An output model on a query without rows is an error whatever its types, so it is reported beside a type error.
    [Fact]
    public void Run_OutputModelOnNoRowsWithATypeError_IsSqlsrc259Too()
    {
        var file = new SqlFile(
            UsersPath,
            "-- name: DeleteUser\n-- output-model: UserRow\nDELETE FROM users WHERE id = @id;\n"
        );
        var regclass = TestSidecars.Postgres(name: "regclass", internalName: "regclass");

        var run = Run(file, Entry(file, "DeleteUser", [TestSidecars.Parameter(0, "id", regclass)], null));

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics[0].ShouldStartWith("SQLSRC246 /app/Repo/Users.sql(1,10)-(1,20): ");
        run.Diagnostics[1]
            .ShouldBe(
                "SQLSRC259 /app/Repo/Users.sql(2,1)-(2,25): The query 'DeleteUser' names an output model, and its "
                    + "statement returns no rows.  Remove the -- output-model: marker."
            );
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // The second query of the file, with its summary, its two markers and the table its entry matches.
    [Fact]
    public void Run_Query_KeepsWhatItsModelsAreWrittenFrom()
    {
        var file = new SqlFile(
            UsersPath,
            "-- name: GetFirst\nSELECT 1;\n\n-- name: GetUser\n-- summary: Gets a user.\n-- input-model: UserKey\n"
                + "-- output-model: UserRow\nSELECT * FROM users WHERE id = @id;\n"
        );
        var entry = Entry(file, "GetUser", [Parameter("id")], [Column("id")]) with
        {
            MatchesTable = new SidecarTable("public", "users"),
        };

        var run = Run(file, Entry(file, "GetFirst", [], [Column("one")]), entry);

        run.Diagnostics.ShouldBeEmpty();
        var models = ModelsOf(run);
        models.NormalizedPath.ShouldBe("app/Repo/Users.sql");
        models.FileName.ShouldBe("Users.sql");
        models.Queries.Count.ShouldBe(2);
        var query = models.Queries[1];
        query.Name.ShouldBe("GetUser");
        query.Index.ShouldBe(1);
        query.NameLocation.Path.ShouldBe(UsersPath);
        query.NameLocation.LineSpan.Start.Line.ShouldBe(3);
        query.NameLocation.LineSpan.Start.Character.ShouldBe(9);
        query.Summary.ShouldBe("Gets a user.");
        query.InputMarker.ShouldNotBeNull().Name.ShouldBe("UserKey");
        query.OutputMarker.ShouldNotBeNull().Name.ShouldBe("UserRow");
        query.Table.ShouldBe(new SidecarTable("public", "users"));
        models.Queries[0].Index.ShouldBe(0);
        models.Queries[0].Table.ShouldBeNull();
    }

    // Every run compiles, with no warning in the generated code, whatever its diagnostics.
    private static GeneratorRun Run(SqlFile file, params SidecarEntry[] entries)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, entries)],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        return run;
    }

    // An entry of the query for its current hash: with rows when it has columns, and without when they are null.
    private static SidecarEntry Entry(
        SqlFile file,
        string name,
        SidecarParameter[] parameters,
        SidecarColumn[]? columns
    ) =>
        TestSidecars.Entry(
            name,
            Sidecars.HashOf(file, name, SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            columns is null ? SidecarResultKind.None : SidecarResultKind.Rows,
            parameters: [.. parameters.Select((parameter, ordinal) => parameter with { Ordinal = ordinal })],
            columns: columns?.Select((column, ordinal) => column with { Ordinal = ordinal }).ToArray()
        );

    private static SidecarParameter Parameter(string name, bool? nullable = null) =>
        TestSidecars.Parameter(0, name, Int4, nullable);

    private static SidecarColumn Column(
        string name,
        PostgresType? type = null,
        bool? nullable = false,
        SidecarOrigin? origin = null,
        bool? identity = null,
        bool? computed = null
    ) => TestSidecars.Column(0, name, type ?? Int4, nullable, origin: origin, identity: identity, computed: computed);

    private static FileModels ModelsOf(GeneratorRun run) =>
        StepOutputs.Single<FileModels>(run, TrackingNames.FileModels);

    private static QueryModels QueryOf(GeneratorRun run) => ModelsOf(run).Queries.ShouldHaveSingleItem();

    private static string[] NamesOf(EquatableArray<ModelProperty>? properties) =>
        [.. properties.ShouldNotBeNull().Select(property => property.Name)];
}
