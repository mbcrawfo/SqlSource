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
}
