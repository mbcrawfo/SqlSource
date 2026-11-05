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
}
