using System.Linq;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

public sealed class PostgresPlanRenamerTests
{
    private const string PlanPath = "answer.plan";
    private const string Place = "capture.json call 3 'explain' " + PlanPath;

    [Fact]
    public void KeysRead_AreEveryKeyOfPlanKeys() =>
        PostgresPlanRenamer.KeysRead.ShouldBe(PlanKeys.All, ignoreOrder: true);

    [Fact]
    public void Rename_DropsEveryKeyNotRead()
    {
        var plan = new JsonArray(
            new JsonObject
            {
                ["Plan"] = new JsonObject
                {
                    ["Node Type"] = "Index Scan",
                    ["Total Cost"] = 1.5,
                    ["Index Cond"] = "(secret_column = 1)",
                    ["Sort Key"] = new JsonArray("secret_sort"),
                    ["Future Key"] = "secret_name",
                    ["Output"] = new JsonArray("x.id"),
                },
                ["Planning Time"] = 0.1,
                ["Triggers"] = new JsonArray(new JsonObject { ["Trigger Name"] = "secret_trigger" }),
            }
        ).ToJsonString();
        var scope = Scope();

        var renamed = PostgresPlanRenamer.Rename(FieldOf(plan), scope);

        scope.Refusals.ShouldBeEmpty();
        var root = JsonNode.Parse(renamed)!.AsArray();
        root.Single()!.AsObject().Select(static member => member.Key).ShouldBe(["Plan"]);
        Root(renamed).AsObject().Select(static member => member.Key).ShouldBe(["Node Type", "Output"]);
        renamed.ShouldNotContain("secret");
    }

    [Fact]
    public void Rename_NameKeys_AreOneRawName()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Relation Name"] = "Order Items",
            ["Schema"] = "app",
            ["Alias"] = "*SELECT* 1",
            ["CTE Name"] = "recent",
        };

        var root = Root(Rename(node));

        Text(root["Relation Name"]).ShouldBe("t_Order Items");
        Text(root["Schema"]).ShouldBe("s_app");
        Text(root["Alias"]).ShouldBe("*SELECT* 1");
        Text(root["CTE Name"]).ShouldBe("a_recent");
    }

    [Fact]
    public void Rename_OutputQuotedAndBare_ShareOneName()
    {
        var scope = Scope();
        _ = scope.Name("user_id", quoted: true, NameClass.Column);
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Relation Name"] = "Order",
            ["Alias"] = "Order",
            ["Output"] = new JsonArray("\"Order\".user_id", "\"Order\".\"user_id\""),
        };

        var root = Root(Rename(node, scope));

        Text(root["Relation Name"]).ShouldBe("t_Order");
        Text(root["Alias"]).ShouldBe("t_Order");
        Texts(root["Output"]).ShouldBe(["\"t_Order\".c_user_id", "\"t_Order\".\"c_user_id\""]);
    }

    [Fact]
    public void Rename_Literal_IsTheMappingsLiteral()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Result",
            ["Output"] = new JsonArray("('Active'::text)", "'it''s'::text"),
        };

        Texts(Root(Rename(node))["Output"]).ShouldBe(["('l_Active'::text)", "'l_it''s'::text"]);
    }

    [Fact]
    public void Rename_FunctionCall_IsAFunctionsName()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Aggregate",
            ["Output"] = new JsonArray("my_total(x.amount)", "count(*)", "\"My Fn\"(x.amount)"),
        };

        Texts(Root(Rename(node))["Output"])
            .ShouldBe(["f_my_total(a_x.a_amount)", "count(*)", "\"f_My Fn\"(a_x.a_amount)"]);
    }

    [Theory]
    [InlineData("recent")]
    [InlineData("Recent")]
    [InlineData("recent orders")]
    public void Rename_CteSubplanName_KeepsCte(string cte)
    {
        var node = new JsonObject
        {
            ["Node Type"] = "CTE Scan",
            ["CTE Name"] = cte,
            ["Plans"] = new JsonArray(
                new JsonObject
                {
                    ["Node Type"] = "Seq Scan",
                    ["Parent Relationship"] = "InitPlan",
                    ["Subplan Name"] = "CTE " + cte,
                }
            ),
        };

        var root = Root(Rename(node));

        Text(root["Plans"]![0]!["Subplan Name"]).ShouldBe("CTE " + Text(root["CTE Name"]));
        Text(root["CTE Name"]).ShouldNotBe(cte);
    }

    [Fact]
    public void Rename_SubPlanAndInitPlanNames_AreKept()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Result",
            ["Output"] = new JsonArray("(InitPlan 1).col1"),
            ["Filter"] = "(hashed SubPlan 2)",
            ["Plans"] = new JsonArray(
                new JsonObject
                {
                    ["Node Type"] = "Result",
                    ["Parent Relationship"] = "SubPlan",
                    ["Subplan Name"] = "SubPlan 1",
                },
                new JsonObject
                {
                    ["Node Type"] = "Result",
                    ["Parent Relationship"] = "InitPlan",
                    ["Subplan Name"] = "InitPlan 1 (returns $1)",
                }
            ),
        };
        var scope = Scope();

        var root = Root(Rename(node, scope));

        scope.Refusals.ShouldBeEmpty();
        Texts(root["Output"]).ShouldBe(["(InitPlan 1).col1"]);
        Text(root["Filter"]).ShouldBe("(hashed SubPlan 2)");
        Text(root["Plans"]![0]!["Subplan Name"]).ShouldBe("SubPlan 1");
        Text(root["Plans"]![1]!["Subplan Name"]).ShouldBe("InitPlan 1 (returns $1)");
    }

    [Fact]
    public void Rename_ChildAlias_IsRenamedThroughItsAlias()
    {
        var scope = Scope();
        _ = scope.Name("orders", quoted: true, NameClass.Table);
        _ = scope.Name("id", quoted: true, NameClass.Column);
        var plan = PostgresPlans.Text(
            PostgresPlans.Append(PostgresPlans.Member("orders_1", "orders_p1", "orders_1.id"))
        );

        var member = Root(PostgresPlanRenamer.Rename(FieldOf(plan), scope))["Plans"]![0]!;

        scope.Refusals.ShouldBeEmpty();
        Text(member["Alias"]).ShouldBe("t_orders_1");
        Text(member["Relation Name"]).ShouldBe("t_orders_p1");
        Text(member["Schema"]).ShouldBe("public");
        Texts(member["Output"]).ShouldBe(["t_orders_1.c_id"]);
    }

    [Fact]
    public void Rename_ChildAliasHeldBySql_IsItsOwnName()
    {
        var scope = Scope();
        _ = scope.Name("orders", quoted: true, NameClass.Table);
        _ = scope.Name("orders_1", quoted: false, NameClass.Alias);
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Alias"] = "orders_1",
            ["Output"] = new JsonArray("orders_1.id"),
        };

        var root = Root(Rename(node, scope));

        Text(root["Alias"]).ShouldBe("a_orders_1");
        Texts(root["Output"]).ShouldBe(["a_orders_1.a_id"]);
    }

    [Fact]
    public void Rename_ChildOfNoName_IsItsOwnName()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Alias"] = "stray_2",
            ["Output"] = new JsonArray("stray_2.id"),
        };

        var root = Root(Rename(node));

        Text(root["Alias"]).ShouldBe("a_stray_2");
        Texts(root["Output"]).ShouldBe(["a_stray_2.a_id"]);
    }

    // Only _<digits> is the planner's suffix: users_archive, and users_note that no alias names, are names of their
    // own though users is mapped, so that no part of them is kept.
    [Fact]
    public void Rename_ChildWithALetterSuffix_IsItsOwnName()
    {
        var scope = Scope();
        _ = scope.Name("users", quoted: true, NameClass.Table);
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Alias"] = "users_archive",
            ["Output"] = new JsonArray("users_archive.id"),
            ["Filter"] = "(users_note.id > 0)",
        };

        var root = Root(Rename(node, scope));

        scope.Refusals.ShouldBeEmpty();
        Text(root["Alias"]).ShouldBe("a_users_archive");
        Texts(root["Output"]).ShouldBe(["a_users_archive.a_id"]);
        Text(root["Filter"]).ShouldBe("(a_users_note.a_id > 0)");
    }

    // A table named as an engine word keeps its word, and its partitions' alias with it: the token check refuses
    // that alias (WordsOf_ChildOfAnEngineWord_IsWhole).
    [Fact]
    public void Rename_ChildOfAnEngineWord_KeepsTheWord()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Relation Name"] = "name",
            ["Alias"] = "name_1",
            ["Output"] = new JsonArray("name_1.id"),
        };

        var root = Root(Rename(node));

        Text(root["Relation Name"]).ShouldBe("name");
        Text(root["Alias"]).ShouldBe("name_1");
        Texts(root["Output"]).ShouldBe(["name_1.a_id"]);
    }

    [Fact]
    public void Rename_GroupingSets_KeepsItsPresence()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Aggregate",
            ["Grouping Sets"] = new JsonArray(
                new JsonObject { ["Hash Key"] = new JsonArray("secret_a") },
                new JsonObject { ["Group Keys"] = new JsonArray(new JsonArray("secret_b")) }
            ),
            ["Group Key"] = new JsonArray("x.a"),
        };

        var renamed = Rename(node);
        var root = Root(renamed);

        root["Grouping Sets"]!.ToJsonString().ShouldBe("[{},{}]");
        Texts(root["Group Key"]).ShouldBe(["a_x.a_a"]);
        renamed.ShouldNotContain("secret");
    }

    [Fact]
    public void Rename_CountAndFixedWords_AreKept()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Hash Join",
            ["Join Type"] = "Left",
            ["Parent Relationship"] = "Outer",
            ["Strategy"] = "Hashed",
            ["Command"] = "Union",
            ["Subplans Removed"] = 2,
        };

        var root = Root(Rename(node));

        JsonNode.DeepEquals(root, node).ShouldBeTrue();
    }

    [Fact]
    public void Rename_WritesIndentedWithLineFeeds()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Relation Name"] = "Café",
            ["Output"] = new JsonArray("1"),
        };

        Rename(node)
            .ShouldBe(
                "[\n  {\n    \"Plan\": {\n      \"Node Type\": \"Seq Scan\",\n      \"Relation Name\": \"t_Café\",\n"
                    + "      \"Output\": [\n        \"1\"\n      ]\n    }\n  }\n]"
            );
    }

    [Fact]
    public void Rename_UnreadableOutput_IsRefusedAtThePlan()
    {
        var scope = Scope();

        _ = Rename(new JsonObject { ["Node Type"] = "Result", ["Output"] = new JsonArray("(a.b") }, scope);

        scope.Refusals.ShouldBe([new RenamingRefusal(RefusalStep.PlanString, Place, "(a.b")]);
    }

    [Theory]
    [InlineData("(x.a + 1e5)", "1e5")]
    [InlineData("x.a ~ 2x", "2x")]
    public void Rename_OtherTokenWithALetter_IsRefused(string output, string token)
    {
        var scope = Scope();

        _ = Rename(new JsonObject { ["Node Type"] = "Result", ["Output"] = new JsonArray(output) }, scope);

        scope.Refusals.ShouldBe([new RenamingRefusal(RefusalStep.PlanString, Place, token)]);
    }

    [Fact]
    public void Rename_OtherTokenOfSymbols_IsKept() =>
        Texts(
                Root(
                    Rename(
                        new JsonObject
                        {
                            ["Node Type"] = "Result",
                            ["Output"] = new JsonArray("((x.a || x.b) ~~ $1)", "(x.c -> 'k'::text)"),
                        }
                    )
                )["Output"]
            )
            .ShouldBe(["((a_x.a_a || a_x.a_b) ~~ $1)", "(a_x.a_c -> 'l_k'::text)"]);

    [Theory]
    [InlineData("not json")]
    [InlineData( /*lang=json,strict*/
        "{\"Plan\": {\"Node Type\": \"Result\"}}"
    )]
    [InlineData("[1]")]
    [InlineData( /*lang=json,strict*/
        "[{\"Plans\": []}]"
    )]
    [InlineData( /*lang=json,strict*/
        "[{\"Plan\": \"secret\"}]"
    )]
    [InlineData( /*lang=json,strict*/
        "[{\"Plan\": {\"Node Type\": \"Result\", \"Plans\": {}}}]"
    )]
    [InlineData( /*lang=json,strict*/
        "[{\"Plan\": {\"Node Type\": \"Result\", \"Plans\": [\"secret\"]}}]"
    )]
    public void Rename_PlanThatIsNoArrayOfPlans_IsRefused(string plan)
    {
        var scope = Scope();

        PostgresPlanRenamer.Rename(FieldOf(plan), scope).ShouldBeEmpty();

        scope.Refusals.ShouldBe([new RenamingRefusal(RefusalStep.PlanString, Place, plan)]);
    }

    [Theory]
    [InlineData( /*lang=json,strict*/
        "{\"Node Type\": \"Result\", \"Subplans Removed\": \"secret\"}",
        "secret"
    )]
    [InlineData( /*lang=json,strict*/
        "{\"Node Type\": \"Result\", \"Alias\": {\"x\": \"secret\"}}", /*lang=json,strict*/
        "{\"x\": \"secret\"}"
    )]
    [InlineData( /*lang=json,strict*/
        "{\"Node Type\": [\"secret\"]}",
        "[\"secret\"]"
    )]
    [InlineData( /*lang=json,strict*/
        "{\"Node Type\": \"Result\", \"Output\": [[\"secret\"]]}",
        "[\"secret\"]"
    )]
    public void Rename_ValueOfAnotherShape_IsRefused(string node, string token)
    {
        var scope = Scope();

        _ = PostgresPlanRenamer.Rename(FieldOf("[{\"Plan\": " + node + "}]"), scope);

        scope.Refusals.ShouldBe([new RenamingRefusal(RefusalStep.PlanString, Place, token)]);
    }

    [Fact]
    public void Register_NamesEveryNameKeyButAChildAlias()
    {
        var scope = Scope();
        var node = new JsonObject
        {
            ["Node Type"] = "Append",
            ["Plans"] = new JsonArray(
                new JsonObject
                {
                    ["Node Type"] = "Seq Scan",
                    ["Parent Relationship"] = "Member",
                    ["Relation Name"] = "Order Items",
                    ["Schema"] = "app",
                    ["Alias"] = "items_1",
                },
                new JsonObject
                {
                    ["Node Type"] = "CTE Scan",
                    ["Parent Relationship"] = "Member",
                    ["CTE Name"] = "recent",
                    ["Alias"] = "r",
                }
            ),
        };

        PostgresPlanRenamer.Register(CallOf(PlanOf(node)), scope);

        scope.Refusals.ShouldBeEmpty();
        scope.Name("Order Items", quoted: true, NameClass.Alias).ShouldBe("t_Order Items");
        scope.Name("app", quoted: true, NameClass.Alias).ShouldBe("s_app");
        scope.IsMapped("recent", quoted: true).ShouldBeTrue();
        scope.IsMapped("r", quoted: true).ShouldBeTrue();
        scope.IsMapped("items_1", quoted: true).ShouldBeFalse();
    }

    [Fact]
    public void Register_ChildWithALetterSuffix_IsRegistered()
    {
        var scope = Scope();
        _ = scope.Name("users", quoted: true, NameClass.Table);
        var node = new JsonObject { ["Node Type"] = "Seq Scan", ["Alias"] = "users_archive" };

        PostgresPlanRenamer.Register(CallOf(PlanOf(node)), scope);

        scope.Refusals.ShouldBeEmpty();
        scope.IsMapped("users_archive", quoted: true).ShouldBeTrue();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData( /*lang=json,strict*/
        "[{\"Plans\": []}]"
    )]
    public void Register_PlanThatDoesNotRead_DoesNothing(string plan)
    {
        var scope = Scope();

        PostgresPlanRenamer.Register(CallOf(plan), scope);

        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Register_PlanThatIsNoString_DoesNothing()
    {
        var scope = Scope();
        var answer = new JsonObject
        {
            ["plan"] = null,
            ["error"] = new JsonObject { ["sqlState"] = "42P01" },
        };

        PostgresPlanRenamer.Register(
            new RenamingCall(3, PostgresCallNames.Explain, [], new JsonObject { ["answer"] = answer }),
            scope
        );

        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void WordsOf_GivesEveryNameWordAndLiteral()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Parent Relationship"] = "InitPlan",
            ["Subplan Name"] = "CTE a_0011aabb",
            ["Relation Name"] = "t_Order \"Items\"",
            ["Schema"] = "public",
            ["Alias"] = "a_12345678",
            ["Subplans Removed"] = 2,
            ["Output"] = new JsonArray("(a_12345678.c_00aa11bb + 1)", "'l_x'::text"),
            ["Filter"] = "(\"c_99887766\" ~~ $1)",
        };

        PostgresPlanRenamer
            .WordsOf(FieldOf(PlanOf(node)))
            .ShouldBe([
                "\"a_0011aabb\"",
                "\"t_Order \"\"Items\"\"\"",
                "\"public\"",
                "\"a_12345678\"",
                "(",
                "a_12345678",
                ".",
                "c_00aa11bb",
                "+",
                "1",
                ")",
                "'l_x'",
                ":",
                ":",
                "text",
                "(",
                "\"c_99887766\"",
                "~",
                "~",
                "$",
                "1",
                ")",
            ]);
    }

    [Fact]
    public void WordsOf_ChildOfAReplacement_IsTheStem()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Alias"] = "t_1234abcd_1",
            ["Output"] = new JsonArray("t_1234abcd_1.c_0011aabb", "\"t_1234abcd_12\".c_0011aabb"),
        };

        PostgresPlanRenamer
            .WordsOf(FieldOf(PlanOf(node)))
            .ShouldBe(["\"t_1234abcd\"", "t_1234abcd", ".", "c_0011aabb", "\"t_1234abcd\"", ".", "c_0011aabb"]);
    }

    [Fact]
    public void WordsOf_ReplacementWithALetterSuffix_IsWhole()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Alias"] = "t_1234abcd_archive",
            ["Output"] = new JsonArray("t_1234abcd_archive.c_0011aabb", "\"t_1234abcd_archive\".c_0011aabb"),
        };

        PostgresPlanRenamer
            .WordsOf(FieldOf(PlanOf(node)))
            .ShouldBe([
                "\"t_1234abcd_archive\"",
                "t_1234abcd_archive",
                ".",
                "c_0011aabb",
                "\"t_1234abcd_archive\"",
                ".",
                "c_0011aabb",
            ]);
    }

    [Fact]
    public void WordsOf_ChildOfAnEngineWord_IsWhole()
    {
        var node = new JsonObject
        {
            ["Node Type"] = "Seq Scan",
            ["Alias"] = "name_1",
            ["Output"] = new JsonArray("name_1.c_0011aabb", "t_1234abc_1.c_0011aabb", "T_1234ABCD_1.c_0011aabb"),
        };

        PostgresPlanRenamer
            .WordsOf(FieldOf(PlanOf(node)))
            .ShouldBe([
                "\"name_1\"",
                "name_1",
                ".",
                "c_0011aabb",
                "t_1234abc_1",
                ".",
                "c_0011aabb",
                "T_1234ABCD_1",
                ".",
                "c_0011aabb",
            ]);
    }

    [Fact]
    public void WordsOf_KeyNotRead_IsGivenWhole() =>
        PostgresPlanRenamer
            .WordsOf(
                FieldOf( /*lang=json,strict*/
                    "[{\"Plan\": {\"Node Type\": \"Result\", \"Future Key\": [\"secret\"]}, \"Extra\": 1}]"
                )
            )
            .ShouldBe(["Future Key", "secret", "Extra"]);

    [Theory]
    [InlineData("not json")]
    [InlineData( /*lang=json,strict*/
        "[{\"Plan\": {\"Output\": [\"(a.b\"]}}]"
    )]
    public void WordsOf_StringThatDoesNotRead_IsGivenWhole(string plan) =>
        PostgresPlanRenamer.WordsOf(FieldOf(plan)).ShouldBe([plan == "not json" ? plan : "(a.b"]);

    private static FakeRenamingScope Scope() => new(PostgresWords.IsEngineWord, PostgresWords.Fold);

    private static RenamingField FieldOf(string plan) =>
        new(3, PostgresCallNames.Explain, PlanPath, plan, new JsonObject { ["plan"] = plan });

    private static RenamingCall CallOf(string plan) =>
        new(
            3,
            PostgresCallNames.Explain,
            [],
            new JsonObject
            {
                ["request"] = new JsonObject(),
                ["answer"] = new JsonObject { ["plan"] = plan },
            }
        );

    private static string PlanOf(JsonObject node) =>
        new JsonArray(new JsonObject { ["Plan"] = node.DeepClone() }).ToJsonString();

    private static string Rename(JsonObject node, FakeRenamingScope? scope = null) =>
        PostgresPlanRenamer.Rename(FieldOf(PlanOf(node)), scope ?? Scope());

    private static JsonNode Root(string renamed) => JsonNode.Parse(renamed)![0]!["Plan"]!;

    private static string Text(JsonNode? node) => node!.GetValue<string>();

    private static string[] Texts(JsonNode? node) =>
        [.. node!.AsArray().Select(static item => item!.GetValue<string>())];
}
