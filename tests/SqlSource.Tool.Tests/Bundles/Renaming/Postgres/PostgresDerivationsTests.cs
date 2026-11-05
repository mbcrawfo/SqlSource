using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

public sealed class PostgresDerivationsTests
{
    private const string Statement = "sqlsource_1";
    private const string PositionPlace = "capture.json call 0 'prepare' answer.error.position";

    private static readonly HashSet<string> Keywords = ["SELECT", "FROM", "WHERE"];

    [Fact]
    public void Derive_Relations_AreInTheirRenamedOrder()
    {
        var call = new RenamingCall(
            0,
            PostgresCallNames.Relations,
            RelationsCall(("app", "alpha"), ("audit", "beta")),
            RelationsCall(("s_b", "t_a"), ("s_a", "t_z"))
        );

        PostgresDerivations.Derive(call, new FakeRenamingScope());

        call.Renamed["request"]!
            .ToJsonString()
            .ShouldBe( /*lang=json,strict*/
                """{"relations":[{"schema":"s_a","name":"t_z"},{"schema":"s_b","name":"t_a"}]}"""
            );
    }

    [Fact]
    public void Derive_Partitions_AreInTheirRenamedOrder()
    {
        var call = new RenamingCall(
            0,
            PostgresCallNames.Partitions,
            RelationsCall(("app", "alpha"), ("app", "beta")),
            RelationsCall(("s_app", "t_zeta"), ("s_app", "t_eta"))
        );

        PostgresDerivations.Derive(call, new FakeRenamingScope());

        call.Renamed["request"]!
            .ToJsonString()
            .ShouldBe( /*lang=json,strict*/
                """{"relations":[{"schema":"s_app","name":"t_eta"},{"schema":"s_app","name":"t_zeta"}]}"""
            );
    }

    [Fact]
    public void Derive_PrepareErrorInTheSql_MapsThePosition()
    {
        var sql = "SELECT total FROM orders WHERE id = $1";
        string[] types = ["mytype"];
        var scope = Scope();
        var head = ParameterPlaceholders.HeadLength(Statement, types);

        // At the "d" of "id", after "total" and "orders", each renamed longer.
        var call = PrepareCall(sql, types, head + 33, scope);
        PostgresDerivations.Derive(call, scope);

        var renamed = RenamedRequest(call);
        var position = PositionOf(call);
        position.ShouldBe(ParameterPlaceholders.HeadLength(Statement, renamed.Types) + 36);
        ParameterPlaceholders
            .PrepareText(Statement, renamed.Types, renamed.Sql)[(position - 1)..]
            .ShouldStartWith("a_id");
        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Derive_PrepareErrorInTheTypeList_StaysInTheSameItem()
    {
        string[] types = ["mytype", "othertype"];
        var scope = Scope();

        // At the first character of "othertype", after "PREPARE sqlsource_1 (", "mytype" and ", ": unmapped, it would
        // be at the separator after "y_mytype", in the first item.
        var call = PrepareCall("SELECT $1, $2", types, 30, scope);
        PostgresDerivations.Derive(call, scope);

        var renamed = RenamedRequest(call);
        var position = PositionOf(call);
        position.ShouldBe(32);
        ParameterPlaceholders.TypeAt(Statement, renamed.Types, position).ShouldBe(1);
        ParameterPlaceholders
            .PrepareText(Statement, renamed.Types, renamed.Sql)[(position - 1)..]
            .ShouldStartWith("y_othertype");
        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Derive_PrepareErrorBeforeTheList_IsKept()
    {
        var scope = Scope();
        var call = PrepareCall("SELECT $1", ["mytype"], 3, scope);

        PostgresDerivations.Derive(call, scope);

        PositionOf(call).ShouldBe(3);
        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Derive_PrepareErrorWithoutPosition_IsLeftAlone()
    {
        var scope = Scope();
        var call = PrepareCall("SELECT $1", ["mytype"], null, scope);
        var before = call.Renamed.ToJsonString();

        PostgresDerivations.Derive(call, scope);

        call.Renamed.ToJsonString().ShouldBe(before);
        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Derive_PrepareErrorWhoseSqlIsNotTheRenamedOne_IsRefused()
    {
        string[] types = ["mytype"];
        var scope = Scope();
        var call = PrepareCall(
            "SELECT total FROM orders",
            types,
            ParameterPlaceholders.HeadLength(Statement, types) + 8,
            scope
        );
        call.Renamed["request"]!["sql"] = "SELECT a_other FROM a_orders";

        PostgresDerivations.Derive(call, scope);

        scope.Refusals.ShouldBe([new RenamingRefusal(RefusalStep.Token, PositionPlace, null)]);
    }

    private static FakeRenamingScope Scope() => new((spelling, _) => Keywords.Contains(spelling));

    private static JsonObject RelationsCall(params (string Schema, string Name)[] relations)
    {
        var list = new JsonArray();
        foreach (var (schema, name) in relations)
        {
            list.Add(new JsonObject { ["schema"] = schema, ["name"] = name });
        }

        return new JsonObject
        {
            ["request"] = new JsonObject { ["relations"] = list },
            ["answer"] = new JsonObject { ["rows"] = new JsonArray(), ["error"] = null },
        };
    }

    // A prepare call that failed at the position given, with its request renamed as the renaming of its members does:
    // the SQL's new words aliases, a type's new words types.
    private static RenamingCall PrepareCall(string sql, string[] types, int? position, FakeRenamingScope scope)
    {
        var renamedSql = scope.Sql(sql, NameClass.Alias).Text;
        var renamedTypes = new JsonArray();
        foreach (var type in types)
        {
            renamedTypes.Add(scope.Sql(type, NameClass.Type).Text);
        }

        var original = new JsonObject
        {
            ["request"] = Request(sql, [with([.. types])]),
            ["answer"] = Answer(position, withDetail: true),
        };
        var renamed = new JsonObject
        {
            ["request"] = Request(renamedSql, renamedTypes),
            ["answer"] = Answer(position, withDetail: false),
        };
        return new RenamingCall(0, PostgresCallNames.Prepare, original, renamed);
    }

    private static JsonObject Request(string sql, JsonArray types) =>
        new()
        {
            ["statement"] = Statement,
            ["sql"] = sql,
            ["types"] = types,
        };

    // The renamed answer has no detail and no hint: the renaming dropped them.
    private static JsonObject Answer(int? position, bool withDetail)
    {
        var error = new JsonObject
        {
            ["sqlState"] = "42704",
            ["message"] = string.Empty,
            ["position"] = position,
        };
        if (withDetail)
        {
            error["detail"] = null;
            error["hint"] = null;
        }

        return new JsonObject { ["parameterTypes"] = null, ["error"] = error };
    }

    private static PostgresPrepareRequest RenamedRequest(RenamingCall call)
    {
        var request = call.Renamed["request"]!;
        return new PostgresPrepareRequest(
            request["statement"]!.GetValue<string>(),
            request["sql"]!.GetValue<string>(),
            [.. request["types"]!.AsArray().Select(static type => type!.GetValue<string>())]
        );
    }

    private static int PositionOf(RenamingCall call) => call.Renamed["answer"]!["error"]!["position"]!.GetValue<int>();
}
