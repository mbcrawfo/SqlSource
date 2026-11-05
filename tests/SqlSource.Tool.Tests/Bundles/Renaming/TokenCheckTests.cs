using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

// Every token of a renamed bundle is a replacement, a word of the engine's, a number or punctuation.
public sealed class TokenCheckTests
{
    private static (FakeEngineRenaming Engine, NameMapping Mapping) Mapping()
    {
        var engine = new FakeEngineRenaming();
        return (engine, new NameMapping(RenamingBundles.Key(1), engine.Fold));
    }

    // A call of describe whose request is the one given, renamed as it is.
    private static RenamingCall Describe(JsonObject request) =>
        new(0, "describe", [], new JsonObject { ["request"] = request, ["answer"] = new JsonObject() });

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
}
