using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A query's columns against a real server: what only a server can show, the row description of a statement and
// what Npgsql makes of it.
[Collection(UsesPostgres.Name)]
public sealed class ColumnTests(PostgresContainer postgres)
{
    private async Task<QueryDescription> ShouldDescribeAsync(string sql, IDescribeExchange? exchange = null)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql), exchange);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }

    private async Task<SidecarColumn[]> ColumnsOfAsync(string sql) =>
        [.. (await ShouldDescribeAsync(sql)).Columns.ShouldNotBeNull()];

    private static string[] TypeNamesOf(IEnumerable<SidecarColumn> columns) =>
        [.. columns.Select(static column => column.Type.Name)];

    // One value from the server, on a connection of the test's own.
    private async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ServerMajorAsync() =>
        int.Parse((string)(await ScalarAsync("SHOW server_version_num"))!, CultureInfo.InvariantCulture) / 10000;

    [Fact]
    public async Task Describe_Select_IsRowsWithItsColumnsInOrder()
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
    // type, which Describe_EveryColumnOfATable_IsSpelledAsFormatTypeSpellsIt holds against the server for a column.
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
    public async Task Describe_CastWithAModifier_IsSpelledAsFormatTypeSpellsIt(string type, string expected) =>
        (await ColumnsOfAsync($"SELECT NULL::{type} AS c")).ShouldHaveSingleItem().Type.Name.ShouldBe(expected);

    // The fields of an interval and the modifier of an array's element, which only the server's spelling of the
    // modifier holds.  The facets are decoded from the modifier: an array's are its element's.
    [Theory]
    [InlineData("interval year to month", "interval year to month", null, null, null)]
    [InlineData("interval day to second(3)", "interval day to second(3)", null, 3, null)]
    [InlineData("varchar(10)[]", "character varying(10)[]", 10, null, null)]
    [InlineData("numeric(6,2)[]", "numeric(6,2)[]", null, 6, 2)]
    public async Task Describe_CastWithFieldsOrAnArrayOfAModifier_IsSpelledWithTheModifier(
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
    public async Task Describe_ExpressionOfAnIntervalWithFields_KeepsTheFields()
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
    public async Task Describe_ColumnOfATypeWithItsOwnModifier_IsTheSchemaNameAndTheTail()
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
    // tool takes the server's spelling of a type of pg_catalog as it is, and puts no schema before it.  A parameter
    // of pg_catalog's numeric binds by numeric, which does not depend on the session: it is returned as a column, so
    // a check that bound the domain, sent as text, would see a column of text.
    [Fact]
    public async Task Describe_BuiltInTypeHiddenOnTheSearchPath_IsStillTheCatalogsSpellingAndBindsByTheBareName()
    {
        var token = TestContext.Current.CancellationToken;
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var create = own.CreateCommand("CREATE DOMAIN public.\"numeric\" AS text"))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        try
        {
            var described = await Sessions.DescribeAsync(
                $"{postgres.ConnectionString};Search Path=public,pg_catalog",
                Requests.Of("SELECT NULL::pg_catalog.numeric(10,2) AS n, @p AS p", ("p", "pg_catalog.numeric"))
            );

            described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
            var description = described.Value.ShouldNotBeNull();
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

    [Fact]
    public async Task Describe_NumericWithANegativeScale_HasIt()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 15, "PostgreSQL 14 has no negative scale.");

        var column = (await ColumnsOfAsync("SELECT NULL::numeric(2,-3) AS c")).ShouldHaveSingleItem();

        var type = column.Type.ShouldBeOfType<PostgresType>();
        type.Name.ShouldBe("numeric(2,-3)");
        type.Precision.ShouldBe(2);
        type.Scale.ShouldBe(-3);
    }

    [Fact]
    public async Task Describe_Literals_AreTextAndNamedByTheServer()
    {
        var columns = await ColumnsOfAsync("SELECT 'x', NULL, 1");

        columns.Select(column => column.Name).ShouldBe(["?column?", "?column?", "?column?"]);
        TypeNamesOf(columns).ShouldBe(["text", "text", "integer"]);
    }

    [Fact]
    public async Task Describe_TwoColumnsWithOneName_KeepsBoth()
    {
        var columns = await ColumnsOfAsync("SELECT 1 AS id, 'a'::text AS id");

        columns.Select(column => (column.Ordinal, column.Name)).ShouldBe([(0, "id"), (1, "id")]);
        TypeNamesOf(columns).ShouldBe(["integer", "text"]);
    }

    // A suffix is the generator's to read: the describer records the name the server returned.
    [Fact]
    public async Task Describe_SuffixNames_AreRecordedAsReturned() =>
        (await ColumnsOfAsync("SELECT deleted_at AS \"deleted_at!\", name AS \"name?\" FROM users"))
            .Select(column => column.Name)
            .ShouldBe(["deleted_at!", "name?"]);

    // The statement is described and never run.
    [Fact]
    public async Task Describe_InsertReturning_IsRowsAndInsertsNothing()
    {
        var description = await ShouldDescribeAsync(
            "INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id, placed_at"
        );

        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.Columns.ShouldNotBeNull().Select(column => column.Name).ShouldBe(["id", "placed_at"]);
        (await ScalarAsync("SELECT count(*) FROM orders")).ShouldBe(0L);
    }

    [Fact]
    public async Task Describe_UpdateWithoutReturning_IsNone()
    {
        var description = await ShouldDescribeAsync("UPDATE users SET name = @name WHERE id = @id");

        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
        description.Parameters.Select(parameter => parameter.Name).ShouldBe(["name", "id"]);
    }

    [Fact]
    public async Task Describe_QueryWithoutATable_AsksColumnsAfterPrepareAndTypesOnce()
    {
        var capture = new DescribeCapture("postgres");

        _ = await ShouldDescribeAsync(
            "SELECT @p::int AS a, 'x' AS b",
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        capture
            .Calls.Select(call => call.Method)
            .ShouldBe(["open", "prepare", "columns", "types", "check", "explain", "deallocate"]);
    }

    // Two tables of these tests alone: one with the modifiers the fixture schema lacks, and one with no column.
    // They are made once and left: the container is thrown away, and the fixtures are recorded in one of their own.
    private const string OwnTables = """
        CREATE TABLE IF NOT EXISTS column_types (
            n0 numeric(18, 0),
            n1 numeric(5),
            n numeric,
            ts3 timestamp(3),
            tstz0 timestamptz(0),
            ttz2 timetz(2),
            t4 time(4),
            i3 interval(3),
            b3 bit(3),
            b1 bit,
            vb5 varbit(5),
            vb varbit,
            c1 char,
            bp bpchar,
            vc varchar,
            em email_t NOT NULL,
            ems email_t[],
            statuses user_status[]
        );
        CREATE TABLE IF NOT EXISTS column_empty ();
        """;

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

    private async Task MakeOwnTablesAsync()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(OwnTables);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

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
    public async Task Describe_EveryColumnOfATable_IsSpelledAsFormatTypeSpellsIt(string table)
    {
        await MakeOwnTablesAsync();

        var columns = await ColumnsOfAsync($"SELECT * FROM {table}");

        TypeNamesOf(columns).ShouldBe(await FormatTypesOfAsync(table));
    }

    [Fact]
    public async Task Describe_ColumnsOfATable_HaveTheirOrigin()
    {
        var columns = await ColumnsOfAsync("SELECT u.id AS user_id, email, amount FROM users u, billing.invoices");

        columns
            .Select(column => column.Origin)
            .ShouldBe([Origin("users", "id"), Origin("users", "email"), Origin("invoices", "amount", "billing")]);
    }

    // ctid and xmin name their table with an attribute number below zero, and a table may have no column at all.
    [Fact]
    public async Task Describe_SystemColumns_HaveNoOrigin()
    {
        await MakeOwnTablesAsync();

        var ofUsers = await ColumnsOfAsync("SELECT ctid, xmin, id FROM users");
        var ofEmpty = await ColumnsOfAsync("SELECT ctid FROM column_empty");

        ofUsers.Select(column => column.Origin).ShouldBe([null, null, Origin("users", "id")]);
        TypeNamesOf(ofUsers).ShouldBe(["tid", "xid", "integer"]);
        ofEmpty.ShouldHaveSingleItem().Origin.ShouldBeNull();
    }

    [Fact]
    public async Task Describe_IdentityAndGeneratedColumns_SayWhatTheyAre()
    {
        var columns = await ColumnsOfAsync(
            "SELECT u.id, u.name, u.name_upper, o.id AS order_id FROM users u, orders o"
        );

        columns.Select(column => column.Identity).ShouldBe([true, false, false, true]);
        columns.Select(column => column.Computed).ShouldBe([false, false, true, false]);
    }

    [Fact]
    public async Task Describe_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity()
    {
        var columns = await ColumnsOfAsync("SELECT count(*) AS n, upper(name) AS u FROM users GROUP BY name");

        columns.ShouldAllBe(column => column.Origin == null && column.Identity == null && column.Computed == null);
        TypeNamesOf(columns).ShouldBe(["bigint", "text"]);
    }

    // The column is its domain.  An expression over it has no origin and says the base type; a cast to the domain
    // says the base type with the domain's modifier, as the protocol reports it.
    [Fact]
    public async Task Describe_DomainColumnAndExpressionsOverIt_SayTheDomainOrItsBase()
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
    public async Task Describe_UsingColumn_HasTheLeftOriginOfAnInnerJoinAndNoneOfAFullJoin()
    {
        var inner = await ColumnsOfAsync("SELECT user_id FROM orders JOIN bookings USING (user_id)");
        var full = await ColumnsOfAsync("SELECT user_id FROM orders FULL JOIN bookings USING (user_id)");

        inner.ShouldHaveSingleItem().Origin.ShouldBe(Origin("orders", "user_id"));
        full.ShouldHaveSingleItem().Origin.ShouldBeNull();
    }

    [Fact]
    public async Task Describe_Union_HasNoOrigin() =>
        (await ColumnsOfAsync("SELECT id FROM users UNION ALL SELECT user_id FROM orders"))
            .ShouldHaveSingleItem()
            .Origin.ShouldBeNull();

    [Fact]
    public async Task Describe_SubqueryAndCte_KeepOrigins()
    {
        var columns = await ColumnsOfAsync(
            "WITH c AS (SELECT id, name FROM users) "
                + "SELECT c.id, s.total FROM c JOIN (SELECT user_id, total FROM orders) AS s ON s.user_id = c.id"
        );

        columns.Select(column => column.Origin).ShouldBe([Origin("users", "id"), Origin("orders", "total")]);
    }

    // The origin of a view's column is the view: what stands behind it is the view's own business.
    [Fact]
    public async Task Describe_ColumnOfAView_HasTheViewAsItsOrigin() =>
        (await ColumnsOfAsync("SELECT name FROM active_users"))
            .ShouldHaveSingleItem()
            .Origin.ShouldBe(Origin("active_users", "name"));

    [Fact]
    public async Task Describe_QueryOfATable_AsksTablesBetweenColumnsAndTypes()
    {
        var capture = new DescribeCapture("postgres");

        _ = await ShouldDescribeAsync("SELECT id FROM users", new CapturingExchange(LiveExchange.Instance, capture));

        capture
            .Calls.Select(call => call.Method)
            .ShouldBe(["open", "prepare", "columns", "tables", "types", "check", "deallocate"]);
    }

    private static (bool? Nullable, string? Source)[] NullabilityOf(IEnumerable<SidecarColumn> columns) =>
        [.. columns.Select(static column => (column.Nullable, column.NullableSource))];

    // PostgreSQL 18 holds a NOT NULL constraint that is NOT VALID: it sets attnotnull, and rows that were there
    // before may hold NULL.  The table is made by SQL that can be run again.
    private const string NotValidTable = """
        CREATE TABLE IF NOT EXISTS column_not_valid (id int, name text NOT NULL);
        DO $$
        BEGIN
            ALTER TABLE column_not_valid ADD CONSTRAINT column_not_valid_id NOT NULL id NOT VALID;
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $$;
        """;

    [Fact]
    public async Task Describe_NotNullThatIsNotValid_IsNullable()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 18, "Only PostgreSQL 18 has a NOT NULL that is NOT VALID.");
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        {
            await using var command = source.CreateCommand(NotValidTable);
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        // The premise: the server sets attnotnull for a constraint that is not validated.
        (
            await ScalarAsync(
                "SELECT attnotnull FROM pg_catalog.pg_attribute "
                    + "WHERE attrelid = 'column_not_valid'::regclass AND attname = 'id'"
            )
        ).ShouldBe(true);

        var columns = await ColumnsOfAsync("SELECT id, name FROM column_not_valid");

        NullabilityOf(columns).ShouldBe([(true, "catalog"), (false, "catalog")]);
    }

    // A legacy inheritance parent and its child, of this test alone, made by SQL that can be run again.
    private const string InheritanceTables = """
        CREATE TABLE IF NOT EXISTS inh_parent (id integer NOT NULL, v text NOT NULL);
        CREATE TABLE IF NOT EXISTS inh_child () INHERITS (inh_parent);
        """;

    // Before PostgreSQL 18 a child may drop a NOT NULL it inherited, and a query of the parent reads the child's
    // rows, so a NULL comes back for a column the parent says is NOT NULL.  18 refuses the drop, and the answer is
    // the same there: the rule does not depend on the version.
    [Fact]
    public async Task Describe_ColumnsOfAnInheritanceParent_AreNullableByTheCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        {
            await using var made = source.CreateCommand(InheritanceTables);
            _ = await made.ExecuteNonQueryAsync(token);
            if (await ServerMajorAsync() < 18)
            {
                await using var dropped = source.CreateCommand("ALTER TABLE inh_child ALTER COLUMN v DROP NOT NULL");
                _ = await dropped.ExecuteNonQueryAsync(token);
            }
        }

        var columns = await ColumnsOfAsync("SELECT p.id, p.v FROM inh_parent p");

        NullabilityOf(columns).ShouldBe([(true, "catalog"), (true, "catalog")]);
    }

    // A partitioned table, of this test alone, with a foreign partition through postgres_fdw over a loopback server
    // to the container's own database, at the server's port inside the container, made by SQL that can be run
    // again.  Its remote row holds NULL in v, which the parent says is NOT NULL.
    private static string ForeignPartitionTables(NpgsqlConnectionStringBuilder connection) =>
        $$"""
            {{ForeignServer(connection)}}
            CREATE TABLE IF NOT EXISTS fp_remote (id integer NOT NULL, kind integer NOT NULL, v text);
            INSERT INTO fp_remote SELECT 1, 2, NULL WHERE NOT EXISTS (SELECT 1 FROM fp_remote);
            CREATE TABLE IF NOT EXISTS fp_parted (id integer NOT NULL, kind integer NOT NULL, v text NOT NULL)
                PARTITION BY LIST (kind);
            CREATE TABLE IF NOT EXISTS fp_parted_1 PARTITION OF fp_parted FOR VALUES IN (1);
            CREATE FOREIGN TABLE IF NOT EXISTS fp_parted_2 PARTITION OF fp_parted FOR VALUES IN (2)
                SERVER fp_loop OPTIONS (table_name 'fp_remote');
            """;

    // The loopback server the foreign tables of these tests share, made by SQL that can be run again.
    private static string ForeignServer(NpgsqlConnectionStringBuilder connection) =>
        $$"""
            CREATE EXTENSION IF NOT EXISTS postgres_fdw;
            CREATE SERVER IF NOT EXISTS fp_loop FOREIGN DATA WRAPPER postgres_fdw
                OPTIONS (dbname {{Literal(connection.Database)}}, host 'localhost', port '5432');
            CREATE USER MAPPING IF NOT EXISTS FOR CURRENT_USER SERVER fp_loop
                OPTIONS (user {{Literal(connection.Username)}}, password {{Literal(connection.Password)}});
            """;

    private static string Literal(string? value) =>
        $"'{(value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal)}'";

    // A foreign partition inherits its parent's NOT NULL, which nothing enforces on the remote rows, and a query of
    // the parent reads them: both columns' origin is the parent, so both are nullable, on every version.
    [Fact]
    public async Task Describe_ColumnsOfAPartitionedTableWithAForeignPartition_AreNullableByTheCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        {
            await using var made = source.CreateCommand(
                ForeignPartitionTables(new NpgsqlConnectionStringBuilder(postgres.ConnectionString))
            );
            _ = await made.ExecuteNonQueryAsync(token);
        }

        // The premise: a query of the parent returns the remote row's NULL.
        (await ScalarAsync("SELECT p.v IS NULL FROM fp_parted p WHERE p.id = 1")).ShouldBe(true);

        var columns = await ColumnsOfAsync("SELECT p.id, p.v FROM fp_parted p");

        NullabilityOf(columns).ShouldBe([(true, "catalog"), (true, "catalog")]);
    }

    // A foreign table of this test alone, through the same loopback server, whose remote row holds NULL in v, which
    // the foreign table says is NOT NULL and PostgreSQL does not enforce.  Made by SQL that can be run again.
    private static string ForeignTable(NpgsqlConnectionStringBuilder connection) =>
        $$"""
            {{ForeignServer(connection)}}
            CREATE TABLE IF NOT EXISTS ft_remote (id integer NOT NULL, v text);
            INSERT INTO ft_remote SELECT 1, NULL WHERE NOT EXISTS (SELECT 1 FROM ft_remote);
            CREATE FOREIGN TABLE IF NOT EXISTS ft_foreign (id integer NOT NULL, v text NOT NULL)
                SERVER fp_loop OPTIONS (table_name 'ft_remote');
            """;

    // PostgreSQL does not enforce a foreign table's NOT NULL on the remote rows, and a query reads them: the column
    // is unknown, and the source says why, in a gated query too, where only a join that extends it makes it nullable.
    [Fact]
    public async Task Describe_ColumnOfAForeignTable_IsUnknownFromTheForeignTable()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        {
            await using var made = source.CreateCommand(
                ForeignTable(new NpgsqlConnectionStringBuilder(postgres.ConnectionString))
            );
            _ = await made.ExecuteNonQueryAsync(token);
        }

        // The premise: a query of the foreign table returns the remote row's NULL.
        (await ScalarAsync("SELECT f.v IS NULL FROM ft_foreign f WHERE f.id = 1")).ShouldBe(true);

        var plain = await ColumnsOfAsync("SELECT f.id, f.v FROM ft_foreign f");
        var gated = await ColumnsOfAsync("SELECT f.id, j.id AS jid FROM ft_foreign f LEFT JOIN users j ON j.id = f.id");

        NullabilityOf(plain).ShouldBe([(null, "foreign-table"), (null, "foreign-table")]);
        NullabilityOf(gated).ShouldBe([(null, "foreign-table"), (true, "outer-join")]);
    }

    // A table with a DO INSTEAD rule on INSERT, and the table the rule writes to instead, of this test alone, made by
    // SQL that can be run again.  The rule writes NULL into the other table's id and returns the other table's row.
    private const string InsteadRuleTables = """
        CREATE TABLE IF NOT EXISTS ir_target (id integer NOT NULL, x integer);
        CREATE TABLE IF NOT EXISTS ir_other (id integer, x integer);
        CREATE OR REPLACE RULE ir_target_insert AS ON INSERT TO ir_target
            DO INSTEAD INSERT INTO ir_other VALUES (NULL, NEW.x) RETURNING ir_other.*;
        """;

    // The rule replaces the INSERT by its action, and the rows of its RETURNING list are the action's, while the row
    // description keeps the target as the column's origin: the column is nullable whatever the target's NOT NULL
    // says, whether the query is gated or not.  The gated one's plan inserts into the other table under its own
    // alias, which the walk maps to the target's column and finds nothing extends.  Its constant is the rewritten
    // statement's, which the plan prints, and is never NULL.
    [Fact]
    public async Task Describe_ReturningOfATableWithAnInsteadRule_IsNullableByTheCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        {
            await using var made = source.CreateCommand(InsteadRuleTables);
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
        NullabilityOf(plain.Columns.ShouldNotBeNull()).ShouldBe([(true, "catalog")]);
        gated.Plan.ShouldBe(SidecarValues.Plan.Walked);
        NullabilityOf(gated.Columns.ShouldNotBeNull()).ShouldBe([(true, "catalog"), (false, "expression")]);
    }

    // The query of the spec's Goal, without its join.  Its count has no origin, so the plan is asked for it and
    // answers it, and the columns of the table keep the catalog's answer.
    [Fact]
    public async Task Describe_QueryWithoutAGateWord_TakesNullabilityFromTheCatalog()
    {
        var description = await ShouldDescribeAsync(
            "SELECT u.id, u.email, count(*) OVER () AS n FROM users u WHERE u.id = @id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        NullabilityOf(description.Columns.ShouldNotBeNull())
            .ShouldBe([(false, "catalog"), (true, "catalog"), (false, "expression")]);
    }

    // The query of the spec's Goal: each row of its table.
    [Fact]
    public async Task Describe_LeftJoin_IsWalkedAndOnlyTheExtendedSideIsNullableByTheJoin()
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
        NullabilityOf(columns)
            .ShouldBe([(false, "catalog"), (true, "catalog"), (true, "outer-join"), (false, "expression")]);
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
    public async Task Describe_GroupingSetsOverANotNullColumn_IsGated(string sql)
    {
        var description = await ShouldDescribeAsync(sql);

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        var status = description.Columns.ShouldNotBeNull()[0];
        status.Origin.ShouldBe(Origin("users", "status"));
        (status.Nullable, status.NullableSource).ShouldBe((true, "heuristic"));
    }

    // PostgreSQL 18's old and new row of a RETURNING list are NULL for the row an INSERT had no old one of, and
    // each column keeps its table as its origin, and the plan prints them as old.id and new.id, which name no
    // relation instance.
    [Fact]
    public async Task Describe_ReturningOldAndNew_KeepsTheHeuristic()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 18, "PostgreSQL before 18 has no old and new in RETURNING.");

        var description = await ShouldDescribeAsync(
            "INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING old.id, new.id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        var columns = description.Columns.ShouldNotBeNull();
        columns.Select(column => column.Origin).ShouldBe([Origin("orders", "id"), Origin("orders", "id")]);
        NullabilityOf(columns).ShouldBe([(true, "heuristic"), (true, "heuristic")]);
    }

    // A MERGE of PostgreSQL 17 that deletes the rows of the target the source lacks returns the source's columns
    // as NULL, and the column keeps the source's table as its origin.  The MERGE plans as a join that extends the
    // source, and the row returned is the target's own.
    [Fact]
    public async Task Describe_MergeReturning_ExtendsTheSourceAndNotTheTarget()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 17, "PostgreSQL before 17 has no RETURNING for MERGE.");

        var description = await ShouldDescribeAsync(
            "MERGE INTO orders AS o USING users AS u ON o.user_id = u.id "
                + "WHEN NOT MATCHED BY SOURCE THEN DELETE RETURNING u.id, o.id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        var columns = description.Columns.ShouldNotBeNull();
        columns[0].Origin.ShouldBe(Origin("users", "id"));
        NullabilityOf(columns).ShouldBe([(true, "outer-join"), (false, "catalog")]);
    }

    // The gate reads words: a function named as one gates a query that has no join.
    [Fact]
    public async Task Describe_FunctionNamedAsAGateWord_IsWalkedAndFindsNothing()
    {
        var description = await ShouldDescribeAsync("SELECT id, left(name, 3) AS l FROM users");

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        NullabilityOf(description.Columns.ShouldNotBeNull()).ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    [Fact]
    public async Task Describe_GateWordInAStringOrAComment_IsNotGated()
    {
        var description = await ShouldDescribeAsync("SELECT id FROM users WHERE name <> 'left join' -- outer");

        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        NullabilityOf(description.Columns.ShouldNotBeNull()).ShouldBe([(false, "catalog")]);
    }

    // PostgreSQL records no NOT NULL for a column of a view, plain or materialized.  In a gated query a view's
    // column says "view" where no join extends it.
    [Fact]
    public async Task Describe_ColumnsOfAViewAndOfAMaterializedView_SayView()
    {
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        await using (
            var create = source.CreateCommand(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS column_users AS SELECT id, name FROM users"
            )
        )
        {
            _ = await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var view = await ColumnsOfAsync("SELECT id, name, email, status FROM active_users");
        var materialized = await ColumnsOfAsync("SELECT id FROM column_users");
        var gated = await ColumnsOfAsync(
            "SELECT v.id, o.total FROM active_users v LEFT JOIN orders o ON o.user_id = v.id"
        );

        NullabilityOf(view).ShouldAllBe(found => found.Nullable == null && found.Source == "view");
        NullabilityOf(materialized).ShouldBe([(null, "view")]);
        NullabilityOf(gated).ShouldBe([(null, "view"), (true, "outer-join")]);
    }

    // A table's row selected whole is a column of the table's composite type, which is no pseudo-type.
    [Fact]
    public async Task Describe_ColumnOfACompositeType_IsTheTablesRowTypeWithNoOrigin()
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
    public async Task Describe_ColumnOfAPseudoType_IsSqlsrc229NamingItAndItsType(string sql, string column)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql));

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC229");
        failure.Arguments.ShouldBe([column, Requests.Query, "record"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
    }
}
