using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Capture;
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

        PostgresDerivations.Derive(call, [call], new FakeRenamingScope());

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

        PostgresDerivations.Derive(call, [call], new FakeRenamingScope());

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
        PostgresDerivations.Derive(call, [call], scope);

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
        PostgresDerivations.Derive(call, [call], scope);

        var renamed = RenamedRequest(call);
        var position = PositionOf(call);
        position.ShouldBe(32);
        ParameterPlaceholders.TypeAt(Statement, renamed.Types, position).ShouldBe(1);
        ParameterPlaceholders
            .PrepareText(Statement, renamed.Types, renamed.Sql)[(position - 1)..]
            .ShouldStartWith("y_othertype");
        scope.Refusals.ShouldBeEmpty();
    }

    // At the "," and at the " " of the separator after "mytype", which "PREPARE sqlsource_1 (" and "mytype" end at
    // 27: each is clamped to the item's last code point, in the word "mytype", whose position is the start of
    // "y_mytype".
    [Theory]
    [InlineData(28)]
    [InlineData(29)]
    public void Derive_PrepareErrorInTheSeparator_StaysInTheItemBefore(int at)
    {
        string[] types = ["mytype", "othertype"];
        var scope = Scope();

        var call = PrepareCall("SELECT $1, $2", types, at, scope);
        PostgresDerivations.Derive(call, [call], scope);

        var renamed = RenamedRequest(call);
        var position = PositionOf(call);
        scope.Refusals.ShouldBeEmpty();
        ParameterPlaceholders.TypeAt(Statement, renamed.Types, position).ShouldBe(0);
        position.ShouldBe(22);
    }

    // On the "\n", the ")", the "A" of "AS" and the " " before the SQL, which is the head's last code point: each is
    // clamped to the last item's last code point, in the word "othertype", whose position is the start of
    // "y_othertype".
    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    [InlineData(2)]
    [InlineData(0)]
    public void Derive_PrepareErrorAfterTheLastItem_StaysInIt(int beforeTheSql)
    {
        string[] types = ["mytype", "othertype"];
        var scope = Scope();
        var head = ParameterPlaceholders.HeadLength(Statement, types);

        var call = PrepareCall("SELECT $1, $2", types, head - beforeTheSql, scope);
        PostgresDerivations.Derive(call, [call], scope);

        var renamed = RenamedRequest(call);
        var position = PositionOf(call);
        scope.Refusals.ShouldBeEmpty();
        ParameterPlaceholders.TypeAt(Statement, renamed.Types, position).ShouldBe(1);
        position.ShouldBe(32);
    }

    // At "PREPARE sqlsource_1 ("'s first code point, at its third and at its "(", its last: each is before the list,
    // which the renaming does not change.
    [Theory]
    [InlineData(20)]
    [InlineData(18)]
    [InlineData(0)]
    public void Derive_PrepareErrorBeforeTheList_IsKept(int beforeTheList)
    {
        var scope = Scope();
        var at = ParameterPlaceholders.Characters(ParameterPlaceholders.Opening(Statement)) - beforeTheList;
        var call = PrepareCall("SELECT $1", ["mytype"], at, scope);

        PostgresDerivations.Derive(call, [call], scope);

        PositionOf(call).ShouldBe(at);
        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Derive_PrepareErrorWithoutPosition_IsLeftAlone()
    {
        var scope = Scope();
        var call = PrepareCall("SELECT $1", ["mytype"], null, scope);
        var before = call.Renamed.ToJsonString();

        PostgresDerivations.Derive(call, [call], scope);

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

        PostgresDerivations.Derive(call, [call], scope);

        scope.Refusals.ShouldBe([new RenamingRefusal(RefusalStep.Token, PositionPlace, null)]);
    }

    // An enum named with "_" and its array, as captured, and renamed token by token, which loses the relation of the
    // array's name to its element's: the bind name is made again from the renamed rows, the element's name and [].
    private static readonly PostgresTypeRow Feel = PostgresRows.EnumOf(16700, "_feel", "a");

    private static readonly PostgresTypeRow Feels = PostgresRows.ArrayOf(16701, "__feel", Feel);

    private static readonly PostgresTypeRow RenamedFeel = PostgresRows.EnumIn(16700, "s_a", "t_feel", "a");

    private static readonly PostgresTypeRow RenamedFeels = PostgresRows.ArrayOf(16701, "t_feels", RenamedFeel);

    [Fact]
    public void Derive_CheckOfAnArrayBoundByItsOwnName_IsTheBindNameOfTheRenamedRows()
    {
        var calls = BindCalls([Feels.Oid], [Feel, Feels], [RenamedFeel, RenamedFeels], "public.__feel", "s_a.t_feels");

        PostgresDerivations.Derive(calls[^1], calls, new FakeRenamingScope());

        CheckTypeOf(calls[^1]).ShouldBe("s_a.t_feel[]");
    }

    // A name the original rows do not give is not one the pipeline made, so it keeps its renaming token by token.
    [Fact]
    public void Derive_CheckWhoseOriginalNameTheRowsDoNotGive_KeepsItsRenamedName()
    {
        var calls = BindCalls([Feels.Oid], [Feel, Feels], [RenamedFeel, RenamedFeels], "public.other", "s_a.t_other");

        PostgresDerivations.Derive(calls[^1], calls, new FakeRenamingScope());

        CheckTypeOf(calls[^1]).ShouldBe("s_a.t_other");
    }

    [Fact]
    public void Derive_CheckWithNoPrepareBefore_IsLeftAlone()
    {
        var calls = BindCalls([Feels.Oid], [Feel, Feels], [RenamedFeel, RenamedFeels], "public.__feel", "s_a.t_feels");
        RenamingCall[] withoutPrepare = [calls[1], calls[2]];

        PostgresDerivations.Derive(calls[2], withoutPrepare, new FakeRenamingScope());

        CheckTypeOf(calls[2]).ShouldBe("s_a.t_feels");
    }

    // A parameter past the OIDs prepare answered has no type to make its name from.
    [Fact]
    public void Derive_CheckOfMoreParametersThanPrepareAnswered_KeepsTheirRenamedNames()
    {
        var calls = BindCalls([], [Feel, Feels], [RenamedFeel, RenamedFeels], "public.__feel", "s_a.t_feels");

        PostgresDerivations.Derive(calls[^1], calls, new FakeRenamingScope());

        CheckTypeOf(calls[^1]).ShouldBe("s_a.t_feels");
    }

    // A renamed request whose parameters are not the original's, one for one, is not read as the original's.
    [Fact]
    public void Derive_CheckWhoseRenamedParametersAreOthers_IsLeftAlone()
    {
        var calls = BindCalls([Feels.Oid], [Feel, Feels], [RenamedFeel, RenamedFeels], "public.__feel", "s_a.t_feels");
        calls[^1].Renamed["request"]!["parameters"]!
            .AsArray()
            .Add(new JsonObject { ["name"] = "q", ["dataTypeName"] = "s_a.t_feels" });

        PostgresDerivations.Derive(calls[^1], calls, new FakeRenamingScope());

        CheckTypeOf(calls[^1]).ShouldBe("s_a.t_feels");
    }

    // The renamed rows lack the element, which PostgresBindNames throws for: the name stays as renamed.
    [Fact]
    public void Derive_CheckWhoseRenamedRowsLackAType_KeepsItsRenamedName()
    {
        var calls = BindCalls([Feels.Oid], [Feel, Feels], [RenamedFeels], "public.__feel", "s_a.t_feels");

        PostgresDerivations.Derive(calls[^1], calls, new FakeRenamingScope());

        CheckTypeOf(calls[^1]).ShouldBe("s_a.t_feels");
    }

    // A parameter's bindName is the derived check's name at its place, and a type with none is given none.
    [Fact]
    public void DeriveEntry_ParameterWithABindName_TakesTheDerivedChecksName()
    {
        var calls = BindCalls([Feels.Oid], [Feel, Feels], [RenamedFeel, RenamedFeels], "public.__feel", "s_a.t_feels");
        PostgresDerivations.Derive(calls[^1], calls, new FakeRenamingScope());
        var entry = new JsonObject
        {
            ["parameters"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = new JsonObject { ["name"] = "s_a.t_feels", ["bindName"] = "s_a.t_feels" },
                }
            ),
        };
        var unbound = new JsonObject
        {
            ["parameters"] = new JsonArray(new JsonObject { ["type"] = new JsonObject { ["name"] = "s_a.t_feels" } }),
        };

        PostgresDerivations.DeriveEntry(entry, calls);
        PostgresDerivations.DeriveEntry(unbound, calls);

        entry["parameters"]![0]!["type"]!["bindName"]!.GetValue<string>().ShouldBe("s_a.t_feel[]");
        unbound["parameters"]![0]!["type"]!.AsObject().ContainsKey("bindName").ShouldBeFalse();
    }

    // A prepare that answered the parameters' OIDs, a types call that answered the rows and a check of one parameter
    // bound by the name given, each as captured and as renamed.
    private static RenamingCall[] BindCalls(
        long[] oids,
        PostgresTypeRow[] rows,
        PostgresTypeRow[] renamedRows,
        string bindName,
        string renamedBindName
    )
    {
        var prepare = JsonSerializer.SerializeToNode(new PostgresPrepareAnswer(oids, null), CaptureJson.Options);
        return
        [
            new RenamingCall(
                0,
                PostgresCallNames.Prepare,
                new JsonObject { ["answer"] = prepare!.DeepClone() },
                new JsonObject { ["answer"] = prepare }
            ),
            new RenamingCall(1, PostgresCallNames.Types, TypesCall(rows), TypesCall(renamedRows)),
            new RenamingCall(2, PostgresCallNames.Check, CheckCall("p", bindName), CheckCall("p", renamedBindName)),
        ];
    }

    private static JsonObject TypesCall(PostgresTypeRow[] rows) =>
        new() { ["answer"] = JsonSerializer.SerializeToNode(new PostgresTypesAnswer(rows, null), CaptureJson.Options) };

    private static JsonObject CheckCall(string name, string bindName) =>
        new()
        {
            ["request"] = JsonSerializer.SerializeToNode(
                new PostgresCheckRequest("SELECT 1", [new PostgresCheckParameter(name, bindName)]),
                CaptureJson.Options
            ),
        };

    private static string CheckTypeOf(RenamingCall call) =>
        call.Renamed["request"]!["parameters"]![0]!["dataTypeName"]!.GetValue<string>();

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
