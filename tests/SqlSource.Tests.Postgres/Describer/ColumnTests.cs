using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;

namespace SqlSource.Tests.Postgres.Describer;

// A query's columns against a real server: what only a server can show, the row description of a statement and
// what Npgsql makes of it.
[Collection(UsesPostgres.Name)]
public sealed class ColumnTests(PostgresContainer postgres)
{
    private Task<QueryDescription> ShouldDescribeAsync(string sql) =>
        Sessions.ShouldDescribeAsync(postgres.ConnectionString, sql);

    private async Task<SidecarColumn[]> ColumnsOfAsync(string sql) =>
        [.. (await ShouldDescribeAsync(sql)).Columns.ShouldNotBeNull()];

    private static string[] TypeNamesOf(IEnumerable<SidecarColumn> columns) =>
        [.. columns.Select(static column => column.Type.Name)];

    [Fact]
    public async Task DescribeAsync_Select_IsRowsWithItsColumnsInOrder()
    {
        var description = await ShouldDescribeAsync("SELECT code, at_local, span, label FROM samples WHERE id = @id");

        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        var columns = description.Columns.ShouldNotBeNull();
        columns.Select(column => column.Ordinal).ShouldBe([0, 1, 2, 3]);
        columns.Select(column => column.Name).ShouldBe(["code", "at_local", "span", "label"]);
        TypeNamesOf(columns).ShouldBe(["character(2)", "timestamp(3) without time zone", "interval", "text"]);
        description.Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");
    }

    // A cast in a select list keeps its modifier in the row description.  Each name is what format_type gives the
    // type, which DescribeAsync_EveryColumnOfATable_IsSpelledAsFormatTypeSpellsIt holds against the server for a
    // column.
    [Theory]
    [InlineData("numeric(18,2)", "numeric(18,2)")]
    [InlineData("numeric(18,0)", "numeric(18,0)")]
    [InlineData("numeric(5)", "numeric(5,0)")]
    [InlineData("numeric", "numeric")]
    [InlineData("varchar(100)", "character varying(100)")]
    [InlineData("varchar", "character varying")]
    [InlineData("char(2)", "character(2)")]
    [InlineData("char", "character(1)")]
    [InlineData("bpchar", "bpchar")]
    [InlineData("bit(3)", "bit(3)")]
    [InlineData("bit", "bit(1)")]
    [InlineData("varbit(5)", "bit varying(5)")]
    [InlineData("varbit", "bit varying")]
    [InlineData("time(4)", "time(4) without time zone")]
    [InlineData("timetz(2)", "time(2) with time zone")]
    [InlineData("timestamp(3)", "timestamp(3) without time zone")]
    [InlineData("timestamptz(0)", "timestamp(0) with time zone")]
    [InlineData("timestamptz", "timestamp with time zone")]
    [InlineData("interval(3)", "interval(3)")]
    [InlineData("interval", "interval")]
    public async Task DescribeAsync_CastWithAModifier_IsSpelledAsFormatTypeSpellsIt(string type, string expected) =>
        (await ColumnsOfAsync($"SELECT NULL::{type} AS c")).ShouldHaveSingleItem().Type.Name.ShouldBe(expected);

    // The fields of an interval and the modifier of an array's element, which only the server's spelling of the
    // modifier holds.  The facets are decoded from the modifier: an array's are its element's.
    [Theory]
    [InlineData("interval year to month", "interval year to month", null, null, null)]
    [InlineData("interval day to second(3)", "interval day to second(3)", null, 3, null)]
    [InlineData("varchar(10)[]", "character varying(10)[]", 10, null, null)]
    [InlineData("numeric(6,2)[]", "numeric(6,2)[]", null, 6, 2)]
    public async Task DescribeAsync_CastWithFieldsOrAnArrayOfAModifier_IsSpelledWithTheModifier(
        string type,
        string expected,
        int? length,
        int? precision,
        int? scale
    )
    {
        var column = (await ColumnsOfAsync($"SELECT NULL::{type} AS c")).ShouldHaveSingleItem();

        var described = column.Type.ShouldBeOfType<PostgresType>();
        described.Name.ShouldBe(expected);
        var faceted = described.Element ?? described;
        (faceted.Length, faceted.Precision, faceted.Scale).ShouldBe((length, precision, scale));
    }

    // An expression has no origin: its modifier is the row description's alone.
    [Fact]
    public async Task DescribeAsync_ExpressionOfAnIntervalWithFields_KeepsTheFields()
    {
        var column = (await ColumnsOfAsync("SELECT '1 day'::interval day to second(3) AS span")).ShouldHaveSingleItem();

        column.Origin.ShouldBeNull();
        var type = column.Type.ShouldBeOfType<PostgresType>();
        type.Name.ShouldBe("interval day to second(3)");
        type.Precision.ShouldBe(3);
    }

    // The fixture schema's tagged is made over varchar's functions in public, as an extension's type would be: its
    // name is the tool's schema.name and the tail of the server's spelling, and it has no facets, which the tool
    // decodes for the types of pg_catalog alone.  An array of it is its element's name and [].
    [Fact]
    public async Task DescribeAsync_ColumnOfATypeWithItsOwnModifier_IsTheSchemaNameAndTheTail()
    {
        var columns = await ColumnsOfAsync("SELECT tag, tags FROM modifiers");

        var tagged = TestSidecars.Postgres("public.tagged(7)", schema: "public", internalName: "tagged");
        columns
            .Select(static column => column.Type)
            .ShouldBe([
                tagged,
                TestSidecars.Postgres(
                    "public.tagged(7)[]",
                    SidecarValues.Kind.Array,
                    "public",
                    "_tagged",
                    element: tagged
                ),
            ]);
    }

    // A domain named numeric in public, first on the search_path of the describe's connection, hides pg_catalog's
    // numeric.  A column of pg_catalog's numeric(10,2) is still numeric(10,2), with the facets of its modifier: the
    // server spells a built-in type with a modifier without its schema, and the tool puts none before it.  A parameter
    // of pg_catalog's numeric binds by numeric, which does not depend on the session: it is returned as a column, so
    // a check that bound the domain, sent as text, would see a column of text.
    [Fact]
    public async Task DescribeAsync_BuiltInTypeHiddenOnTheSearchPath_IsStillTheCatalogsSpellingAndBindsByTheBareName()
    {
        var token = TestContext.Current.CancellationToken;
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var create = own.CreateCommand("CREATE DOMAIN public.\"numeric\" AS text"))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        try
        {
            var description = await Sessions.ShouldDescribeAsync(
                $"{postgres.ConnectionString};Search Path=public,pg_catalog",
                DescribeRequests.Of("SELECT NULL::pg_catalog.numeric(10,2) AS n, @p AS p", ("p", "pg_catalog.numeric"))
            );

            description
                .Columns.ShouldNotBeNull()[0]
                .Type.ShouldBe(
                    TestSidecars.Postgres("numeric(10,2)", internalName: "numeric", precision: 10, scale: 2)
                );
            description.Parameters.ShouldHaveSingleItem().Type.ShouldNotBeNull().BindName.ShouldBe("numeric");
        }
        finally
        {
            await using var drop = own.CreateCommand("DROP DOMAIN IF EXISTS public.\"numeric\"");
            _ = await drop.ExecuteNonQueryAsync(token);
        }
    }

    // A domain of the same name, first on the search_path, makes format_type(oid, -1) write a type of pg_catalog
    // with its schema: pg_catalog.text, pg_catalog."char".  The name is the one it has in any other session, quotes
    // kept, and an array is its element's.  json is qualified so below 17 alone, which special-cases it.  The domains
    // named text, oid and int4 would take the casts of the describer's own SQL, and the column of a table makes it
    // ask tables and spellings too; HiddenTypesCallTests asks every catalog call so.
    [Fact]
    public async Task DescribeAsync_BuiltInTypesHiddenOnTheSearchPath_AreNamedAsInAnySession()
    {
        await postgres.ExecuteAsync(ScratchSchema.HidingTypes);

        var description = await Sessions.ShouldDescribeAsync(
            $"{postgres.ConnectionString};{ScratchSchema.HidingTypesPath}",
            DescribeRequests.Of(
                """
                SELECT NULL::pg_catalog.text AS t, NULL::pg_catalog.jsonb AS j, NULL::pg_catalog.json AS js,
                    NULL::pg_catalog.uuid AS u, NULL::pg_catalog.bpchar AS b, NULL::pg_catalog."char" AS c,
                    NULL::pg_catalog."bit" AS bt, NULL::pg_catalog.text[] AS a, s.code, @p AS p
                FROM public.samples AS s
                """,
                ("p", "pg_catalog.text")
            )
        );

        var columns = description.Columns.ShouldNotBeNull();
        TypeNamesOf(columns)
            .ShouldBe([
                "text",
                "jsonb",
                "json",
                "uuid",
                "bpchar",
                "\"char\"",
                "\"bit\"",
                "text[]",
                "character(2)",
                "text",
            ]);
        columns[7].Type.ShouldBeOfType<PostgresType>().Element.ShouldNotBeNull().Name.ShouldBe("text");
        description
            .Parameters.ShouldHaveSingleItem()
            .Type.ShouldNotBeNull()
            .ShouldSatisfyAllConditions(type => type.Name.ShouldBe("text"), type => type.BindName.ShouldBe("text"));
    }

    [Fact]
    public async Task DescribeAsync_NumericWithANegativeScale_HasIt()
    {
        postgres.SkipBelow(15, "PostgreSQL 14 has no negative scale.");

        var column = (await ColumnsOfAsync("SELECT NULL::numeric(2,-3) AS c")).ShouldHaveSingleItem();

        var type = column.Type.ShouldBeOfType<PostgresType>();
        type.Name.ShouldBe("numeric(2,-3)");
        type.Precision.ShouldBe(2);
        type.Scale.ShouldBe(-3);
    }

    [Fact]
    public async Task DescribeAsync_Literals_AreTextAndNamedByTheServer()
    {
        var columns = await ColumnsOfAsync("SELECT 'x', NULL, 1");

        columns.Select(column => column.Name).ShouldBe(["?column?", "?column?", "?column?"]);
        TypeNamesOf(columns).ShouldBe(["text", "text", "integer"]);
    }

    [Fact]
    public async Task DescribeAsync_TwoColumnsWithOneName_KeepsBoth()
    {
        var columns = await ColumnsOfAsync("SELECT 1 AS id, 'a'::text AS id");

        columns.Select(column => (column.Ordinal, column.Name)).ShouldBe([(0, "id"), (1, "id")]);
        TypeNamesOf(columns).ShouldBe(["integer", "text"]);
    }

    // A suffix is the generator's to read: the describer records the name the server returned.
    [Fact]
    public async Task DescribeAsync_SuffixNames_AreRecordedAsReturned() =>
        (await ColumnsOfAsync("SELECT deleted_at AS \"deleted_at!\", name AS \"name?\" FROM users"))
            .Select(column => column.Name)
            .ShouldBe(["deleted_at!", "name?"]);

    // The statement is described and never run.
    [Fact]
    public async Task DescribeAsync_InsertReturning_IsRowsAndInsertsNothing()
    {
        var description = await ShouldDescribeAsync(
            "INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id, placed_at"
        );

        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.Columns.ShouldNotBeNull().Select(column => column.Name).ShouldBe(["id", "placed_at"]);
        (await postgres.ScalarAsync("SELECT count(*) FROM orders")).ShouldBe(0L);
    }

    [Fact]
    public async Task DescribeAsync_UpdateWithoutReturning_IsNone()
    {
        var description = await ShouldDescribeAsync("UPDATE users SET name = @name WHERE id = @id");

        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
        description.Parameters.Select(parameter => parameter.Name).ShouldBe(["name", "id"]);
    }

    [Fact]
    public async Task DescribeAsync_QueryWithoutATable_AsksColumnsAfterPrepareAndTypesOnce()
    {
        var (described, capture) = await Sessions.DescribeCapturedAsync(
            postgres.ConnectionString,
            DescribeRequests.Of("SELECT @p::int AS a, 'x' AS b")
        );

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);

        capture
            .Calls.Select(call => call.Method)
            .ShouldBe(["open", "prepare", "columns", "types", "check", "explain", "deallocate"]);
    }

    // The name of each column's type as the server spells it with its modifier, with the schema before a type that
    // is not of pg_catalog: format_type leaves the schema out of a type on the search path.
    private const string FormatTypes = """
        SELECT CASE WHEN n.nspname = 'pg_catalog' THEN '' ELSE n.nspname || '.' END
            || pg_catalog.format_type(a.atttypid, a.atttypmod)
        FROM pg_catalog.pg_attribute AS a
        JOIN pg_catalog.pg_type AS t ON t.oid = a.atttypid
        JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace
        WHERE a.attrelid = CAST(@table AS regclass) AND a.attnum > 0 AND NOT a.attisdropped
        ORDER BY a.attnum
        """;

    private Task MakeOwnTablesAsync() => postgres.ExecuteAsync(ScratchSchema.ColumnTypes);

    private async Task<List<string>> FormatTypesOfAsync(string table)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(FormatTypes);
        _ = command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(token);
        var names = new List<string>();
        while (await reader.ReadAsync(token))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static SidecarOrigin Origin(string table, string column, string schema = "public") =>
        new(schema, table, column);

    // Every column of every table and view of the fixture schema, and of the table of these tests: the name the
    // describer spells is the one format_type gives the column, domains, enums, arrays and ranges included.
    [Theory]
    [InlineData("users")]
    [InlineData("orders")]
    [InlineData("bookings")]
    [InlineData("samples")]
    [InlineData("billing.invoices")]
    [InlineData("active_users")]
    [InlineData("user_orders")]
    [InlineData("modifiers")]
    [InlineData("column_types")]
    public async Task DescribeAsync_EveryColumnOfATable_IsSpelledAsFormatTypeSpellsIt(string table)
    {
        await MakeOwnTablesAsync();

        var columns = await ColumnsOfAsync($"SELECT * FROM {table}");

        TypeNamesOf(columns).ShouldBe(await FormatTypesOfAsync(table));
    }

    [Fact]
    public async Task DescribeAsync_ColumnsOfATable_HaveTheirOrigin()
    {
        var columns = await ColumnsOfAsync("SELECT u.id AS user_id, email, amount FROM users u, billing.invoices");

        columns
            .Select(column => column.Origin)
            .ShouldBe([Origin("users", "id"), Origin("users", "email"), Origin("invoices", "amount", "billing")]);
    }

    // ctid and xmin name their table with an attribute number below zero, and a table may have no column at all.
    [Fact]
    public async Task DescribeAsync_SystemColumns_HaveNoOrigin()
    {
        await MakeOwnTablesAsync();

        var ofUsers = await ColumnsOfAsync("SELECT ctid, xmin, id FROM users");
        var ofEmpty = await ColumnsOfAsync("SELECT ctid FROM column_empty");

        ofUsers.Select(column => column.Origin).ShouldBe([null, null, Origin("users", "id")]);
        TypeNamesOf(ofUsers).ShouldBe(["tid", "xid", "integer"]);
        ofEmpty.ShouldHaveSingleItem().Origin.ShouldBeNull();
    }

    [Fact]
    public async Task DescribeAsync_IdentityAndGeneratedColumns_SayWhatTheyAre()
    {
        var columns = await ColumnsOfAsync(
            "SELECT u.id, u.name, u.name_upper, o.id AS order_id FROM users u, orders o"
        );

        columns.Select(column => column.Identity).ShouldBe([true, false, false, true]);
        columns.Select(column => column.Computed).ShouldBe([false, false, true, false]);
    }

    [Fact]
    public async Task DescribeAsync_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity()
    {
        var columns = await ColumnsOfAsync("SELECT count(*) AS n, upper(name) AS u FROM users GROUP BY name");

        columns.ShouldAllBe(column => column.Origin == null && column.Identity == null && column.Computed == null);
        TypeNamesOf(columns).ShouldBe(["bigint", "text"]);
    }

    // The column is its domain.  An expression over it has no origin and says the base type; a cast to the domain
    // says the base type with the domain's modifier, as the protocol reports it.
    [Fact]
    public async Task DescribeAsync_DomainColumnAndExpressionsOverIt_SayTheDomainOrItsBase()
    {
        var columns = await ColumnsOfAsync(
            "SELECT email, COALESCE(email, 'x@y') AS c, NULL::email_t AS n, email::text AS t FROM users"
        );

        TypeNamesOf(columns).ShouldBe(["public.email_t", "character varying", "character varying(254)", "text"]);
        var domain = columns[0].Type.ShouldBeOfType<PostgresType>();
        domain.Kind.ShouldBe(SidecarValues.Kind.Domain);
        domain.Base.ShouldNotBeNull().Name.ShouldBe("character varying(254)");
        columns.Skip(1).ShouldAllBe(column => column.Origin == null);
    }

    // The USING column of an inner join is the left table's; of a full join it is a COALESCE of both, and no
    // table's.
    [Fact]
    public async Task DescribeAsync_UsingColumn_HasTheLeftOriginOfAnInnerJoinAndNoneOfAFullJoin()
    {
        var inner = await ColumnsOfAsync("SELECT user_id FROM orders JOIN bookings USING (user_id)");
        var full = await ColumnsOfAsync("SELECT user_id FROM orders FULL JOIN bookings USING (user_id)");

        inner.ShouldHaveSingleItem().Origin.ShouldBe(Origin("orders", "user_id"));
        full.ShouldHaveSingleItem().Origin.ShouldBeNull();
    }

    [Fact]
    public async Task DescribeAsync_Union_HasNoOrigin() =>
        (await ColumnsOfAsync("SELECT id FROM users UNION ALL SELECT user_id FROM orders"))
            .ShouldHaveSingleItem()
            .Origin.ShouldBeNull();

    [Fact]
    public async Task DescribeAsync_SubqueryAndCte_KeepOrigins()
    {
        var columns = await ColumnsOfAsync(
            "WITH c AS (SELECT id, name FROM users) "
                + "SELECT c.id, s.total FROM c JOIN (SELECT user_id, total FROM orders) AS s ON s.user_id = c.id"
        );

        columns.Select(column => column.Origin).ShouldBe([Origin("users", "id"), Origin("orders", "total")]);
    }

    // The origin of a view's column is the view: what stands behind it is the view's own business.
    [Fact]
    public async Task DescribeAsync_ColumnOfAView_HasTheViewAsItsOrigin() =>
        (await ColumnsOfAsync("SELECT name FROM active_users"))
            .ShouldHaveSingleItem()
            .Origin.ShouldBe(Origin("active_users", "name"));

    [Fact]
    public async Task DescribeAsync_QueryOfATable_AsksTablesBetweenColumnsAndTypes()
    {
        var (described, capture) = await Sessions.DescribeCapturedAsync(
            postgres.ConnectionString,
            DescribeRequests.Of("SELECT id FROM users")
        );

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);

        capture
            .Calls.Select(call => call.Method)
            .ShouldBe(["open", "prepare", "columns", "tables", "types", "check", "deallocate"]);
    }

    // PostgreSQL 18 holds a NOT NULL constraint that is NOT VALID: it sets attnotnull, and rows that were there
    // before may hold NULL.
    [Fact]
    public async Task DescribeAsync_NotNullThatIsNotValid_IsNullable()
    {
        postgres.SkipBelow(18, "Only PostgreSQL 18 has a NOT NULL that is NOT VALID.");
        await postgres.ExecuteAsync(ScratchSchema.NotValid);

        // The premise: the server sets attnotnull for a constraint that is not validated.
        (
            await postgres.ScalarAsync(
                "SELECT attnotnull FROM pg_catalog.pg_attribute "
                    + "WHERE attrelid = 'column_not_valid'::regclass AND attname = 'id'"
            )
        ).ShouldBe(true);

        var columns = await ColumnsOfAsync("SELECT id, name FROM column_not_valid");

        Of(columns).ShouldBe([NullableCatalog, NotNullCatalog]);
    }

    // Before PostgreSQL 18 a child may drop a NOT NULL it inherited, and a query of the parent reads the child's
    // rows, so a NULL comes back for a column the parent says is NOT NULL.  18 refuses the drop, and the answer is
    // the same there: the rule does not depend on the version.
    [Fact]
    public async Task DescribeAsync_ColumnsOfAnInheritanceParent_AreNullableByTheCatalog()
    {
        await postgres.ExecuteAsync(ScratchSchema.Inheritance);

        var columns = await ColumnsOfAsync("SELECT p.id, p.v FROM inh_parent p");

        Of(columns).ShouldBe([NullableCatalog, NullableCatalog]);
    }

    // A foreign partition inherits its parent's NOT NULL, which nothing enforces on the remote rows, and a query of
    // the parent reads them: both columns' origin is the parent, so both are nullable, on every version.
    [Fact]
    public async Task DescribeAsync_ColumnsOfAPartitionedTableWithAForeignPartition_AreNullableByTheCatalog()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        await postgres.ExecuteAsync(ScratchSchema.ForeignPartitions(connection));

        // The premise: a query of the parent returns the remote row's NULL.
        (await postgres.ScalarAsync("SELECT p.v IS NULL FROM fp_parted p WHERE p.id = 1")).ShouldBe(true);

        var columns = await ColumnsOfAsync("SELECT p.id, p.v FROM fp_parted p");

        Of(columns).ShouldBe([NullableCatalog, NullableCatalog]);
    }

    // No column names the partitioned table, so a reference inside an expression is proved by its partitions' rows,
    // and a foreign partition, whose remote rows nothing holds to its NOT NULL, proves nothing.
    [Fact]
    public async Task DescribeAsync_ExpressionOverAPartitionedTableWithAForeignPartition_StaysUnknown()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        await postgres.ExecuteAsync(ScratchSchema.ForeignPartitions(connection));

        // The premise: a query of the parent returns the remote row's NULL.
        (await postgres.ScalarAsync("SELECT p.v IS NULL FROM fp_parted p WHERE p.id = 1")).ShouldBe(true);

        var columns = await ColumnsOfAsync(
            "SELECT u.name, (p.v || 'x') AS t FROM users u JOIN fp_parted p ON p.id = u.id"
        );

        Of(columns).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // PostgreSQL does not enforce a foreign table's NOT NULL on the remote rows, and a query reads them: the column
    // is unknown, and the source says why, in a gated query too, where only a join that extends it makes it nullable.
    [Fact]
    public async Task DescribeAsync_ColumnOfAForeignTable_IsUnknownFromTheForeignTable()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        await postgres.ExecuteAsync(ScratchSchema.ForeignTable(connection));

        // The premise: a query of the foreign table returns the remote row's NULL.
        (await postgres.ScalarAsync("SELECT f.v IS NULL FROM ft_foreign f WHERE f.id = 1")).ShouldBe(true);

        var plain = await ColumnsOfAsync("SELECT f.id, f.v FROM ft_foreign f");
        var gated = await ColumnsOfAsync("SELECT f.id, j.id AS jid FROM ft_foreign f LEFT JOIN users j ON j.id = f.id");

        Of(plain).ShouldBe([UnknownForeignTable, UnknownForeignTable]);
        Of(gated).ShouldBe([UnknownForeignTable, NullableOuterJoin]);
    }

    // The rule replaces the INSERT by its action, and the rows of its RETURNING list are the action's, while the row
    // description keeps the target as the column's origin: the column is nullable whatever the target's NOT NULL
    // says, whether the query is gated or not.  The gated one's plan inserts into the other table under its own
    // alias, which the walk maps to the target's column and finds nothing extends.  Its constant is the rewritten
    // statement's, which the plan prints, and is never NULL.
    [Fact]
    public async Task DescribeAsync_ReturningOfATableWithAnInsteadRule_IsNullableByTheCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        {
            await using var made = source.CreateCommand(ScratchSchema.InsteadRule);
            _ = await made.ExecuteNonQueryAsync(token);

            // The premise: the INSERT returns the rule's row, whose id is NULL.  The row is rolled back.
            await using var connection = await source.OpenConnectionAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var inserted = connection.CreateCommand();
            inserted.CommandText = "INSERT INTO ir_target (id, x) VALUES (1, 2) RETURNING id";
            (await inserted.ExecuteScalarAsync(token)).ShouldBe(DBNull.Value);
            await transaction.RollbackAsync(token);
        }

        var plain = await ShouldDescribeAsync("INSERT INTO ir_target (id, x) VALUES (1, 2) RETURNING id");
        var gated = await ShouldDescribeAsync(
            "INSERT INTO ir_target (id, x) VALUES (1, 2) RETURNING id, left('a', 1) AS l"
        );

        plain.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        Of(plain.Columns.ShouldNotBeNull()).ShouldBe([NullableCatalog]);
        gated.Plan.ShouldBe(SidecarValues.Plan.Walked);
        Of(gated.Columns.ShouldNotBeNull()).ShouldBe([NullableCatalog, NotNullExpression]);
    }

    // A query with no word of the gate.  Its count has no origin, so the plan is asked for it and answers it as an
    // expression, and the columns of the table keep the catalog's answer.
    [Fact]
    public async Task DescribeAsync_QueryWithoutAGateWord_TakesNullabilityFromTheCatalog()
    {
        var description = await ShouldDescribeAsync(
            "SELECT u.id, u.email, count(*) OVER () AS n FROM users u WHERE u.id = @id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        Of(description.Columns.ShouldNotBeNull()).ShouldBe([NotNullCatalog, NullableCatalog, NotNullExpression]);
    }

    // A left join with a count over the window: the plan is walked, the columns of users keep the catalog's answer,
    // the extended orders.total is nullable by the join, and the count is not null as an expression.
    [Fact]
    public async Task DescribeAsync_LeftJoin_IsWalkedAndOnlyTheExtendedSideIsNullableByTheJoin()
    {
        var description = await ShouldDescribeAsync(
            "SELECT u.id, u.email, o.total, count(*) OVER () AS n "
                + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id"
        );

        var columns = description.Columns.ShouldNotBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        TypeNamesOf(columns).ShouldBe(["integer", "public.email_t", "numeric(18,2)", "bigint"]);
        Of(columns).ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin, NotNullExpression]);
        columns
            .Select(column => column.Origin)
            .ShouldBe([Origin("users", "id"), Origin("users", "email"), Origin("orders", "total"), null]);
    }

    // A subtotal row holds NULL in a NOT NULL grouping column, with no outer join in sight, and the column keeps
    // its origin.  The walk refuses a plan with a grouping set, so the heuristic stands.
    [Theory]
    [InlineData("SELECT status, count(*) FROM users GROUP BY ROLLUP (status)")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY CUBE (status)")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY GROUPING SETS ((status), ())")]
    public async Task DescribeAsync_GroupingSetsOverANotNullColumn_IsGated(string sql)
    {
        var description = await ShouldDescribeAsync(sql);

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        var status = description.Columns.ShouldNotBeNull()[0];
        status.Origin.ShouldBe(Origin("users", "status"));
        (status.Nullable, status.NullableSource).ShouldBe(NullableHeuristic);
    }

    // PostgreSQL 18's old row of a RETURNING list is NULL for a row an INSERT adds.  Each column keeps its table as
    // its origin, and the plan prints them as old.id and new.id, which name no relation instance, so both keep the
    // heuristic.
    [Fact]
    public async Task DescribeAsync_ReturningOldAndNew_KeepsTheHeuristic()
    {
        postgres.SkipBelow(18, PostgresContainer.NoOldAndNewInReturning);

        var description = await ShouldDescribeAsync(
            "INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING old.id, new.id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        var columns = description.Columns.ShouldNotBeNull();
        columns.Select(column => column.Origin).ShouldBe([Origin("orders", "id"), Origin("orders", "id")]);
        Of(columns).ShouldBe([NullableHeuristic, NullableHeuristic]);
    }

    // A MERGE of PostgreSQL 17 that deletes the rows of the target the source lacks returns the source's columns
    // as NULL, and the column keeps the source's table as its origin.  The MERGE plans as a join that extends the
    // source, and the row returned is the target's own.
    [Fact]
    public async Task DescribeAsync_MergeReturning_ExtendsTheSourceAndNotTheTarget()
    {
        postgres.SkipBelow(17, "PostgreSQL before 17 has no RETURNING for MERGE.");

        var description = await ShouldDescribeAsync(
            "MERGE INTO orders AS o USING users AS u ON o.user_id = u.id "
                + "WHEN NOT MATCHED BY SOURCE THEN DELETE RETURNING u.id, o.id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        var columns = description.Columns.ShouldNotBeNull();
        columns[0].Origin.ShouldBe(Origin("users", "id"));
        Of(columns).ShouldBe([NullableOuterJoin, NotNullCatalog]);
    }

    // The gate reads words: a function named as one gates a query that has no join.
    [Fact]
    public async Task DescribeAsync_FunctionNamedAsAGateWord_IsWalkedAndFindsNothing()
    {
        var description = await ShouldDescribeAsync("SELECT id, left(name, 3) AS l FROM users");

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        Of(description.Columns.ShouldNotBeNull()).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    [Fact]
    public async Task DescribeAsync_GateWordInAStringOrAComment_IsNotGated()
    {
        var description = await ShouldDescribeAsync("SELECT id FROM users WHERE name <> 'left join' -- outer");

        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        Of(description.Columns.ShouldNotBeNull()).ShouldBe([NotNullCatalog]);
    }

    // PostgreSQL records no NOT NULL for a column of a view, plain or materialized.  In a gated query a view's
    // column says "view" where no join extends it.
    [Fact]
    public async Task DescribeAsync_ColumnsOfAViewAndOfAMaterializedView_SayView()
    {
        await postgres.ExecuteAsync(ScratchSchema.MaterializedUsers);

        var view = await ColumnsOfAsync("SELECT id, name, email, status FROM active_users");
        var materialized = await ColumnsOfAsync("SELECT id FROM column_users");
        var gated = await ColumnsOfAsync(
            "SELECT v.id, o.total FROM active_users v LEFT JOIN orders o ON o.user_id = v.id"
        );

        Of(view).ShouldBe([UnknownView, UnknownView, UnknownView, UnknownView]);
        Of(materialized).ShouldBe([UnknownView]);
        Of(gated).ShouldBe([UnknownView, NullableOuterJoin]);
    }

    // A table's row selected whole is a column of the table's composite type, which is no pseudo-type.
    [Fact]
    public async Task DescribeAsync_ColumnOfACompositeType_IsTheTablesRowTypeWithNoOrigin()
    {
        var column = (await ColumnsOfAsync("SELECT u FROM users u")).ShouldHaveSingleItem();

        column.Name.ShouldBe("u");
        column.Type.Name.ShouldBe("public.users");
        column.Type.ShouldBeOfType<PostgresType>().Kind.ShouldBe(SidecarValues.Kind.Composite);
        column.Origin.ShouldBeNull();
    }

    // A row built in the select list, and the alias of a subquery selected whole, are "record".
    [Theory]
    [InlineData("SELECT id, ROW(1, 2) FROM users", "row")]
    [InlineData("SELECT s FROM (SELECT 1 AS a, 2 AS b) AS s", "s")]
    public async Task DescribeAsync_ColumnOfAPseudoType_IsSqlsrc229NamingItAndItsType(string sql, string column)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, DescribeRequests.Of(sql));

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC229");
        failure.Arguments.ShouldBe([column, DescribeRequests.Query, "record"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
    }
}
