using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresRequests;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: the columns, their facets, tables and origins, a catalog or type that
// lacks one of them, and the spellings of modifiers.  A call the script does not hold is a CaptureMissException, so a
// describe that asks more than its test wrote fails.
public sealed class PostgresSessionColumnTests
{
    [Fact]
    public async Task DescribeAsync_StatementWithColumns_IsRowsWithEachColumnsTypeAndFacets()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)", [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.ExpressionOf(0, "?column?", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "varchar", PostgresRows.Varchar.Oid, length: 100),
            PostgresTables.ExpressionOf(2, "numeric", PostgresRows.Numeric.Oid, precision: 18, scale: 2)
        );
        _ = script.Resolves([23, 1043, 1700], PostgresRows.Int4, PostgresRows.Varchar, PostgresRows.Numeric);
        _ = script.Checks(
            "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)",
            [],
            PostgresTables.ExpressionOf(0, "?column?", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "varchar", PostgresRows.Varchar.Oid),
            PostgresTables.ExpressionOf(2, "numeric", PostgresRows.Numeric.Oid)
        );
        _ = script.Explains(
            1,
            0,
            PostgresScript.PlanOf(
                Scan("x") with
                {
                    NodeType = "Result",
                    Alias = null,
                    Output = ["1", "'a'::character varying(100)", "2.00"],
                }
            )
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT 1, 'a'::varchar(100), 2::numeric(18,2)"),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        description
            .Columns.ShouldNotBeNull()
            .Select(column => (column.Ordinal, column.Name, column.Type.Name))
            .ShouldBe([
                (0, "?column?", "integer"),
                (1, "varchar", "character varying(100)"),
                (2, "numeric", "numeric(18,2)"),
            ]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A statement without rows has no columns, no table and no provenance.
    [Fact]
    public async Task DescribeAsync_StatementWithoutColumns_IsNoneWithNoProvenance()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks("DELETE FROM samples WHERE id = @id", [("id", "integer")]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM samples WHERE id = @id", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // "types" is asked once, for the parameters and the columns together, sorted and each OID once.
    [Fact]
    public async Task DescribeAsync_ParametersAndColumns_AskTypesOnceForBoth()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT $1::text AS a, 1 AS b, 2 AS c", ["unknown"], new([25], null));
        _ = script.Columns(
            1,
            1,
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Text.Oid),
            PostgresTables.ExpressionOf(1, "b", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(2, "c", PostgresRows.Int4.Oid)
        );
        _ = script.Resolves([23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = script.Checks(
            "SELECT @p::text AS a, 1 AS b, 2 AS c",
            [("p", "text")],
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Text.Oid),
            PostgresTables.ExpressionOf(1, "b", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(2, "c", PostgresRows.Int4.Oid)
        );
        _ = script.PlansNothing(1, 1);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p::text AS a, 1 AS b, 2 AS c", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description
            .Parameters.ShouldHaveSingleItem()
            .Type.ShouldBe(TestSidecars.Postgres("text", internalName: "text", bindName: "text"));
        description
            .Columns.ShouldNotBeNull()
            .Select(column => column.Type.Name)
            .ShouldBe(["text", "integer", "integer"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The position the server gives is in the tool's own EXECUTE, so there is no "at character" line.
    [Fact]
    public async Task DescribeAsync_ColumnsThatFails_IsSqlsrc225AtDescribeColumnsAndFreesTheStatement()
    {
        var dropped = new PostgresServerError(
            "57P01",
            "terminating connection due to administrator command",
            null,
            null,
            9
        );
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT 1", [], new([], null));
        _ = script.Answer(
            "columns",
            new PostgresColumnsRequest("sqlsource_1", 0),
            new PostgresColumnsAnswer(null, dropped)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT 1"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe(["57P01: terminating connection due to administrator command"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // "tables" is asked once, for each table once and sorted, whatever the order of the columns.  "types" is then
    // asked for the types of the origin attributes too, which is where a domain is found.
    [Fact]
    public async Task DescribeAsync_ColumnsOfTables_AskTablesOnceAndTypesForTheOriginsToo()
    {
        var users = PostgresTables.Users;
        var orders = PostgresTables.Orders;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT o.total, u.email, u.id FROM orders o, users u", [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.ColumnOf(0, "total", PostgresRows.Numeric.Oid, orders, 3, precision: 18, scale: 2),
            PostgresTables.ColumnOf(1, "email", PostgresRows.Varchar.Oid, users, 3, length: 254),
            PostgresTables.ColumnOf(2, "id", PostgresRows.Int4.Oid, users, 1)
        );
        _ = script.Tables([users.Oid, orders.Oid], users, orders);
        _ = script.Resolves(
            [23, 1043, 1700, PostgresRows.EmailT.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.Numeric,
            PostgresRows.EmailT
        );
        _ = script.Checks(
            "SELECT o.total, u.email, u.id FROM orders o, users u",
            [],
            PostgresTables.ExpressionOf(0, "total", PostgresRows.Numeric.Oid),
            PostgresTables.ExpressionOf(1, "email", PostgresRows.Varchar.Oid),
            PostgresTables.ExpressionOf(2, "id", PostgresRows.Int4.Oid)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT o.total, u.email, u.id FROM orders o, users u"),
            TestContext.Current.CancellationToken
        );

        var columns = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull();
        columns.Select(column => column.Type.Name).ShouldBe(["numeric(18,2)", "public.email_t", "integer"]);
        columns
            .Select(column => column.Origin)
            .ShouldBe([
                new SidecarOrigin("public", "orders", "total"),
                new SidecarOrigin("public", "users", "email"),
                new SidecarOrigin("public", "users", "id"),
            ]);
        columns.Select(column => column.Identity).ShouldBe([false, false, true]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A system column names its table, which is asked for, and has no origin.
    [Fact]
    public async Task DescribeAsync_SystemColumn_HasNoOrigin()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT ctid FROM users", [], new([], null));
        _ = script.Columns(1, 0, new PostgresColumnRow(0, "ctid", 27, users.Oid, -1, null, null, null));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([27], PostgresRows.Tid);
        _ = script.Checks("SELECT ctid FROM users", [], PostgresTables.ExpressionOf(0, "ctid", PostgresRows.Tid.Oid));
        _ = script.PlansNothing(1, 0);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT ctid FROM users"),
            TestContext.Current.CancellationToken
        );

        var column = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        column.Origin.ShouldBeNull();
        column.Type.Name.ShouldBe("tid");
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_TablesThatFails_IsSqlsrc225AtCatalog()
    {
        var refused = new PostgresServerError("42501", "permission denied for table pg_attribute", null, null, 300);
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT id FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Answer("tables", new PostgresTablesRequest([users.Oid]), new PostgresTablesAnswer(null, refused));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT id FROM users"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.ServerLines.ShouldBe(["42501: permission denied for table pg_attribute"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // Another session dropped the table, or a column of it, between "columns" and "tables": the answer lacks what
    // "columns" named.  It is SQLSRC225 at the catalog and not a throw, nothing is asked after it, and the statement
    // is still freed.
    [Fact]
    public async Task DescribeAsync_TableThatTablesLacks_IsSqlsrc225AtCatalogAndAsksNoTypes()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT id FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT id FROM users"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.Arguments.ShouldBe(["Q", "the table of the column 'id' no longer exists"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_AttributeThatTablesLacks_IsSqlsrc225AtCatalogAndAsksNoTypes()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT id FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid], PostgresTables.TableOf(users.Oid, "users"));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT id FROM users"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.Arguments.ShouldBe(["Q", "the column 'id' no longer exists in its table 'public.users'"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    // A type went between "tables" and "types": the answer lacks the row of a type that was asked for.  The first
    // that has none is named, in the order of the request: the parameters, the columns, the origin attributes.  The
    // script holds no "check", so none is asked.
    [Fact]
    public async Task DescribeAsync_TypeOfAColumnThatTypesLacks_IsSqlsrc225AtResolveTypeAndAsksNoCheck()
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT 1 AS n", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.ExpressionOf(0, "n", PostgresRows.Int4.Oid));
        _ = script.Resolves([23]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT 1 AS n"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Arguments.ShouldBe(["Q", "the type of the column 'n' no longer exists"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_TypeOfAParameterThatTypesLacks_IsSqlsrc225AtResolveTypeAndAsksNoCheck()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(
            1,
            "SELECT 1 WHERE $1 = 1",
            ["unknown"],
            new PostgresPrepareAnswer([23], null)
        );
        _ = script.Resolves([23]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT 1 WHERE @id = 1", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Arguments.ShouldBe(["Q", "the type of the parameter '@id' no longer exists"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    // The column's own type is there and the attribute's is not: a domain that went after "tables" named it.
    [Fact]
    public async Task DescribeAsync_TypeOfAnOriginAttributeThatTypesLacks_IsSqlsrc225NamingTheColumn()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT email FROM users", [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.ColumnOf(0, "email", PostgresRows.Varchar.Oid, users, 3, length: 254));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([1043, PostgresRows.EmailT.Oid], PostgresRows.Varchar);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT email FROM users"),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Arguments.ShouldBe(["Q", "the type of the column 'email' no longer exists in its table"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The pairs of the parameter's domain base and of the columns are asked in one request, after "types", sorted
    // and each once: two columns of varchar(10), one of them an array, give one pair.  A column without a modifier
    // gives none.
    [Fact]
    public async Task DescribeAsync_ColumnsWithModifiers_AsksSpellingsOnceAfterTypes()
    {
        const string Sql = "SELECT names, v, amount, n FROM t WHERE email = $1";
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "names", PostgresRows.VarcharArray.Oid, length: 10) with
            {
                TypeModifier = Typmods.Varchar10,
            },
            PostgresTables.ExpressionOf(1, "v", PostgresRows.Varchar.Oid, length: 10) with
            {
                TypeModifier = Typmods.Varchar10,
            },
            PostgresTables.ExpressionOf(2, "amount", PostgresRows.Numeric.Oid, precision: 6, scale: 2) with
            {
                TypeModifier = Typmods.Numeric6Scale2,
            },
            PostgresTables.ExpressionOf(3, "n", PostgresRows.Int4.Oid) with
            {
                TypeModifier = -1,
            },
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, Sql, ["unknown"], new([PostgresRows.EmailT254.Oid], null));
        _ = script.Columns(1, 1, columns);
        _ = script.Resolves(
            [23, 1015, 1043, 1700, PostgresRows.EmailT254.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.VarcharArray,
            PostgresRows.Numeric,
            PostgresRows.EmailT254
        );
        _ = script.Spells(
            (1043, Typmods.Varchar10, "character varying(10)"),
            (1043, Typmods.Varchar254, "character varying(254)"),
            (1700, Typmods.Numeric6Scale2, "numeric(6,2)")
        );
        _ = script.Checks(
            "SELECT names, v, amount, n FROM t WHERE email = @email",
            [("email", "public.email_t")],
            columns
        );
        _ = script.PlansNothing(1, 1);
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(
            Of("SELECT names, v, amount, n FROM t WHERE email = @email", Inferred("email")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        var parameter = description.Parameters.ShouldHaveSingleItem().Type.ShouldBeOfType<PostgresType>();
        parameter.Name.ShouldBe("public.email_t");
        parameter.Base.ShouldNotBeNull().Name.ShouldBe("character varying(254)");
        description
            .Columns.ShouldNotBeNull()
            .Select(column => column.Type.Name)
            .ShouldBe(["character varying(10)[]", "character varying(10)", "numeric(6,2)", "integer"]);
        log.Events.Select(written => written.Name)
            .Where(name => name is "types" or "spellings" or "column")
            .ShouldBe(["types", "spellings", "column", "column", "column", "column"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // Columns and parameters whose types carry no modifier: "spellings" is not in the script, and asking it would
    // be a CaptureMissException.
    [Fact]
    public async Task DescribeAsync_NoModifierToSpell_AsksNoSpellings()
    {
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "a", PostgresRows.Text.Oid) with
            {
                TypeModifier = -1,
            },
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT $1::text AS a", ["unknown"], new([25], null));
        _ = script.Columns(1, 1, columns);
        _ = script.Resolves([25], PostgresRows.Text);
        _ = script.Checks("SELECT @p::text AS a", [("p", "text")], columns);
        _ = script.PlansNothing(1, 1);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT @p::text AS a", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull().ShouldHaveSingleItem().Type.Name.ShouldBe("text");
        exchange.Unused.ShouldBeEmpty();
    }

    // A capture recorded before the modifier was read: no TypeModifier, so Npgsql's numbers spell the columns as
    // they did, the array without its element's length, and nothing is spelled by the server.
    [Fact]
    public async Task DescribeAsync_OldCaptureWithoutModifiers_IsDescribedAsBefore()
    {
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "names", PostgresRows.VarcharArray.Oid, length: 10),
            PostgresTables.ExpressionOf(1, "d", PostgresRows.Interval.Oid, precision: 3),
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT names, d FROM t", [], new([], null));
        _ = script.Columns(1, 0, columns);
        _ = script.Resolves([1015, 1186], PostgresRows.Varchar, PostgresRows.VarcharArray, PostgresRows.Interval);
        _ = script.Checks("SELECT names, d FROM t", [], columns);
        _ = script.PlansNothing(1, 0);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("SELECT names, d FROM t"),
            TestContext.Current.CancellationToken
        );

        var built = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull();
        built.Select(column => column.Type.Name).ShouldBe(["character varying[]", "interval(3)"]);
        built[0]
            .Type.ShouldBeOfType<PostgresType>()
            .Element.ShouldBe(TestSidecars.Postgres("character varying", internalName: "varchar"));
        built[1].Type.ShouldBeOfType<PostgresType>().Precision.ShouldBe(3);
        exchange.Unused.ShouldBeEmpty();
    }

    // An error of "spellings" is one of "types": the types could not be resolved.  The statement is still freed.
    [Fact]
    public async Task DescribeAsync_SpellingsRefused_IsSqlsrc225AtResolveType()
    {
        var dropped = new PostgresServerError(
            "57P01",
            "terminating connection due to administrator command",
            null,
            null,
            null
        );
        PostgresColumnRow[] columns =
        [
            PostgresTables.ExpressionOf(0, "v", PostgresRows.Varchar.Oid, length: 10) with
            {
                TypeModifier = Typmods.Varchar10,
            },
        ];
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "SELECT v FROM t", [], new([], null));
        _ = script.Columns(1, 0, columns);
        _ = script.Resolves([1043], PostgresRows.Varchar);
        _ = script.Answer(
            "spellings",
            new PostgresSpellingsRequest([new PostgresTypmodPair(1043, Typmods.Varchar10)]),
            new PostgresSpellingsAnswer(null, dropped)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of("SELECT v FROM t"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBe(["57P01: terminating connection due to administrator command"]);
        exchange.Unused.ShouldBeEmpty();
    }
}
