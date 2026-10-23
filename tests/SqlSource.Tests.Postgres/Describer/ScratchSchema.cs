using System;
using Npgsql;
using SqlSource.Tool.Testing;

namespace SqlSource.Tests.Postgres.Describer;

// Every object that a test of the UsesPostgres collection leaves on its one server beyond the fixture has its text
// here alone, made by SQL that can be run again, and is never dropped beyond what its own text replaces: the
// container is thrown away, and the fixtures are recorded in one of their own.  So a class that runs it first makes
// the same object as any other would.  An object that changes what another test sees while it exists,
// public."numeric", the schema "user", the schema shadow of ShadowCount and ShadowOperators, the aggregate
// pg_catalog.count(text) of CatalogCount, the operator pg_catalog.* of CatalogOperator, describer_canary,
// command_widgets, error_path_widgets and session_gone, is made by its test and dropped in a finally.
internal static class ScratchSchema
{
    /// <summary>
    /// A table partitioned by <c>kind</c> into <c>scratch_parts_1</c> and <c>scratch_parts_2</c>, of which
    /// <c>note</c> is the one column that may be <c>NULL</c>.  It is empty: a test that needs rows inserts them in a
    /// transaction it rolls back.
    /// </summary>
    public const string Partitioned = """
        CREATE TABLE IF NOT EXISTS scratch_parts (id integer NOT NULL, kind integer NOT NULL, note text)
            PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS scratch_parts_1 PARTITION OF scratch_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS scratch_parts_2 PARTITION OF scratch_parts FOR VALUES IN (2);
        """;

    /// <summary>
    /// Aggregates <c>shadow.count(*)</c> and <c>shadow.count(integer)</c> that always return <c>NULL</c>.  On a path
    /// that names <c>shadow</c> before <c>pg_catalog</c> the plan prints the first as <c>count(*)</c>; on any path that
    /// names <c>shadow</c>, the second beats <c>pg_catalog.count("any")</c> for an integer by its exact type and prints
    /// as <c>count(id)</c>.
    /// </summary>
    public const string ShadowCount = """
        CREATE SCHEMA IF NOT EXISTS shadow;
        CREATE OR REPLACE FUNCTION shadow.nulls(int8) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8';
        CREATE OR REPLACE AGGREGATE shadow.count(*) (sfunc = shadow.nulls, stype = int8);
        CREATE OR REPLACE FUNCTION shadow.nulls(int8, integer) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8';
        CREATE OR REPLACE AGGREGATE shadow.count(integer) (sfunc = shadow.nulls, stype = int8);
        """;

    /// <summary>
    /// An aggregate <c>shadow.max(timestamptz)</c> and an operator <c>shadow.*</c> of <c>(numeric, numeric)</c> that
    /// always return <c>NULL</c>.  On a path that names <c>shadow</c> before <c>pg_catalog</c> each is chosen over the
    /// catalog's by the same argument types, and the plan prints it bare, as the catalog's would print.  Their
    /// functions are PL/pgSQL, since the planner inlines a function of SQL and the plan would then show the constant.
    /// </summary>
    public const string ShadowOperators = """
        CREATE SCHEMA IF NOT EXISTS shadow;
        CREATE OR REPLACE FUNCTION shadow.null_timestamptz(timestamptz, timestamptz) RETURNS timestamptz
            LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';
        CREATE OR REPLACE AGGREGATE shadow.max(timestamptz) (sfunc = shadow.null_timestamptz, stype = timestamptz);
        CREATE OR REPLACE FUNCTION shadow.null_numeric(numeric, numeric) RETURNS numeric
            LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';
        DROP OPERATOR IF EXISTS shadow.* (numeric, numeric);
        CREATE OPERATOR shadow.* (leftarg = numeric, rightarg = numeric, function = shadow.null_numeric);
        """;

    /// <summary>
    /// Drops the schema <c>shadow</c> with what <see cref="ShadowCount" /> and <see cref="ShadowOperators" /> made in
    /// it.  Every test that runs either runs this in a finally: while <c>shadow</c> holds a <c>count</c> or a
    /// <c>*</c>, the describer reads no <c>count(x)</c> and no range intersection of any other test.
    /// </summary>
    public const string DropShadow = "DROP SCHEMA IF EXISTS shadow CASCADE";

    /// <summary>
    /// An aggregate <c>pg_catalog.count(text)</c> that always returns <c>NULL</c>, added to <c>pg_catalog</c> itself as
    /// a superuser or an extension installed there may.  Its function is PL/pgSQL, since the planner inlines a function
    /// of SQL, and is in <c>pg_catalog</c> too.  The plan prints a call of it bare, as <c>count(...)</c>, whatever the
    /// path's order.
    /// </summary>
    public const string CatalogCount = """
        CREATE OR REPLACE FUNCTION pg_catalog.sqlsource_test_nulls(int8, text) RETURNS int8
            LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';
        CREATE OR REPLACE AGGREGATE pg_catalog.count(text) (sfunc = pg_catalog.sqlsource_test_nulls, stype = int8);
        """;

    /// <summary>
    /// Drops what <see cref="CatalogCount" /> made.  Every test that runs it runs this in a finally: while
    /// <c>pg_catalog</c> holds a <c>count</c> that is not built in, the describer reads no <c>count(x)</c> of any
    /// other test.
    /// </summary>
    public const string DropCatalogCount = """
        DROP AGGREGATE IF EXISTS pg_catalog.count(text);
        DROP FUNCTION IF EXISTS pg_catalog.sqlsource_test_nulls(int8, text);
        """;

    /// <summary>
    /// An operator <c>pg_catalog.*</c> over <c>(text, text)</c>, for which <c>pg_catalog</c> has none, that always
    /// returns <c>NULL</c>, added to <c>pg_catalog</c> itself.  Its function is PL/pgSQL and in <c>pg_catalog</c> too.
    /// </summary>
    public const string CatalogOperator = """
        CREATE OR REPLACE FUNCTION pg_catalog.sqlsource_test_null_text(text, text) RETURNS text
            LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';
        DROP OPERATOR IF EXISTS pg_catalog.* (text, text);
        CREATE OPERATOR pg_catalog.* (leftarg = text, rightarg = text, function = pg_catalog.sqlsource_test_null_text);
        """;

    /// <summary>
    /// Drops what <see cref="CatalogOperator" /> made.  Every test that runs it runs this in a finally: while
    /// <c>pg_catalog</c> holds a <c>*</c> that is not built in, the describer reads no range intersection of any other
    /// test.
    /// </summary>
    public const string DropCatalogOperator = """
        DROP OPERATOR IF EXISTS pg_catalog.* (text, text);
        DROP FUNCTION IF EXISTS pg_catalog.sqlsource_test_null_text(text, text);
        """;

    /// <summary>
    /// The extension <c>postgres_fdw</c> and one loopback server, <c>scratch_loop</c>, to the container's own
    /// database at the server's port inside the container, with a user mapping for the connection's user.  Every
    /// foreign table of the tests is through it; each text that makes one starts with this.
    /// </summary>
    public static string ForeignServer(NpgsqlConnectionStringBuilder connection) =>
        $$"""
            CREATE EXTENSION IF NOT EXISTS postgres_fdw;
            CREATE SERVER IF NOT EXISTS scratch_loop FOREIGN DATA WRAPPER postgres_fdw
                OPTIONS (dbname {{Literal(connection.Database)}}, host 'localhost', port '5432');
            CREATE USER MAPPING IF NOT EXISTS FOR CURRENT_USER SERVER scratch_loop
                OPTIONS (user {{Literal(connection.Username)}}, password {{Literal(connection.Password)}});
            """;

    private static string Literal(string? value) =>
        $"'{(value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal)}'";

    // Made by ColumnTests.

    /// <summary>
    /// <c>column_types</c>, a table with the modifiers the fixture schema lacks, and <c>column_empty</c>, a table with
    /// no column.  Both are empty.
    /// </summary>
    public const string ColumnTypes = """
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

    /// <summary>
    /// <c>column_not_valid</c>, whose <c>id</c> has a NOT NULL constraint that is NOT VALID, which sets
    /// <c>attnotnull</c> while rows that were there before may hold <c>NULL</c>.  Only PostgreSQL 18 has one, so a
    /// test runs this only after it skips below 18.
    /// </summary>
    public const string NotValid = """
        CREATE TABLE IF NOT EXISTS column_not_valid (id int, name text NOT NULL);
        DO $$
        BEGIN
            ALTER TABLE column_not_valid ADD CONSTRAINT column_not_valid_id NOT NULL id NOT VALID;
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $$;
        """;

    /// <summary>
    /// A legacy inheritance parent <c>inh_parent</c>, whose columns are NOT NULL, and its child <c>inh_child</c>.
    /// Before PostgreSQL 18 the child drops the NOT NULL of <c>v</c> that it inherited, which 18 refuses: before 18 the
    /// child's <c>v</c> may hold <c>NULL</c>.  Both are empty.
    /// </summary>
    public const string Inheritance = """
        CREATE TABLE IF NOT EXISTS inh_parent (id integer NOT NULL, v text NOT NULL);
        CREATE TABLE IF NOT EXISTS inh_child () INHERITS (inh_parent);
        DO $$
        BEGIN
            IF current_setting('server_version_num')::int < 180000 THEN
                ALTER TABLE inh_child ALTER COLUMN v DROP NOT NULL;
            END IF;
        END
        $$;
        """;

    /// <summary>
    /// <c>fp_parted</c>, partitioned by <c>kind</c>, whose columns are NOT NULL, into <c>fp_parted_1</c>, empty, and
    /// the foreign partition <c>fp_parted_2</c> over <c>fp_remote</c>, whose one row, of id 1, holds <c>NULL</c> in
    /// <c>v</c>.
    /// </summary>
    public static string ForeignPartitions(NpgsqlConnectionStringBuilder connection) =>
        $$"""
            {{ForeignServer(connection)}}
            CREATE TABLE IF NOT EXISTS fp_remote (id integer NOT NULL, kind integer NOT NULL, v text);
            INSERT INTO fp_remote SELECT 1, 2, NULL WHERE NOT EXISTS (SELECT 1 FROM fp_remote);
            CREATE TABLE IF NOT EXISTS fp_parted (id integer NOT NULL, kind integer NOT NULL, v text NOT NULL)
                PARTITION BY LIST (kind);
            CREATE TABLE IF NOT EXISTS fp_parted_1 PARTITION OF fp_parted FOR VALUES IN (1);
            CREATE FOREIGN TABLE IF NOT EXISTS fp_parted_2 PARTITION OF fp_parted FOR VALUES IN (2)
                SERVER scratch_loop OPTIONS (table_name 'fp_remote');
            """;

    /// <summary>
    /// The foreign table <c>ft_foreign</c>, whose columns are NOT NULL, over <c>ft_remote</c>, whose one row, of id 1,
    /// holds <c>NULL</c> in <c>v</c>, which PostgreSQL does not hold to the foreign table's NOT NULL.
    /// </summary>
    public static string ForeignTable(NpgsqlConnectionStringBuilder connection) =>
        $$"""
            {{ForeignServer(connection)}}
            CREATE TABLE IF NOT EXISTS ft_remote (id integer NOT NULL, v text);
            INSERT INTO ft_remote SELECT 1, NULL WHERE NOT EXISTS (SELECT 1 FROM ft_remote);
            CREATE FOREIGN TABLE IF NOT EXISTS ft_foreign (id integer NOT NULL, v text NOT NULL)
                SERVER scratch_loop OPTIONS (table_name 'ft_remote');
            """;

    /// <summary>
    /// <c>ir_target</c>, whose <c>id</c> is NOT NULL, with a DO INSTEAD rule on INSERT that writes <c>NULL</c> into
    /// the id of <c>ir_other</c> and returns that row.  Both are empty: a test that inserts rolls back.
    /// </summary>
    public const string InsteadRule = """
        CREATE TABLE IF NOT EXISTS ir_target (id integer NOT NULL, x integer);
        CREATE TABLE IF NOT EXISTS ir_other (id integer, x integer);
        CREATE OR REPLACE RULE ir_target_insert AS ON INSERT TO ir_target
            DO INSTEAD INSERT INTO ir_other VALUES (NULL, NEW.x) RETURNING ir_other.*;
        """;

    /// <summary>The materialized view <c>column_users</c>, of the <c>id</c> and <c>name</c> of <c>users</c>.</summary>
    public const string MaterializedUsers =
        "CREATE MATERIALIZED VIEW IF NOT EXISTS column_users AS SELECT id, name FROM users";

    // Made by TableMatchTests.

    /// <summary>
    /// <c>match_dropped</c>, whose second attribute, <c>gone</c>, is dropped, leaving <c>id</c> and <c>kept</c>.
    /// </summary>
    public const string DroppedColumn = """
        CREATE TABLE IF NOT EXISTS match_dropped (id integer PRIMARY KEY, gone text, kept text);
        ALTER TABLE match_dropped DROP COLUMN IF EXISTS gone;
        """;

    // Made by ExplainTests.

    /// <summary><c>walk_explain_rows</c>, with one row: id 1, labelled <c>kept</c>.</summary>
    public const string ExplainRows = """
        CREATE TABLE IF NOT EXISTS walk_explain_rows (id integer PRIMARY KEY, label text);
        INSERT INTO walk_explain_rows VALUES (1, 'kept') ON CONFLICT (id) DO UPDATE SET label = 'kept';
        """;

    // Made by SetOperationTests.

    /// <summary>
    /// The view <c>setop_leaf_alias</c>, whose subquery is named <c>"*SELECT* 9"</c> like a leaf scan of a set
    /// operation, and prints <c>b</c>, <c>NULLIF(name, 'x')</c>, before <c>a</c>, the name of each of <c>users</c>.
    /// </summary>
    public const string LeafAliasView =
        "CREATE OR REPLACE VIEW setop_leaf_alias AS SELECT \"*SELECT* 9\".b, \"*SELECT* 9\".a FROM "
        + "(SELECT u.name AS a, NULLIF(u.name, 'x') AS b FROM users u OFFSET 0) AS \"*SELECT* 9\"";

    /// <summary>
    /// The SQL function <c>setop_pairs()</c>, which the planner inlines: a UNION ALL of the names of <c>users</c>
    /// twice and of the names with <c>NULLIF(name, 'x')</c> under a LIMIT.
    /// </summary>
    public const string PairsFunction =
        "CREATE OR REPLACE FUNCTION setop_pairs() RETURNS TABLE (a varchar, b varchar) LANGUAGE sql STABLE AS "
        + "$$ SELECT u.name, u.name FROM users u "
        + "UNION ALL (SELECT x.name, NULLIF(x.name, 'x') FROM users x LIMIT 5) $$";

    // Made by ExpressionTests.

    /// <summary>
    /// <c>expression_amounts</c>, empty, whose NOT NULL <c>amount</c> is the domain <c>money_t</c>, a type that no
    /// signature of the tables takes.
    /// </summary>
    public const string Amounts =
        "CREATE TABLE IF NOT EXISTS expression_amounts (id integer NOT NULL PRIMARY KEY, amount money_t NOT NULL)";

    /// <summary>
    /// The view <c>expression_lonely</c>: the users with no order, by a LEFT JOIN ... IS NULL, with the constant
    /// <c>k</c> of the side it extends, so the anti join it plans as returns <c>k</c> as <c>NULL</c>.
    /// </summary>
    public const string LonelyView =
        "CREATE OR REPLACE VIEW expression_lonely AS SELECT u.id, s.k FROM users u "
        + "LEFT JOIN (SELECT o.user_id, 'x'::text AS k FROM orders o) s ON s.user_id = u.id "
        + "WHERE s.user_id IS NULL";

    // Made by NpgsqlCheckTests.

    /// <summary>The longest name a type can have: PostgreSQL keeps 63 bytes of a name.</summary>
    public const string LongTypeName = "check_enum_with_a_name_of_exactly_sixty_three_characters_long00";

    /// <summary>
    /// What the check's tests need and the fixture schema lacks: the enum <c>"My Schema"."Status"</c> in a schema
    /// whose name needs quotes, <c>check_d2</c>, a domain over a domain, the composite type <c>check_pair</c>, the
    /// enum <see cref="LongTypeName"/>, and <c>check_canary</c>, an empty table that a describe must leave as it is.
    /// </summary>
    public const string CheckTypes = $$"""
        CREATE SCHEMA IF NOT EXISTS "My Schema";
        DO $do$
        BEGIN
            CREATE TYPE "My Schema"."Status" AS ENUM ('a', 'b');
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        DO $do$
        BEGIN
            CREATE DOMAIN check_d2 AS email_t;
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        DO $do$
        BEGIN
            CREATE TYPE check_pair AS (a int, b int);
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        DO $do$
        BEGIN
            CREATE TYPE {{LongTypeName}} AS ENUM ('a', 'b');
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        CREATE TABLE IF NOT EXISTS check_canary (id int);
        """;

    // Made by ScanTests.

    /// <summary>
    /// The foreign table <c>scan_foreign</c>, whose <c>id</c> is NOT NULL, over <c>orders</c> of the same database.
    /// </summary>
    public static string ScanForeign(NpgsqlConnectionStringBuilder connection) =>
        $$"""
            {{ForeignServer(connection)}}
            CREATE FOREIGN TABLE IF NOT EXISTS scan_foreign (id bigint NOT NULL, note text)
                SERVER scratch_loop OPTIONS (table_name 'orders');
            """;

    // Made by DescribeCommandTests.

    /// <summary>The role of the log test, a word that nothing else of a run writes.</summary>
    public const string LogRoleName = "sqlsrc_wombat";

    /// <summary>The database of the log test, a word that nothing else of a run writes.</summary>
    public const string LogDatabaseName = "sqlsrc_numbat";

    /// <summary>
    /// The role <see cref="LogRoleName"/>, which logs in with the password <see cref="Secrets.Marker"/>.  CREATE ROLE
    /// has no IF NOT EXISTS, so a DO block asks the catalog first.
    /// </summary>
    public const string LogRole = $$"""
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = '{{LogRoleName}}') THEN
                CREATE ROLE {{LogRoleName}} LOGIN PASSWORD '{{Secrets.Marker}}';
            END IF;
        END
        $$;
        """;

    /// <summary>
    /// The empty database <see cref="LogDatabaseName"/>, owned by <see cref="LogRoleName"/>, made after
    /// <see cref="LogRole"/>.  CREATE DATABASE has no IF NOT EXISTS and cannot run in a DO block, so a test runs this
    /// only when <c>pg_database</c> has no database of the name.
    /// </summary>
    public const string LogDatabase = $"CREATE DATABASE {LogDatabaseName} OWNER {LogRoleName}";
}
