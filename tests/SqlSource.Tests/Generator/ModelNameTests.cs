// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation.Models;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The properties of each query's models, their names and nullability, and the errors of their names, read from what
// the step of the models gave.  Each run is at Models under postgres on Npgsql 10, with an entry written for each
// query's current hash.
public sealed class ModelNameTests
{
    // The name of GetUser in Users.
    private const string GetUserName = $"{GeneratorHarness.UsersPath}(1,10)-(1,17)";

    [Fact]
    public void Run_ColumnNames_ArePascalCase()
    {
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(
                GeneratorHarness.UsersFile,
                "GetUser",
                [],
                [Column("created_at"), Column("userId"), Column("ID"), Column("api_v2_url")]
            )
        );

        run.Diagnostics.ShouldBeEmpty();
        NamesOf(QueryOf(run).Output).ShouldBe(["CreatedAt", "UserId", "ID", "ApiV2Url"]);
    }

    [Fact]
    public void Run_ParameterName_IsItsBareName()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @accountId, @user_id;\n");

        var run = CleanRun(file, Entry(file, "GetUser", [Parameter("accountId"), Parameter("user_id")], null));

        run.Diagnostics.ShouldBeEmpty();
        NamesOf(QueryOf(run).Input).ShouldBe(["AccountId", "UserId"]);
    }

    [Fact]
    public void Run_ColumnNamedQuestionColumn_IsSqlsrc253()
    {
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column("id"), Column("?column?")])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC253 {GetUserName}: The column at position 2 of the query 'GetUser' has no name.  Give it an alias.",
        ]);
        run.SeverityOf("SQLSRC253").ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ColumnNotAnIdentifier_IsSqlsrc254()
    {
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column("api-url")])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC254 {GetUserName}: The column 'api-url' gives the property name 'Api-url', which is not a C# "
                + "identifier.  Give it an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ParameterNotAnIdentifier_IsSqlsrc254WithRename()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @2fa;\n");

        var run = CleanRun(file, Entry(file, "GetUser", [Parameter("2fa")], null));

        run.Diagnostics.ShouldBe([
            $"SQLSRC254 {GetUserName}: The parameter '2fa' gives the property name '2fa', which is not a C# "
                + "identifier.  Rename it.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // SQL Server takes a letter number in a variable's name, and so does C# in a property's.
    [Fact]
    public void Run_ParameterWithALetterNumber_GivesItsProperty()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- dialect: mssql\n-- name: GetUser\nSELECT @x\u2160;\n");

        var run = SqlServerRun(file, SqlServerEntry(file, "x\u2160"));

        run.Diagnostics.ShouldBeEmpty();
        NamesOf(QueryOf(run).Input).ShouldBe(["X\u2160"]);
    }

    // SQL Server takes the zero-width non-joiner in a variable's name.  C# ignores it in a name, so a property cannot
    // hold it.
    [Fact]
    public void Run_ParameterWithAFormatCharacter_IsSqlsrc254WithRename()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- dialect: mssql\n-- name: GetUser\nSELECT @a\u200Cb;\n");

        var run = SqlServerRun(file, SqlServerEntry(file, "a\u200Cb"));

        run.Diagnostics.ShouldBe([
            $"SQLSRC254 {GeneratorHarness.UsersPath}(2,10)-(2,17): The parameter 'a\u200Cb' gives the property name "
                + "'A\u200Cb', which is not a C# identifier.  Rename it.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_TwoColumnsOneName_IsSqlsrc255()
    {
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column("id"), Column("id")])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC255 {GetUserName}: The column 'id' at position 1 and the column 'id' at position 2 give one "
                + "property name, 'Id', ignoring case.  Give one of them an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_UserIdBesideUserId_IsSqlsrc255()
    {
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column("user_id"), Column("userId")])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC255 {GetUserName}: The column 'user_id' at position 1 and the column 'userId' at position 2 give "
                + "one property name, 'UserId', ignoring case.  Give one of them an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // Two spellings of one name, a_b and aB, give one property name, 'AB'.
    [Fact]
    public void Run_TwoParametersOneName_IsSqlsrc255()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @a_b, @aB;\n");

        var run = CleanRun(file, Entry(file, "GetUser", [Parameter("a_b"), Parameter("aB")], null));

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
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column(name)])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC256 {GetUserName}: The column '{name}' gives the property name '{name}', which a record or object "
                + "already has.  Give it an alias.",
        ]);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ReservedParameterName_IsSqlsrc256WithRename()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @to_string;\n");

        var run = CleanRun(file, Entry(file, "GetUser", [Parameter("to_string")], null));

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
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: DeleteUser\n-- output-model: UserRow\nDELETE FROM users;\n"
        );

        var run = CleanRun(file, Entry(file, "DeleteUser", [], null));

        run.Diagnostics.ShouldBe([
            $"SQLSRC259 {GeneratorHarness.UsersPath}(2,1)-(2,25): The query 'DeleteUser' names an output model, and "
                + "its statement returns no rows.  Remove the -- output-model: marker.",
        ]);
        run.SeverityOf("SQLSRC259").ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // Every error of a query is reported, each at its name, its parameters' before its columns'.
    [Fact]
    public void Run_QueryWithSeveralErrors_ReportsEach()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @2fa;\n");

        var run = CleanRun(
            file,
            Entry(file, "GetUser", [Parameter("2fa")], [Column("?column?"), Column("id"), Column("ID")])
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
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\nSELECT 1;\n\n-- name: GetOther\nSELECT 2;\n"
        );

        var run = CleanRun(
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
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(
                GeneratorHarness.UsersFile,
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
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @p, @q, @r;\n");

        var run = CleanRun(
            file,
            Entry(
                file,
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
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @id;\n");
        var numeric = TestSidecars.Postgres(name: "numeric(10,2)", internalName: "numeric");
        var run = CleanRun(
            file,
            Entry(
                file,
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

        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column("id", odd)])
        );

        run.Diagnostics.ShouldBeEmpty();
        QueryOf(run)
            .Output.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Documentation.ShouldBe("The column <c>id</c>, <c>\"a&lt;b&gt;&amp;c\"</c>.");
    }

    [Fact]
    public void Run_Property_HasItsTypeAndSubject()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @user_id;\n");

        var run = CleanRun(file, Entry(file, "GetUser", [Parameter("user_id")], [Column("id")]));

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
        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column("id")])
        );

        run.Diagnostics.ShouldBeEmpty();
        var query = QueryOf(run);
        query.Input.ShouldBeNull();
        _ = query.Output.ShouldNotBeNull().ShouldHaveSingleItem();
    }

    [Fact]
    public void Run_NoRows_HasNoOutputModel()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT @id;\n");

        var run = CleanRun(file, Entry(file, "GetUser", [Parameter("id")], null));

        run.Diagnostics.ShouldBeEmpty();
        var query = QueryOf(run);
        _ = query.Input.ShouldNotBeNull().ShouldHaveSingleItem();
        query.Output.ShouldBeNull();
    }

    // SELECT FROM users returns rows of no column.
    [Fact]
    public void Run_NoColumns_HasAnEmptyOutputModel()
    {
        var run = CleanRun(GeneratorHarness.UsersFile, Entry(GeneratorHarness.UsersFile, "GetUser", [], []));

        run.Diagnostics.ShouldBeEmpty();
        QueryOf(run).Output.ShouldNotBeNull().ShouldBeEmpty();
    }

    // The query's type error is its one error, and its names are not looked at.
    [Fact]
    public void Run_QueryWithATypeError_HasNoModelsAndNoNameDiagnostic()
    {
        var regclass = TestSidecars.Postgres(name: "regclass", internalName: "regclass");

        var run = CleanRun(
            GeneratorHarness.UsersFile,
            Entry(GeneratorHarness.UsersFile, "GetUser", [], [Column("relation", regclass), Column("?column?")])
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith($"SQLSRC246 {GetUserName}: ");
        ModelsOf(run).Queries.ShouldBeEmpty();
        ModelsOf(run).Diagnostics.ShouldBeEmpty();
    }

    // An output model on a query without rows is an error whatever its types, so it is reported beside a type error.
    [Fact]
    public void Run_OutputModelOnNoRowsWithATypeError_IsSqlsrc259Too()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: DeleteUser\n-- output-model: UserRow\nDELETE FROM users WHERE id = @id;\n"
        );
        var regclass = TestSidecars.Postgres(name: "regclass", internalName: "regclass");

        var run = CleanRun(file, Entry(file, "DeleteUser", [TestSidecars.Parameter(0, "id", regclass)], null));

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics[0].ShouldStartWith($"SQLSRC246 {GeneratorHarness.UsersPath}(1,10)-(1,20): ");
        run.Diagnostics[1]
            .ShouldBe(
                $"SQLSRC259 {GeneratorHarness.UsersPath}(2,1)-(2,25): The query 'DeleteUser' names an output model, "
                    + "and its statement returns no rows.  Remove the -- output-model: marker."
            );
        ModelsOf(run).Queries.ShouldBeEmpty();
    }

    // The second query of the file, with its summary, its two markers and the table its entry matches.
    [Fact]
    public void Run_Query_KeepsWhatItsModelsAreWrittenFrom()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFirst\nSELECT 1;\n\n-- name: GetUser\n-- summary: Gets a user.\n-- input-model: UserKey\n"
                + "-- output-model: UserRow\nSELECT * FROM users WHERE id = @id;\n"
        );
        var entry = Entry(file, "GetUser", [Parameter("id")], [Column("id")]) with
        {
            MatchesTable = new SidecarTable("public", "users"),
        };

        var run = CleanRun(file, Entry(file, "GetFirst", [], [Column("one")]), entry);

        run.Diagnostics.ShouldBeEmpty();
        var models = ModelsOf(run);
        models.NormalizedPath.ShouldBe(GeneratorHarness.UsersNormalizedPath);
        models.FileName.ShouldBe("Users.sql");
        models.Queries.Count.ShouldBe(2);
        var query = models.Queries[1];
        query.Name.ShouldBe("GetUser");
        query.Index.ShouldBe(1);
        query.NameLocation.Path.ShouldBe(GeneratorHarness.UsersPath);
        query.NameLocation.LineSpan.Start.Line.ShouldBe(3);
        query.NameLocation.LineSpan.Start.Character.ShouldBe(9);
        query.Summary.ShouldBe("Gets a user.");
        query.InputMarker.ShouldNotBeNull().Name.ShouldBe("UserKey");
        query.OutputMarker.ShouldNotBeNull().Name.ShouldBe("UserRow");
        query.Table.ShouldBe(new SidecarTable("public", "users"));
        models.Queries[0].Index.ShouldBe(0);
        models.Queries[0].Table.ShouldBeNull();
    }

    // C# refuses these contextual keywords as the name of a type (CS9056, CS9029, CS9062 and, from C# 14, CS9306), so
    // no part of a model's name or of the models' namespace can be one.  A marker is SQLSRC111 at its value, and the
    // settings file's value SQLSRC019, after which the models take the type's namespace.
    [Theory]
    [InlineData("input-model", "file")]
    [InlineData("input-model", "required")]
    [InlineData("input-model", "scoped")]
    [InlineData("input-model", "extension")]
    [InlineData("output-model", "file")]
    [InlineData("output-model", "required")]
    [InlineData("output-model", "scoped")]
    [InlineData("output-model", "extension")]
    [InlineData("modelNamespace", "file")]
    [InlineData("modelNamespace", "required")]
    [InlineData("modelNamespace", "scoped")]
    [InlineData("modelNamespace", "extension")]
    public void Run_ModelNameWithAContextualKeywordThatCSharpRefusesAsATypeName_IsAnError(string place, string keyword)
    {
        // A part alone, in the middle and at the end, so that each part is checked.
        var value = place switch
        {
            "input-model" => keyword,
            "output-model" => "App." + keyword + ".UserRow",
            _ => "App." + keyword,
        };
        var marker = place == "modelNamespace" ? string.Empty : "-- " + place + ": " + value + "\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n" + marker + "SELECT id FROM users WHERE id = @id;\n"
        );

        // A file with a marker that is not valid is not described, so it has no entry.
        var run =
            place == "modelNamespace"
                ? Run(
                    file,
                    [Entry(file, "GetUser", [Parameter("id")], [Column("id")])],
                    $$"""{"modelNamespace": "{{value}}"}"""
                )
                : Run(file);

        var diagnostic = run.Problems().ShouldHaveSingleItem();
        if (place == "modelNamespace")
        {
            diagnostic.ShouldStartWith($"SQLSRC019 {GeneratorHarness.ProjectSettingsPath}(");
            diagnostic.ShouldContain($"'{value}' is not a valid value of modelNamespace");
            run.Sources.ShouldContainKey(GetUserDtoFile);
        }
        else
        {
            var start = place.Length + 6;
            diagnostic.ShouldStartWith(
                $"SQLSRC111 {GeneratorHarness.UsersPath}(2,{start})-(2,{start + value.Length}): "
            );
        }
    }

    // A run of an mssql file with its sidecar, on Microsoft.Data.SqlClient 7.
    private static GeneratorRun SqlServerRun(SqlFile file, SidecarEntry entry)
    {
        var run = Run(Source, [file, Sidecars.Of(file, entry)], references: [SqlClientReferences.SqlClient7]);
        _ = run.Problems();
        return run;
    }

    // An entry of GetUser with one parameter of int.
    private static SidecarEntry SqlServerEntry(SqlFile file, string parameter) =>
        MethodRuns.SqlServerEntry(
            file,
            "GetUser",
            [TestSidecars.Parameter(0, parameter, TestSidecars.SqlServer())],
            null
        );

    private static QueryModels QueryOf(GeneratorRun run) => ModelsOf(run).Queries.ShouldHaveSingleItem();

    private static string[] NamesOf(EquatableArray<ModelProperty>? properties) =>
        [.. properties.ShouldNotBeNull().Select(property => property.Name)];
}
