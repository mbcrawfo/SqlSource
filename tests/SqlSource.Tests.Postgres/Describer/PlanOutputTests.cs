using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// What PostgreSQL 14 and 18 print in a plan's Output for each form PlanExpression reads, and for the texts near
// them: a form is trusted only where a server is seen to print it so.  A row that fails on a server is a form the
// recogniser must not read as written.
[Collection(UsesPostgres.Name)]
public sealed class PlanOutputTests(PostgresContainer postgres)
{
    private const string OneTable =
        "SELECT deleted_at IS NULL, greatest(created_at, deleted_at), coalesce(email, 'x') FROM users a ORDER BY id";

    private const string Windows =
        "SELECT id, count(*) FILTER (WHERE status = 'active') AS a, count(DISTINCT name) AS d, count(*) OVER () AS w, "
        + "row_number() OVER (ORDER BY id) AS r, rank() OVER w2, dense_rank() OVER w2, percent_rank() OVER w2, "
        + "cume_dist() OVER w2 FROM users GROUP BY id WINDOW w2 AS (ORDER BY id)";

    private const string Constants =
        "SELECT count(*), coalesce(max(email::text), 'x'), 'lit' AS l, NULL::text AS n, 1, 1.5, -1, -1.5, true, "
        + "5000000000, 1.5::float8 FROM users";

    private const string Tests =
        "SELECT u.deleted_at IS NULL, u.deleted_at IS NOT NULL, u.email IS DISTINCT FROM u.name, "
        + "u.email IS NOT DISTINCT FROM u.name, CASE WHEN u.deleted_at IS NULL THEN 'open' ELSE 'closed' END, "
        + "CASE u.status WHEN 'active' THEN 'A' END, greatest(u.created_at, u.deleted_at), least(1, u.id), "
        + "u.email::text, upper(u.name), 'x'::email_t FROM users u JOIN orders o ON o.user_id = u.id";

    // The root of the plan of the SQL, on a connection of the test's own.
    private async Task<PlanNode> ExplainAsync(string sql, string? connection = null) =>
        PlanNode.Read(await ExplainTextAsync(sql, connection)).ShouldNotBeNull();

    // The plan of the SQL as the server writes it, for a key that PlanNode does not read.
    private async Task<string> ExplainTextAsync(string sql, string? connection = null)
    {
        await using var source = NpgsqlDataSource.Create(connection ?? postgres.ConnectionString);
        await using var command = source.CreateCommand("EXPLAIN (VERBOSE, FORMAT JSON) " + sql);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    // The generic plan of a statement of one parameter, with a NULL for it, as the describer asks for it, and with
    // partition pruning or without.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL and the type are the test's own constants."
    )]
    private async Task<PlanNode> ExplainExecuteAsync(string sql, string type, bool pruning)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        foreach (
            var text in new[]
            {
                "SET LOCAL plan_cache_mode = force_generic_plan",
                $"SET LOCAL enable_partition_pruning = {(pruning ? "on" : "off")}",
                $"PREPARE plan_output ({type}) AS {sql}",
            }
        )
        {
            await using var set = new NpgsqlCommand(text, connection, transaction);
            _ = await set.ExecuteNonQueryAsync(token);
        }

        await using var command = new NpgsqlCommand(
            "EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE plan_output(NULL)",
            connection,
            transaction
        );
        var plan = (string)(await command.ExecuteScalarAsync(token)).ShouldNotBeNull();
        await transaction.RollbackAsync(token);
        await using var deallocate = new NpgsqlCommand("DEALLOCATE plan_output", connection);
        _ = await deallocate.ExecuteNonQueryAsync(token);
        return PlanNode.Read(plan).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(Constants, 0, "count(*)", "count(*)", false)]
    [InlineData(
        Constants,
        1,
        "COALESCE(max((email)::text), 'x'::text)",
        "COALESCE(max((email)::text), 'x'::text)",
        false
    )]
    [InlineData(Constants, 2, "'lit'::text", "'lit'::text", false)]
    [InlineData(Constants, 3, "NULL::text", "NULL::text", true)]
    [InlineData(Constants, 4, "1", "1", false)]
    [InlineData(Constants, 5, "1.5", "1.5", false)]
    [InlineData(Constants, 6, "'-1'::integer", "'-1'::integer", false)]
    [InlineData(Constants, 7, "'-1.5'::numeric", "'-1.5'::numeric", false)]
    [InlineData(Constants, 8, "true", "true", false)]
    [InlineData(Constants, 9, "'5000000000'::bigint", "'5000000000'::bigint", false)]
    [InlineData(Constants, 10, "'1.5'::double precision", "'1.5'::double precision", false)]
    [InlineData(
        Windows,
        1,
        "(count(*) FILTER (WHERE (status = 'active'::user_status)))",
        "(count(*) FILTER (WHERE (status = 'active'::user_status)))",
        false
    )]
    [InlineData(Windows, 2, "(count(DISTINCT name))", "(count(DISTINCT name))", null)]
    [InlineData(Windows, 3, "count(*) OVER (?)", "count(*) OVER w1", false)]
    [InlineData(Windows, 4, "(row_number() OVER (?))", "(row_number() OVER w2)", false)]
    [InlineData(Windows, 5, "(rank() OVER (?))", "(rank() OVER w2)", false)]
    [InlineData(Windows, 6, "(dense_rank() OVER (?))", "(dense_rank() OVER w2)", false)]
    [InlineData(Windows, 7, "(percent_rank() OVER (?))", "(percent_rank() OVER w2)", false)]
    [InlineData(Windows, 8, "(cume_dist() OVER (?))", "(cume_dist() OVER w2)", false)]
    [InlineData(Tests, 0, "(u.deleted_at IS NULL)", "(u.deleted_at IS NULL)", false)]
    [InlineData(Tests, 1, "(u.deleted_at IS NOT NULL)", "(u.deleted_at IS NOT NULL)", false)]
    [InlineData(
        Tests,
        2,
        "((u.email)::text IS DISTINCT FROM (u.name)::text)",
        "((u.email)::text IS DISTINCT FROM (u.name)::text)",
        false
    )]
    [InlineData(
        Tests,
        3,
        "(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))",
        "(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))",
        false
    )]
    [InlineData(
        Tests,
        4,
        "CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END",
        "CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END",
        false
    )]
    [InlineData(
        Tests,
        5,
        "CASE u.status WHEN 'active'::user_status THEN 'A'::text ELSE NULL::text END",
        "CASE u.status WHEN 'active'::user_status THEN 'A'::text ELSE NULL::text END",
        null
    )]
    [InlineData(Tests, 6, "GREATEST(u.created_at, u.deleted_at)", "GREATEST(u.created_at, u.deleted_at)", null)]
    [InlineData(Tests, 7, "LEAST(1, u.id)", "LEAST(1, u.id)", false)]
    [InlineData(Tests, 8, "(u.email)::text", "(u.email)::text", null)]
    [InlineData(Tests, 9, "upper((u.name)::text)", "upper((u.name)::text)", null)]
    [InlineData(Tests, 10, "('x'::character varying(254))::email_t", "('x'::character varying(254))::email_t", null)]
    [InlineData(OneTable, 0, "((deleted_at IS NULL))", "((deleted_at IS NULL))", false)]
    [InlineData(OneTable, 1, "(GREATEST(created_at, deleted_at))", "(GREATEST(created_at, deleted_at))", null)]
    [InlineData(
        OneTable,
        2,
        "(COALESCE((email)::character varying, 'x'::character varying))",
        "(COALESCE((email)::character varying, 'x'::character varying))",
        false
    )]
    public async Task Explain_Form_PrintsWhatTheRecogniserReads(
        string sql,
        int ordinal,
        string printedOn14,
        string printedOn18,
        bool? expected
    )
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");
        var printed = postgres.Major == 14 ? printedOn14 : printedOn18;

        var root = await ExplainAsync(sql);

        var entry = root.Output.ShouldNotBeNull()[ordinal];
        entry.ShouldBe(printed);
        PlanExpression.NullableOf(entry, new ExpressionContext(true, static _ => null)).ShouldBe(expected);
    }

    // The root of a set operation prints its first branch, whatever the operation.
    [Theory]
    [InlineData("SELECT 'a'::text AS k FROM users UNION SELECT NULL FROM orders")]
    [InlineData("SELECT 'a'::text AS k FROM users INTERSECT SELECT note FROM orders")]
    [InlineData("SELECT 'a'::text AS k FROM users EXCEPT SELECT note FROM orders")]
    [InlineData("SELECT 'a'::text AS k UNION ALL SELECT NULL ORDER BY 1")]
    public async Task Explain_SetOperation_PrintsTheFirstBranchAtTheRoot(string sql)
    {
        var root = await ExplainAsync(sql);

        root.Output.ShouldNotBeNull()[0].ShouldBe("('a'::text)");
        Followed(root).ShouldContain(static node => node.NodeType == "Append" || node.NodeType == "SetOp");
    }

    // A branch of a set operation whose output needs a cast is a Subquery Scan aliased *SELECT* <k>, which prints a
    // column for each of the branch's, in the branch's order and under the branch's own names.  The first branch is
    // text and the second varchar, so the second is coerced; its names cross the first's, and its scan prints them in
    // its own order: the names say nothing of the place, and the place is the branch's.
    [Fact]
    public async Task Explain_SetOperationLeafScan_PrintsTheBranchInItsOrder()
    {
        var root = await ExplainAsync(
            "SELECT o.note AS label, o.note AS note FROM orders o "
                + "UNION ALL SELECT u.name AS note, u.email AS label FROM users u"
        );

        root.NodeType.ShouldBe("Append");
        root.Plans.Select(static member => member.ParentRelationship).ShouldBe(["Member", "Member"]);
        var scan = root.Plans[1];
        scan.NodeType.ShouldBe("Subquery Scan");
        scan.Alias.ShouldBe("*SELECT* 2");
        scan.Output.ShouldBe(["\"*SELECT* 2\".note", "\"*SELECT* 2\".label"]);
        var child = scan.Plans.Single(static child => child.ParentRelationship == "Subquery");
        child.Output.ShouldBe(["u.name", "u.email"]);
    }

    // The implicit cast of a branch to the set operation's type is not in the scan: it prints the column as the
    // branch's, int4, under a result of int8.  Only a node above the set operation prints the cast.
    [Fact]
    public async Task Explain_SetOperationLeafScan_HidesItsImplicitCast()
    {
        var root = await ExplainAsync("SELECT id FROM users UNION ALL SELECT id FROM orders");

        root.NodeType.ShouldBe("Append");
        var scan = root.Plans[0];
        scan.NodeType.ShouldBe("Subquery Scan");
        scan.Alias.ShouldBe("*SELECT* 1");
        scan.Output.ShouldBe(["\"*SELECT* 1\".id"]);
        scan.Plans.Single(static child => child.ParentRelationship == "Subquery").Output.ShouldBe(["users.id"]);
        root.Plans[1].Output.ShouldBe(["orders.id"]);
    }

    // INTERSECT and EXCEPT are a SetOp.  On 14 its one input is an Append of the branches, each ending in a flag of
    // its own, 0 for the left input and 1 for the right; on 18 it has two inputs, Outer and Inner, and no flag.
    [Theory]
    [InlineData("INTERSECT", "Intersect")]
    [InlineData("INTERSECT ALL", "Intersect All")]
    [InlineData("EXCEPT", "Except")]
    [InlineData("EXCEPT ALL", "Except All")]
    public async Task Explain_SetOp_HasFlaggedMembersOn14AndTwoInputsOn18(string keyword, string command)
    {
        postgres.SkipUnlessRecordedMajor("The set operations");

        var text = await ExplainTextAsync($"SELECT id FROM users {keyword} SELECT id FROM orders");

        var root = PlanNode.Read(text).ShouldNotBeNull();
        root.NodeType.ShouldBe("SetOp");
        using (var document = JsonDocument.Parse(text))
        {
            document.RootElement[0].GetProperty("Plan").GetProperty("Command").GetString().ShouldBe(command);
        }

        if (postgres.Major == 14)
        {
            var append = root.Plans.Single();
            append.ParentRelationship.ShouldBe("Outer");
            append.NodeType.ShouldBe("Append");
            append.Plans.ShouldAllBe(static member => member.ParentRelationship == "Member");
            append
                .Plans.Select(static member => member.Output.ShouldNotBeNull()[^1])
                .ShouldBe(["0", "1"], ignoreOrder: true);
        }
        else
        {
            root.Plans.Select(static child => child.ParentRelationship).ShouldBe(["Outer", "Inner"]);
            root.Plans.ShouldAllBe(static child => child.Output!.Count == 1);
        }
    }

    // EXCEPT keeps its left input first even where it is the smaller: the member of flag 0 on 14, the Outer child on
    // 18, whichever way the planner would order an INTERSECT.
    [Fact]
    public async Task Explain_Except_KeepsItsLeftInputFirst()
    {
        postgres.SkipUnlessRecordedMajor("The set operations");

        var root = await ExplainAsync(
            "SELECT id FROM users WHERE id < 3 EXCEPT SELECT g FROM generate_series(1, 100000) g"
        );

        root.NodeType.ShouldBe("SetOp");
        var (left, right) =
            postgres.Major == 14
                ? (
                    root.Plans.Single().Plans.Single(static member => member.Output![^1] == "0"),
                    root.Plans.Single().Plans.Single(static member => member.Output![^1] == "1")
                )
                : (
                    root.Plans.Single(static child => child.ParentRelationship == "Outer"),
                    root.Plans.Single(static child => child.ParentRelationship == "Inner")
                );
        All(left).Select(static node => node.RelationName).OfType<string>().ShouldBe(["users"]);
        All(right).Select(static node => node.RelationName).OfType<string>().ShouldBeEmpty();
    }

    // A branch's value counts for the result's type where the two are in one family of built-in types, every ordered
    // pair of which has a cast that the server ships: no user can add another, and none returns NULL for a value that
    // is not.  The families are SetOperationTypes' own.
    [Fact]
    public async Task Catalog_CastFamilies_AreBuiltInBothWays()
    {
        (int Family, uint Type)[] members =
        [
            .. SetOperationTypes.Families.SelectMany(
                static (family, index) => family.Select(oid => (index, (uint)oid))
            ),
        ];
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(
            """
            WITH family (id, type) AS (
                SELECT * FROM unnest($1::int4[], $2::oid[])
            ),
            pair AS (
                SELECT a.type AS source, b.type AS target
                FROM family a JOIN family b ON b.id = a.id AND b.type <> a.type
            )
            SELECT
                count(*) AS pairs,
                count(*) FILTER (
                    WHERE NOT EXISTS (
                        SELECT 1 FROM pg_cast c
                        WHERE c.castsource = p.source AND c.casttarget = p.target AND c.oid < 16384
                    )
                ) AS missing
            FROM pair p
            """
        );
        _ = command.Parameters.Add(new NpgsqlParameter { Value = members.Select(static m => m.Family).ToArray() });
        _ = command.Parameters.Add(
            new NpgsqlParameter { Value = members.Select(static m => m.Type).ToArray(), DataTypeName = "oid[]" }
        );
        var token = TestContext.Current.CancellationToken;
        await using var reader = await command.ExecuteReaderAsync(token);
        (await reader.ReadAsync(token)).ShouldBeTrue();

        // Six types and three, three, make 30, 6 and 6 ordered pairs.
        reader
            .GetInt64(0)
            .ShouldBe(SetOperationTypes.Families.Sum(static family => family.Count * (family.Count - 1)));
        reader.GetInt64(0).ShouldBe(42);
        reader.GetInt64(1).ShouldBe(0);
    }

    // Each OID the describer names is the catalog's type of the constant's name, compared upper-cased.
    [Fact]
    public async Task Catalog_BuiltInTypes_AreNamedSo()
    {
        var expected = typeof(BuiltInTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(long))
            .ToDictionary(
                static field => (uint)(long)field.GetRawConstantValue().ShouldNotBeNull(),
                static field => field.Name.ToUpperInvariant()
            );
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(
            """
            SELECT t.oid, upper(t.typname::text), n.nspname::text
            FROM pg_catalog.pg_type AS t
            JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace
            WHERE t.oid = ANY ($1)
            """
        );
        _ = command.Parameters.Add(new NpgsqlParameter { Value = expected.Keys.ToArray(), DataTypeName = "oid[]" });
        var token = TestContext.Current.CancellationToken;
        var found = new Dictionary<uint, string>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            reader.GetString(2).ShouldBe("pg_catalog");
            found.Add(await reader.GetFieldValueAsync<uint>(0, token), reader.GetString(1));
        }

        expected.Count.ShouldBe(19);
        found.ShouldBe(expected, ignoreOrder: true);
    }

    // Each operator and function TotalFunctions reads is pg_catalog's own, of exactly that name and those argument
    // types, returning that type, and implemented by the C function the row names: an operator by a function of
    // pg_catalog whose prosrc that is, and a plain function returning one value whose own prosrc it is.  Every row
    // that is not so is listed.
    [Fact]
    public async Task Catalog_TotalFunctions_ArePgCatalogsOwn()
    {
        var operators = TotalFunctions.Operators.ToArray();
        var functions = TotalFunctions.Functions.ToArray();
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        var token = TestContext.Current.CancellationToken;
        var mismatches = new List<string>();

        await using (
            var command = source.CreateCommand(
                """
                SELECT r.ordinal
                FROM unnest($1::text[], $2::oid[], $3::oid[], $4::oid[], $5::text[]) WITH ORDINALITY
                    AS r (name, oprleft, oprright, result, implementation, ordinal)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_operator AS o
                    JOIN pg_catalog.pg_proc AS p ON p.oid = o.oprcode
                    WHERE o.oprnamespace = 'pg_catalog'::regnamespace
                        AND o.oprname = r.name
                        AND o.oprleft = r.oprleft
                        AND o.oprright = r.oprright
                        AND o.oprresult = r.result
                        AND p.pronamespace = 'pg_catalog'::regnamespace
                        AND p.prolang = (SELECT l.oid FROM pg_catalog.pg_language AS l WHERE l.lanname = 'internal')
                        AND p.prosrc = r.implementation
                )
                ORDER BY r.ordinal
                """
            )
        )
        {
            _ = command.Parameters.Add(
                new NpgsqlParameter { Value = operators.Select(static row => row.Name).ToArray() }
            );
            _ = command.Parameters.Add(Oids(operators.Select(static row => row.Left)));
            _ = command.Parameters.Add(Oids(operators.Select(static row => row.Right)));
            _ = command.Parameters.Add(Oids(operators.Select(static row => row.Result)));
            _ = command.Parameters.Add(
                new NpgsqlParameter { Value = operators.Select(static row => row.Implementation).ToArray() }
            );
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var (name, left, right, result, implementation) = operators[reader.GetInt64(0) - 1];
                mismatches.Add($"operator {name}({left}, {right}) -> {result} by {implementation}");
            }
        }

        await using (
            var command = source.CreateCommand(
                """
                SELECT r.ordinal
                FROM unnest($1::text[], $2::text[], $3::oid[], $4::text[]) WITH ORDINALITY
                    AS r (name, arguments, result, implementation, ordinal)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_proc AS p
                    WHERE p.pronamespace = 'pg_catalog'::regnamespace
                        AND p.proname = r.name
                        AND p.proargtypes = r.arguments::oidvector
                        AND p.prorettype = r.result
                        AND p.prokind = 'f'
                        AND NOT p.proretset
                        AND p.prolang = (SELECT l.oid FROM pg_catalog.pg_language AS l WHERE l.lanname = 'internal')
                        AND p.prosrc = r.implementation
                )
                ORDER BY r.ordinal
                """
            )
        )
        {
            _ = command.Parameters.Add(
                new NpgsqlParameter { Value = functions.Select(static row => row.Name).ToArray() }
            );
            _ = command.Parameters.Add(
                new NpgsqlParameter
                {
                    Value = functions.Select(static row => string.Join(' ', row.Arguments)).ToArray(),
                }
            );
            _ = command.Parameters.Add(Oids(functions.Select(static row => row.Result)));
            _ = command.Parameters.Add(
                new NpgsqlParameter { Value = functions.Select(static row => row.Implementation).ToArray() }
            );
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var (name, arguments, result, implementation) = functions[reader.GetInt64(0) - 1];
                mismatches.Add($"function {name}({string.Join(", ", arguments)}) -> {result} by {implementation}");
            }
        }

        mismatches.ShouldBeEmpty();
        operators.Length.ShouldBe(178);
        functions.Length.ShouldBe(5);

        static NpgsqlParameter Oids(IEnumerable<long> oids) =>
            new() { Value = oids.Select(static oid => (uint)oid).ToArray(), DataTypeName = "oid[]" };
    }

    // Each aggregate TotalFunctions reads is pg_catalog's own, of exactly that name and those argument types, returning
    // that type, a plain aggregate of one value and no ordered set, whose transition function and final one are the
    // internal functions of pg_catalog the row names, or no final one for "-".  Every row that is not so is listed.
    [Fact]
    public async Task Catalog_TotalAggregates_ArePgCatalogsOwn()
    {
        var aggregates = TotalFunctions.Aggregates.ToArray();
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(
            """
            SELECT r.ordinal
            FROM unnest($1::text[], $2::text[], $3::oid[], $4::text[], $5::text[]) WITH ORDINALITY
                AS r (name, arguments, result, transition, final, ordinal)
            WHERE NOT EXISTS (
                SELECT 1
                FROM pg_catalog.pg_proc AS p
                JOIN pg_catalog.pg_aggregate AS a ON a.aggfnoid = p.oid
                JOIN pg_catalog.pg_proc AS t ON t.oid = a.aggtransfn
                LEFT JOIN pg_catalog.pg_proc AS f ON f.oid = a.aggfinalfn
                WHERE p.pronamespace = 'pg_catalog'::regnamespace
                    AND p.proname = r.name
                    AND p.proargtypes = r.arguments::oidvector
                    AND p.prorettype = r.result
                    AND p.prokind = 'a'
                    AND NOT p.proretset
                    AND a.aggkind = 'n'
                    AND t.pronamespace = 'pg_catalog'::regnamespace
                    AND t.prolang = (SELECT l.oid FROM pg_catalog.pg_language AS l WHERE l.lanname = 'internal')
                    AND t.proname = r.transition
                    AND (
                        (r.final = '-' AND a.aggfinalfn = 0)
                        OR (
                            f.pronamespace = 'pg_catalog'::regnamespace
                            AND f.prolang = (SELECT l.oid FROM pg_catalog.pg_language AS l WHERE l.lanname = 'internal')
                            AND f.proname = r.final
                        )
                    )
            )
            ORDER BY r.ordinal
            """
        );
        _ = command.Parameters.Add(new NpgsqlParameter { Value = aggregates.Select(static row => row.Name).ToArray() });
        _ = command.Parameters.Add(
            new NpgsqlParameter { Value = aggregates.Select(static row => string.Join(' ', row.Arguments)).ToArray() }
        );
        _ = command.Parameters.Add(
            new NpgsqlParameter
            {
                Value = aggregates.Select(static row => (uint)row.Result).ToArray(),
                DataTypeName = "oid[]",
            }
        );
        _ = command.Parameters.Add(
            new NpgsqlParameter { Value = aggregates.Select(static row => row.Transition).ToArray() }
        );
        _ = command.Parameters.Add(
            new NpgsqlParameter { Value = aggregates.Select(static row => row.Final).ToArray() }
        );

        var token = TestContext.Current.CancellationToken;
        var mismatches = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var (name, arguments, result, transition, final) = aggregates[reader.GetInt64(0) - 1];
            mismatches.Add($"aggregate {name}({string.Join(", ", arguments)}) -> {result} by {transition} and {final}");
        }

        mismatches.ShouldBeEmpty();
        aggregates.Length.ShouldBe(38);
    }

    // The types that carry a modifier are every type of the catalog with a typmod input, but the arrays, whose
    // element carries it.
    [Fact]
    public async Task Catalog_TypesWithAModifier_AreTheBuiltInOnesWithATypmodInput()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(
            """
            SELECT t.typname::text
            FROM pg_catalog.pg_type AS t
            WHERE t.typnamespace = 'pg_catalog'::regnamespace AND t.typmodin <> 0 AND t.typcategory <> 'A'
            """
        );
        var token = TestContext.Current.CancellationToken;
        var found = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            found.Add(reader.GetString(0));
        }

        found.ShouldBe(BuiltInTypes.WithModifier, ignoreOrder: true);
    }

    // A function the path finds before the catalog's prints as the catalog's would: the reason a bare name is the
    // catalog's only when the path names pg_catalog first.
    [Fact]
    public async Task Explain_CountShadowedOnTheSearchPath_PrintsUnqualified()
    {
        await postgres.ExecuteAsync(ScratchSchema.ShadowCount);
        var connection = $"{postgres.ConnectionString};Search Path=shadow,pg_catalog,public";

        var root = await ExplainAsync("SELECT count(*) FROM users", connection);

        root.Output.ShouldNotBeNull().ShouldBe(["count(*)"]);
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand("SELECT count(*) IS NULL FROM users");
        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldBe(true);
    }

    // A Subquery Scan prints the subquery's columns in the order the query above needs them, and its child in the
    // subquery's own: the reason no column through a scan is mapped by its place.
    [Fact]
    public async Task Explain_SubqueryScan_PrintsItsColumnsInTheOrderTheyAreUsed()
    {
        var root = await ExplainAsync(
            "SELECT s.total, s.id FROM (SELECT o.id, o.total, row_number() OVER () AS n FROM orders o) s "
                + "WHERE s.n > 1"
        );

        var scan = Followed(root).Single(static node => node.NodeType == "Subquery Scan");
        scan.Output.ShouldBe(["s.total", "s.id"]);
        var child = scan.Plans.Single(static child => child.ParentRelationship == "Subquery");
        child.Output.ShouldNotBeNull().Take(2).ShouldBe(["o.id", "o.total"]);
    }

    // Each scan of a CTE names it, and the CTE's plan is one InitPlan named for it, wherever it hangs.
    [Fact]
    public async Task Explain_CteScan_NamesTheInitPlanOfItsCte()
    {
        var root = await ExplainAsync(
            "WITH recent AS (SELECT o.id, o.user_id FROM orders o) "
                + "SELECT r.id, n.id FROM recent r LEFT JOIN recent n ON n.user_id = r.user_id"
        );

        Followed(root)
            .Where(static node => node.NodeType == "CTE Scan")
            .Select(static node => (node.Alias, node.CteName))
            .ShouldBe([("r", "recent"), ("n", "recent")], ignoreOrder: true);
        var plans = All(root).Where(static node => node.ParentRelationship == "InitPlan").ToArray();
        plans.Single().SubplanName.ShouldBe("CTE recent");
        plans.Single().Output.ShouldBe(["o.id", "o.user_id"]);
    }

    // The NULL of a parameter prunes every partition when the executor starts, and EXPLAIN shows the Append with no
    // member.  Without partition pruning every member shows, each under the table's alias and a number, with the same
    // Output but for the alias, and the nodes above print the table's alias.
    [Fact]
    public async Task Explain_PartitionedTableWithoutPruning_PrintsEveryMember()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);
        const string Sql = "SELECT p.id, p.kind FROM scratch_parts p WHERE p.kind = $1 ORDER BY p.id";

        var pruned = await ExplainExecuteAsync(Sql, "integer", pruning: true);
        var unpruned = await ExplainExecuteAsync(Sql, "integer", pruning: false);

        Followed(pruned).Single(static node => node.NodeType == "Append").Plans.ShouldBeEmpty();
        unpruned.Output.ShouldBe(["p.id", "p.kind"]);
        var append = Followed(unpruned).Single(static node => node.NodeType == "Append");
        append.Output.ShouldBeNull();
        append
            .Plans.Select(static member => (member.ParentRelationship, member.Alias, member.RelationName))
            .ShouldBe([("Member", "p_1", "scratch_parts_1"), ("Member", "p_2", "scratch_parts_2")]);
        append.Plans[0].Output.ShouldBe(["p_1.id", "p_1.kind"]);
        append.Plans[1].Output.ShouldBe(["p_2.id", "p_2.kind"]);
    }

    // A value computed under a join prints in one pair of parentheses at every node above the node that computes
    // it, and as itself there; a value the join computes prints as itself at the join.
    [Fact]
    public async Task Explain_ValueComputedBelow_IsParenthesisedAboveIt()
    {
        var root = await ExplainAsync(
            "SELECT u.id, s.k, coalesce(s.note, 'z') AS z FROM users u LEFT JOIN "
                + "(SELECT o.user_id, 'x'::text AS k, o.note FROM orders o) s ON s.user_id = u.id "
                + "ORDER BY u.name LIMIT 5"
        );

        var path = Followed(root);
        var join = path.Single(static node => node.JoinType == "Right");
        root.Output.ShouldNotBeNull().Take(3).ShouldBe(["u.id", "('x'::text)", "(COALESCE(o.note, 'z'::text))"]);
        join.Output.ShouldNotBeNull().Take(3).ShouldBe(["u.id", "('x'::text)", "COALESCE(o.note, 'z'::text)"]);
        var scan = path.Single(static node => node.Alias == "o");
        scan.Output.ShouldNotBeNull().ShouldContain("'x'::text");
    }

    // An operator, a function and a cast print with the implicit casts of their arguments, and bare where the session
    // would resolve the same one from its name and those argument types.  The text is the same on 14 and on 18.
    [Theory]
    [InlineData("u.balance * 2", "(balance * '2'::numeric)")]
    [InlineData("u.id::bigint * 2", "((id)::bigint * 2)")]
    [InlineData("lower(u.name) || '!'", "(lower((name)::text) || '!'::text)")]
    [InlineData("length(u.name)", "length((name)::text)")]
    [InlineData("date_trunc('day', u.created_at)", "date_trunc('day'::text, created_at)")]
    [InlineData("u.created_at::date", "(created_at)::date")]
    [InlineData("u.name = 'x'", "((name)::text = 'x'::text)")]
    public async Task Explain_TotalForm_PrintsWhatTheReaderReads(string expression, string expected)
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");

        var root = await ExplainAsync($"SELECT {expression} FROM users u");

        root.Output.ShouldBe([expected]);
    }

    // An aggregate under GROUP BY is computed at the Aggregate that has a Group Key, which prints it bare; the node
    // above prints it in parentheses.  The Aggregate has no Grouping Sets.
    [Fact]
    public async Task Explain_GroupedAggregate_IsComputedAtANodeWithAGroupKey()
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");

        var text = await ExplainTextAsync(
            "SELECT u.id, max(u.name), sum(u.balance) FROM users u JOIN orders o ON o.user_id = u.id "
                + "GROUP BY u.id ORDER BY 3"
        );

        var root = PlanNode.Read(text).ShouldNotBeNull();
        root.NodeType.ShouldBe("Sort");
        root.Output.ShouldBe(["u.id", "(max((u.name)::text))", "(sum(u.balance))"]);
        var aggregate = root.Plans.Single();
        aggregate.NodeType.ShouldBe("Aggregate");
        aggregate.HasGroupKey.ShouldBeTrue();
        aggregate.HasGroupingSets.ShouldBeFalse();
        aggregate.HasFilter.ShouldBeFalse();
        aggregate.Strategy.ShouldBeOneOf("Sorted", "Hashed");
        aggregate.Output.ShouldBe(["u.id", "max((u.name)::text)", "sum(u.balance)"]);

        // The key as the server prints it, which PlanNode reads only as present.
        using var document = JsonDocument.Parse(text);
        document
            .RootElement[0]
            .GetProperty("Plan")
            .GetProperty("Plans")[0]
            .GetProperty("Group Key")
            .EnumerateArray()
            .Select(static key => key.GetString())
            .ShouldBe(["u.id"]);
    }

    // A scalar subquery is a SubPlan whose plan is an Aggregate of the Plain strategy, with no Group Key, and the
    // column prints (SubPlan <n>) at the node that evaluates it.
    [Fact]
    public async Task Explain_ScalarSubquery_IsASubPlanOfAPlainAggregate()
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");

        var root = await ExplainAsync(
            "SELECT u.id, (SELECT count(*) FROM orders o WHERE o.user_id = u.id) AS n FROM users u"
        );

        root.Output.ShouldBe(["u.id", "(SubPlan 1)"]);
        var subPlan = All(root).Single(static node => node.SubplanName == "SubPlan 1");
        subPlan.ParentRelationship.ShouldBe("SubPlan");
        subPlan.NodeType.ShouldBe("Aggregate");
        subPlan.Strategy.ShouldBe("Plain");
        subPlan.HasGroupKey.ShouldBeFalse();
        subPlan.HasFilter.ShouldBeFalse();
        subPlan.Output.ShouldBe(["count(*)"]);
    }

    // A subquery in an expression is a SubPlan of every kind.  14 prints each as (SubPlan <n>), or (hashed SubPlan
    // <n>) where the planner hashes it; 18 prints EXISTS, IN or ANY and ARRAY each its own way, and a scalar
    // subquery as 14 does.  The uncorrelated IN is hashed on both by a rule of size and not of cost: the planner
    // hashes an IN whose subplan takes no parameter of the query above when the subplan's estimated rows times their
    // width fit in the memory a hash may take (subplan_is_hashable, under work_mem and hash_mem_multiplier).  A scan of
    // one integer column of the small orders table fits by far under the server's default work_mem, which the test
    // container keeps, so the row does not move with the costs or the statistics.
    [Theory]
    [InlineData("EXISTS (SELECT 1 FROM orders o WHERE o.user_id > u.id)", "(SubPlan 1)", "EXISTS(SubPlan 1)")]
    [InlineData(
        "u.id IN (SELECT o.user_id FROM orders o WHERE o.placed_at > u.created_at)",
        "(SubPlan 1)",
        "(ANY (u.id = (SubPlan 1).col1))"
    )]
    [InlineData(
        "u.id IN (SELECT o.user_id FROM orders o)",
        "(hashed SubPlan 1)",
        "(ANY (u.id = (hashed SubPlan 1).col1))"
    )]
    [InlineData("ARRAY(SELECT o.id FROM orders o WHERE o.user_id = u.id)", "(SubPlan 1)", "ARRAY(SubPlan 1)")]
    public async Task Explain_Sublinks_PrintEachKindOnEighteen(
        string expression,
        string printedOn14,
        string printedOn18
    )
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");
        var printed = postgres.Major == 14 ? printedOn14 : printedOn18;

        var root = await ExplainAsync($"SELECT {expression} FROM users u");

        root.Output.ShouldBe([printed]);
        All(root).Count(static node => node.ParentRelationship == "SubPlan").ShouldBe(1);
    }

    // A call whose argument types the server resolves from its arguments prints as the reader reads it, with the
    // table's alias left out of a one-table query: the text is the same on 14 and on 18.  The recogniser reads these
    // calls by the name and the printed arguments.
    [Theory]
    [InlineData("count(o.note)", "orders o", "count(note)")]
    [InlineData("count(DISTINCT u.status)", "users u", "count(DISTINCT status)")]
    [InlineData("count(u.email) FILTER (WHERE u.id > 1)", "users u", "count(email) FILTER (WHERE (id > 1))")]
    [InlineData(
        "jsonb_build_object('id', u.id, 'email', u.email)",
        "users u",
        "jsonb_build_object('id', id, 'email', email)"
    )]
    [InlineData("json_build_object()", "users u", "json_build_object()")]
    [InlineData("b.during * tstzrange(now(), now())", "bookings b", "(during * tstzrange(now(), now()))")]
    public async Task Explain_PolymorphicForm_PrintsWhatTheReaderReads(string selectList, string from, string expected)
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");

        var root = await ExplainAsync($"SELECT {selectList} FROM {from}");

        root.Output.ShouldBe([expected]);
    }

    // A window aggregate prints its window as ? on 14 and as the window's name on 18.  A call with a VARIADIC array
    // prints the keyword and the parameter, and an ordered, distinct aggregate prints its order inside the call and is
    // computed at the Aggregate that has the Group Key.
    [Theory]
    [InlineData(
        "SELECT count(u.email) OVER (PARTITION BY u.status) FROM users u",
        null,
        "WindowAgg",
        false,
        "count(email) OVER (?)",
        "count(email) OVER w1"
    )]
    [InlineData(
        "SELECT jsonb_build_object(VARIADIC $1) FROM users u",
        "text[]",
        "Seq Scan",
        false,
        "jsonb_build_object(VARIADIC $1)",
        "jsonb_build_object(VARIADIC $1)"
    )]
    [InlineData(
        "SELECT array_agg(DISTINCT o.id ORDER BY o.id) FROM orders o GROUP BY o.user_id",
        null,
        "Aggregate",
        true,
        "array_agg(DISTINCT id ORDER BY id)",
        "array_agg(DISTINCT id ORDER BY id)"
    )]
    public async Task Explain_PolymorphicFormsOfTheMajor_PrintAsEach(
        string sql,
        string? parameterType,
        string nodeType,
        bool hasGroupKey,
        string printedOn14,
        string printedOn18
    )
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");
        var printed = postgres.Major == 14 ? printedOn14 : printedOn18;

        var root = parameterType is null
            ? await ExplainAsync(sql)
            : await ExplainExecuteAsync(sql, parameterType, pruning: true);

        root.NodeType.ShouldBe(nodeType);
        root.HasGroupKey.ShouldBe(hasGroupKey);
        root.Output.ShouldNotBeNull()[0].ShouldBe(printed);
    }

    // The arguments of COALESCE, CASE and the like are cast to their common type, and print with the casts: a
    // constant as a quoted literal of the type, and a column of another type in a cast.  The Aggregate prints each
    // call bare.
    [Theory]
    [InlineData("sum(coalesce(o.total, 0))", "sum(COALESCE(total, '0'::numeric))")]
    [InlineData("max(coalesce(o.id, o.user_id))", "max(COALESCE(id, (user_id)::bigint))")]
    [InlineData(
        "sum(CASE WHEN o.id > 1 THEN 1 ELSE 2.5 END)",
        "sum(CASE WHEN (id > 1) THEN '1'::numeric ELSE 2.5 END)"
    )]
    public async Task Explain_CommonType_PrintsTheCastsOfTheArguments(string expression, string expected)
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");

        var root = await ExplainAsync($"SELECT {expression} FROM orders o GROUP BY o.user_id");

        root.NodeType.ShouldBe("Aggregate");
        root.HasGroupKey.ShouldBeTrue();
        root.Output.ShouldNotBeNull()[0].ShouldBe(expected);
    }

    // The COALESCE over the two sides of a full join is computed at the join, whichever join the planner picks: the
    // node of Join Type Full prints it as itself, and no node under it does.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explain_FullJoinCoalesce_IsComputedAtTheJoin(bool mergeJoin)
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");
        var connection = mergeJoin
            ? $"{postgres.ConnectionString};Options=-c enable_hashjoin=off -c enable_nestloop=off"
            : null;

        var root = await ExplainAsync(
            "SELECT coalesce(u.id, o.user_id) AS k, o.total FROM users u FULL JOIN orders o ON o.user_id = u.id",
            connection
        );

        var join = All(root).Single(static node => node.JoinType == "Full");
        join.NodeType.ShouldBe(mergeJoin ? "Merge Join" : "Hash Join");
        join.Output.ShouldNotBeNull().ShouldContain("COALESCE(u.id, o.user_id)");
        All(join)
            .Skip(1)
            .SelectMany(static node => node.Output ?? [])
            .ShouldNotContain(static entry => entry.Contains("COALESCE"));
    }

    // A system column of a table prints as a column, qualified by the table's name on an INSERT's RETURNING.
    [Fact]
    public async Task Explain_SystemColumn_PrintsAsAColumn()
    {
        postgres.SkipUnlessRecordedMajor("The printed forms");

        var root = await ExplainAsync(
            "INSERT INTO users (name) VALUES ('x') ON CONFLICT DO NOTHING RETURNING id, (xmax = 0) AS inserted"
        );

        root.NodeType.ShouldBe("ModifyTable");
        root.Output.ShouldBe(["users.id", "(users.xmax = 0)"]);
    }

    // merge_action() is not a function but the grammar's own form, and prints in upper case.  A MERGE has a RETURNING
    // from 17.
    [Fact]
    public async Task Explain_MergeAction_PrintsUpperCase()
    {
        postgres.SkipBelow(17, "MERGE ... RETURNING and merge_action() need PostgreSQL 17");

        var root = await ExplainAsync(
            "MERGE INTO users u USING (SELECT 1 AS id) s ON s.id = u.id WHEN MATCHED THEN DO NOTHING "
                + "RETURNING merge_action(), u.id"
        );

        root.NodeType.ShouldBe("ModifyTable");
        root.Output.ShouldBe(["MERGE_ACTION()", "u.id"]);
    }

    // The node and every node under it through the sides of a join or the one input of another node.
    private static PlanNode[] Followed(PlanNode node) =>
        [
            node,
            .. node.Plans.Where(static child => child.ParentRelationship is "Outer" or "Inner").SelectMany(Followed),
        ];

    // The node and every node under it, whatever the relationship.
    private static PlanNode[] All(PlanNode node) => [node, .. node.Plans.SelectMany(All)];
}
