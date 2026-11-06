using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

// Every token of a renamed bundle is a replacement, a word of the engine's, a number or punctuation.
public sealed class TokenCheckTests
{
    private static (FakeEngineRenaming Engine, NameMapping Mapping) Mapping(SqlDialect dialect = SqlDialect.PostgreSql)
    {
        var engine = new FakeEngineRenaming(dialect);
        return (engine, new NameMapping(RenamingBundles.Key(1), engine.Fold));
    }

    // A call of describe whose request is the one given, renamed as it is.
    private static RenamingCall Describe(JsonObject request) =>
        new(0, "describe", [], new JsonObject { ["request"] = request, ["answer"] = new JsonObject() });

    // A call of open whose server has one setting of the value given, an Own member of the fake's.
    private static RenamingCall Open(string value)
    {
        var setting = new JsonObject { ["name"] = "search_path", ["value"] = value };
        var answer = new JsonObject { ["server"] = new JsonObject { ["settings"] = new JsonArray(setting) } };
        return new(0, "open", [], new JsonObject { ["request"] = new JsonObject(), ["answer"] = answer });
    }

    // The check of query.sql alone, its name a replacement and its body the SQL given.
    private static RenamingRefusal? CheckQuery(string sql, SqlDialect dialect = SqlDialect.PostgreSql)
    {
        var (engine, mapping) = Mapping(dialect);
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        return TokenCheck.Check(engine, mapping, [], $"-- name: {query}\n{sql}", expected: null);
    }

    // The dialect of a theory's row, which names it as a query file does.
    private static SqlDialect DialectOf(string name)
    {
        SqlDialectName.TryParse(name, out var choice).ShouldBeTrue(name);
        return choice.Dialect;
    }

    [Fact]
    public void Check_UnknownWord_IsRefusedNamingIt()
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        var table = mapping.Name("users", quoted: true, NameClass.Table);
        var call = Describe(new JsonObject { ["name"] = query, ["sql"] = $"SELECT secret FROM {table}" });

        var refusal = TokenCheck.Check(engine, mapping, [call], $"-- name: {query}\nSELECT 1", expected: null);

        refusal.ShouldBe(
            new RenamingRefusal(RefusalStep.Token, "capture.json call 0 'describe' request.sql", "secret")
        );
        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT 1 FROM {table} WHERE Status = 1", expected: null)
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", "Status"));
    }

    [Fact]
    public void Check_ReplacementsAndEngineWords_Pass()
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        var table = mapping.Name("users", quoted: true, NameClass.Table);
        var column = mapping.Name("id", quoted: true, NameClass.Column);
        var alias = mapping.Name("Name", quoted: true, NameClass.Alias);
        var literal = mapping.Literal("active");
        var sql =
            $"SELECT {column}, count(*) AS \"{alias}!\", 2.5 AS \"?column?\" /**/ FROM public.{table} --\n"
            + $"WHERE {column} = @{column} AND {column} <> E'{literal}' AND {column} = $q${literal}$q$";
        var call = Describe(
            new JsonObject
            {
                ["name"] = query,
                ["sql"] = sql,
                ["parameters"] = new JsonArray(new JsonObject { ["name"] = column, ["type"] = "text[]" }),
            }
        );

        var refusal = TokenCheck.Check(
            engine,
            mapping,
            [call],
            $"-- name: {query} -> one-optional\n-- param: @{column} text not null\n{sql};\n",
            expected: null
        );

        refusal.ShouldBeNull();
    }

    [Fact]
    public void Check_LiteralThatIsNoReplacement_IsRefused()
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        var column = mapping.Name("status", quoted: true, NameClass.Column);

        var refusal = TokenCheck.Check(
            engine,
            mapping,
            [],
            $"-- name: {query}\nSELECT 1 WHERE {column} = 'active'",
            expected: null
        );

        refusal.ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", "active"));
    }

    // A name of a row the engine's part says is its own is kept by the mapping, and passes though no vocabulary
    // holds it, in the row and wherever the SQL spells it as the engine folds it.
    [Fact]
    public void Check_SpellingKeptFromAnEngineRow_Passes()
    {
        var (engine, mapping) = Mapping();
        var type = new JsonObject
        {
            ["name"] = "regdictionary",
            ["kind"] = "base",
            ["schema"] = "pg_catalog",
            ["internalName"] = "regdictionary",
        };
        var column = new JsonObject
        {
            ["ordinal"] = 0,
            ["name"] = "c_1",
            ["type"] = type,
        };
        var answer = new JsonObject { ["description"] = new JsonObject { ["columns"] = new JsonArray(column) } };
        var call = new RenamingCall(
            0,
            "describe",
            [],
            new JsonObject { ["request"] = new JsonObject(), ["answer"] = answer }
        );
        var run = RenamingRun.For(engine, mapping, "SELECT 1");
        CaptureRenamer.Register(engine, [call], run);
        column["name"] = mapping.Name("total", quoted: true, NameClass.Column);
        var query = mapping.Name("GetTotals", quoted: false, NameClass.Query);
        engine.IsEngineWord("regdictionary", quoted: true).ShouldBeFalse();

        var refusal = TokenCheck.Check(
            engine,
            mapping,
            [call],
            $"-- name: {query}\nSELECT 1::RegDictionary",
            expected: null
        );

        refusal.ShouldBeNull();
        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT 1::\"RegDictionary\"", expected: null)
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", "RegDictionary"));
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("mssql")]
    public void Check_Numbers_Pass(string name)
    {
        var dialect = DialectOf(name);
        CheckQuery("SELECT 1, 2.5, 1e10, .5, 1.5E-3, 1.5e-3, 2E+4, 1_000, 1_000.000_1, 1., 1.e5, 2-1", dialect)
            .ShouldBeNull();
        var (engine, mapping) = Mapping(dialect);
        TokenCheck.Check(engine, mapping, [Open("1.5e-3, 1_000, 2")], "SELECT 1", expected: null).ShouldBeNull();
    }

    // As the SQL renamer refuses them, wherever the check meets them.  The word e5 is no number either.
    [Theory]
    [InlineData("postgres", "SELECT 1users", "1users")]
    [InlineData("postgres", "SELECT 2secret_column", "2secret_column")]
    [InlineData("postgres", "SELECT 1.users", "1.users")]
    [InlineData("postgres", "SELECT 1e5users", "1e5users")]
    [InlineData("postgres", "SELECT 1e", "1e")]
    [InlineData("postgres", "SELECT 1_", "1_")]
    [InlineData("postgres", "SELECT 1__000", "1__000")]
    [InlineData("postgres", "SELECT 0x1F", "0x1F")]
    [InlineData("postgres", "SELECT /*+ 1users */ 1", "1users")]
    [InlineData("postgres", "SELECT e5", "e5")]
    [InlineData("mssql", "SELECT 0x7365637265743132", "0x7365637265743132")]
    public void Check_RunFromADigitThatIsNoNumber_IsRefused(string dialect, string sql, string token) =>
        CheckQuery(sql, DialectOf(dialect)).ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", token));

    [Fact]
    public void Check_OwnWordOrNameFromADigitThatIsNoNumber_IsRefused()
    {
        var (engine, mapping) = Mapping();
        var column = new JsonObject { ["ordinal"] = 0, ["name"] = "1users" };
        var answer = new JsonObject { ["description"] = new JsonObject { ["columns"] = new JsonArray(column) } };
        var describe = new RenamingCall(
            0,
            "describe",
            [],
            new JsonObject { ["request"] = new JsonObject(), ["answer"] = answer }
        );

        TokenCheck
            .Check(engine, mapping, [Open("1users")], "SELECT 1", expected: null)
            .ShouldBe(
                new RenamingRefusal(
                    RefusalStep.Token,
                    "capture.json call 0 'open' answer.server.settings[].value",
                    "1users"
                )
            );
        TokenCheck
            .Check(engine, mapping, [describe], "SELECT 1", expected: null)
            .ShouldBe(
                new RenamingRefusal(
                    RefusalStep.Token,
                    "capture.json call 0 'describe' answer.description.columns[].name",
                    "1users"
                )
            );
    }

    // The shape after a name's arrow and a dialect's name are read as the generator reads them, and are refused when
    // they do not read, whatever the renaming kept.
    [Theory]
    [InlineData("-- name: {0} -> secretshape\nSELECT 1", "secretshape")]
    [InlineData("-- name: {0} ->\nSELECT 1", "")]
    [InlineData("-- name: {0}\n-- dialect: secretdb\nSELECT 1", "secretdb")]
    public void Check_MarkerValueThatDoesNotRead_IsRefused(string file, string token)
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);

        var refusal = TokenCheck.Check(
            engine,
            mapping,
            [],
            string.Format(CultureInfo.InvariantCulture, file, query),
            expected: null
        );

        refusal.ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", token));
    }

    [Fact]
    public void Check_MarkerValuesThatRead_Pass()
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);

        TokenCheck
            .Check(engine, mapping, [], $"-- dialect: postgres\n-- name: {query} -> Many\nSELECT 1", expected: null)
            .ShouldBeNull();
    }

    // A spelling an engine's part keeps through the scope maps to itself, but the check trusts it no more than any
    // other word: it passes only as a word of the engine's.
    [Fact]
    public void Check_SpellingKeptThroughTheScope_IsRefusedUnlessAnEngineWord()
    {
        var (engine, mapping) = Mapping();
        var scope = RenamingRun.For(engine, mapping, "SELECT 1").At("capture.json call 0 'describe'");
        scope.Keep("users", quoted: true);
        scope.Keep("integer", quoted: true);
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);

        scope.Name("users", quoted: true, NameClass.Table).ShouldBe("users");
        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT 1 FROM \"users\"", expected: null)
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", "users"));
        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT 1::\"integer\"", expected: null)
            .ShouldBeNull();
    }

    // Each branch of the check that reads plain SQL or a marker of query.sql refuses a token that is the user's.
    [Theory]
    [InlineData("postgres", "SELECT 'open", "'")]
    [InlineData("postgres", "SELECT 1 FROM \"secret\"", "secret")]
    [InlineData("postgres", "SELECT $q$secret$q$", "secret")]
    [InlineData("postgres", "SELECT 1 WHERE 2 = @secret", "secret")]
    [InlineData("postgres", "SELECT 1 -- secret", "secret")]
    [InlineData("postgres", "SELECT /* secret */ 1", "secret")]
    [InlineData("postgres", "SELECT /*+ secret */ 1", "secret")]
    [InlineData("postgres", "SELECT 1 AS E", "E")]
    [InlineData("postgres", "-- database: secret\nSELECT 1", "secret")]
    [InlineData("postgres", "-- param: @secret\nSELECT 1", "secret")]
    [InlineData("postgres", "-- token: {{secret:1}}\nSELECT 1", "secret")]
    [InlineData("postgres", "-- token-ignore: secret\nSELECT 1", "secret")]
    [InlineData("postgres", "-- summary: Gets the users\nSELECT 1", "-- summary:")]
    [InlineData("postgres", "-- column: total not null\nSELECT 1", "-- column:")]
    [InlineData("mssql", "SELECT [secret]", "secret")]
    [InlineData("mssql", "SELECT 1 FROM #secret", "#secret")]
    [InlineData("mssql", "SELECT 1 ≠ 2", "≠")]
    public void Check_QueryFileTokenOfTheUser_IsRefused(string dialect, string sql, string token) =>
        CheckQuery(sql, DialectOf(dialect)).ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", token));

    [Fact]
    public void Check_NameMarkerThatIsNoReplacement_IsRefused()
    {
        var (engine, mapping) = Mapping();

        TokenCheck
            .Check(engine, mapping, [], "-- name: secret\nSELECT 1", expected: null)
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", "secret"));
    }

    // A one-letter literal prefix passes only right before its quote, or before U&'s ampersand.
    [Fact]
    public void Check_LiteralPrefix_PassesOnlyBeforeItsQuote()
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        var literal = mapping.Literal("active");

        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT E'{literal}', U&'{literal}'", expected: null)
            .ShouldBeNull();
        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT N '{literal}'", expected: null)
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", "N"));
    }

    // SQL Server's # and @ before a name are punctuation, and the name after them is checked.
    [Fact]
    public void Check_NameAfterSqlServersPrefix_PassesAsTheName()
    {
        var (engine, mapping) = Mapping(SqlDialect.SqlServer);
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        var table = mapping.Name("orders", quoted: false, NameClass.Table);

        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT 1 FROM #{table} JOIN ##{table} ON 1 = 1", null)
            .ShouldBeNull();
    }

    // A comment of a call's SQL is checked as query.sql's is.
    [Fact]
    public void Check_CommentOfACallsSql_IsRefused() =>
        Check(Describe(new JsonObject { ["sql"] = "SELECT 1 -- secret" }))
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, "capture.json call 0 'describe' request.sql", "secret"));

    [Fact]
    public void Check_MemberWithNoRule_IsRefused() =>
        Check(Describe(new JsonObject { ["secret"] = "GetUsers" }))
            .ShouldBe(
                new RenamingRefusal(RefusalStep.Token, "capture.json call 0 'describe' request.secret", "GetUsers")
            );

    // A dropped member is in no renamed bundle, so one that is there is refused whatever it holds.
    [Fact]
    public void Check_DroppedMemberStillPresent_IsRefused()
    {
        var server = new JsonObject { ["driver"] = "Npgsql 9.0.3" };
        var call = new RenamingCall(
            0,
            "open",
            [],
            new JsonObject
            {
                ["request"] = new JsonObject(),
                ["answer"] = new JsonObject { ["server"] = server },
            }
        );

        Check(call)
            .ShouldBe(
                new RenamingRefusal(
                    RefusalStep.Token,
                    "capture.json call 0 'open' answer.server.driver",
                    "Npgsql 9.0.3"
                )
            );
    }

    [Fact]
    public void Check_LabelThatIsNoReplacement_IsRefused()
    {
        var (engine, mapping) = Mapping();
        var type = new JsonObject { ["labels"] = new JsonArray(mapping.Literal("active"), "secret") };
        var column = new JsonObject { ["type"] = type };
        var call = new RenamingCall(
            0,
            "describe",
            [],
            new JsonObject
            {
                ["request"] = new JsonObject(),
                ["answer"] = new JsonObject
                {
                    ["description"] = new JsonObject { ["columns"] = new JsonArray(column) },
                },
            }
        );

        TokenCheck
            .Check(engine, mapping, [call], "SELECT 1", expected: null)
            .ShouldBe(
                new RenamingRefusal(
                    RefusalStep.Token,
                    "capture.json call 0 'describe' answer.description.columns[].type.labels[]",
                    "secret"
                )
            );
    }

    // A parameter passes as a replacement, or as a copy of the query's whose parameter is one, after its prefix.
    [Theory]
    [InlineData("secret")]
    [InlineData("@secret")]
    [InlineData("secret_1")]
    public void Check_ParameterThatIsNoReplacement_IsRefused(string name)
    {
        var (engine, mapping) = Mapping();
        var id = mapping.Name("id", quoted: false, NameClass.Parameter);
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        static RenamingCall Parameter(string value) =>
            Describe(new JsonObject { ["parameters"] = new JsonArray(new JsonObject { ["name"] = value }) });
        var file = $"-- name: {query}\nSELECT @{id}";

        TokenCheck.Check(engine, mapping, [Parameter($"@{id}"), Parameter($"{id}_2")], file, null).ShouldBeNull();
        TokenCheck
            .Check(engine, mapping, [Parameter(name)], file, expected: null)
            .ShouldBe(
                new RenamingRefusal(RefusalStep.Token, "capture.json call 0 'describe' request.parameters[].name", name)
            );
    }

    // A copy of a query's parameter passes only through a parameter that passes: the capture is checked before
    // query.sql, so the copy is refused first.
    [Fact]
    public void Check_CopyOfAParameterThatIsNoReplacement_IsRefused()
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        var call = Describe(
            new JsonObject { ["parameters"] = new JsonArray(new JsonObject { ["name"] = "secret_1" }) }
        );

        TokenCheck
            .Check(engine, mapping, [call], $"-- name: {query}\nSELECT @secret", expected: null)
            .ShouldBe(
                new RenamingRefusal(
                    RefusalStep.Token,
                    "capture.json call 0 'describe' request.parameters[].name",
                    "secret_1"
                )
            );
    }

    [Fact]
    public void Check_WholeNameThatIsNoReplacement_IsRefused()
    {
        var engine = new FakeEngineRenaming(
            rules: new Dictionary<string, FieldRule?>
            {
                ["describe request.clrType"] = FieldRule.WholeName(NameClass.Type),
            }
        );
        var mapping = new NameMapping(RenamingBundles.Key(1), engine.Fold);
        var type = mapping.Name("MyApp.Money, MyApp", quoted: true, NameClass.Type);

        TokenCheck
            .Check(engine, mapping, [Describe(new JsonObject { ["clrType"] = type })], "SELECT 1", null)
            .ShouldBeNull();
        TokenCheck
            .Check(engine, mapping, [Describe(new JsonObject { ["clrType"] = "MyApp.Money, MyApp" })], "SELECT 1", null)
            .ShouldBe(
                new RenamingRefusal(
                    RefusalStep.Token,
                    "capture.json call 0 'describe' request.clrType",
                    "MyApp.Money, MyApp"
                )
            );
    }

    // An engine's own string is checked word by word: a literal and a quoted name by their content, and one character
    // of punctuation passes.
    [Theory]
    [InlineData("secret", "secret")]
    [InlineData("'secret'", "secret")]
    [InlineData("\"secret\"", "secret")]
    [InlineData(";;", ";;")]
    public void Check_OwnWordOfTheUser_IsRefused(string word, string token)
    {
        var (engine, mapping) = Mapping();

        TokenCheck
            .Check(engine, mapping, [Open($"public, ; {word}")], "SELECT 1", expected: null)
            .ShouldBe(
                new RenamingRefusal(
                    RefusalStep.Token,
                    "capture.json call 0 'open' answer.server.settings[].value",
                    token
                )
            );
    }

    // The entry is checked after query.sql: its key, a member by the sidecar's table, and a member with no rule.
    [Theory]
    [InlineData("secret", "{}", "expected.json", "secret")]
    [InlineData(
        null, /*lang=json,strict*/
        """{"columns": [{"name": "secret"}]}""",
        "expected.json columns[].name",
        "secret"
    )]
    [InlineData(
        null, /*lang=json,strict*/
        """{"database": "secret"}""",
        "expected.json database",
        "secret"
    )]
    [InlineData(
        null, /*lang=json,strict*/
        """{"secret": "x"}""",
        "expected.json secret",
        "x"
    )]
    public void Check_EntryTokenOfTheUser_IsRefused(string? key, string entry, string place, string token)
    {
        var (engine, mapping) = Mapping();
        var query = mapping.Name("GetUsers", quoted: false, NameClass.Query);
        static string Sidecar(string name, string body) =>
            new JsonObject { ["queries"] = new JsonObject { [name] = JsonNode.Parse(body) } }.ToJsonString();

        TokenCheck
            .Check(
                engine,
                mapping,
                [],
                $"-- name: {query}\nSELECT 1",
                Sidecar(
                    query, /*lang=json,strict*/
                    """{"database": "postgres"}"""
                )
            )
            .ShouldBeNull();
        TokenCheck
            .Check(engine, mapping, [], $"-- name: {query}\nSELECT 1", Sidecar(key ?? query, entry))
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, place, token));
    }

    // The check of the calls given, with a query.sql of SELECT 1 alone.
    private static RenamingRefusal? Check(params RenamingCall[] calls)
    {
        var (engine, mapping) = Mapping();
        return TokenCheck.Check(engine, mapping, calls, "SELECT 1", expected: null);
    }
}
