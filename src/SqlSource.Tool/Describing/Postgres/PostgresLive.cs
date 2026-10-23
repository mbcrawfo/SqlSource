using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Schema;
using SqlSource.Tool.Describing.Postgres.Statements;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What runs on a session's connection: the SQL the tool sends, and the function that asks the server each call of
/// the describer.  A function of this class is the <c>live</c> one of its call, and under a replay none runs.
/// </summary>
/// <remarks>
/// Each function answers through <see cref="AnswerAsync{TAnswer}" />: what the server can cause is the answer's
/// error.  A cancellation passes through, and anything else is a bug.
/// <para>
/// The SQL of the catalog calls runs under the user's <c>search_path</c>, so every type and function it names is
/// written with <c>pg_catalog.</c>: a user's domain named <c>text</c> before <c>pg_catalog</c> on the path would
/// otherwise take <c>::text</c>.  Its operators are not (TD-0077), but those of <c>overloads</c> compare only by the
/// exact argument types of an operator of <c>pg_catalog</c>'s.
/// </para>
/// </remarks>
internal sealed class PostgresLive(PostgresConnection connection)
{
    /// <summary>
    /// What <c>prepare</c> answers when <c>pg_prepared_statements</c> does not list the statement that was just
    /// prepared, by its name and its text: the read ran on another backend than the <c>PREPARE</c>, although the two
    /// are in one transaction, which no pooler in session or transaction mode does.
    /// </summary>
    public const string StatementNotListed = "the server does not list the statement it prepared";

    /// <summary>
    /// What <c>prepare</c> answers, beside the SQLSTATE <c>08P01</c>, when a pooler in statement mode refuses the
    /// describe's transaction: such a connection does not keep a session for the calls of a describe.  The text of
    /// the refusal is not kept, since an error of class <c>08</c> holds the describer's own text
    /// (<see cref="ConnectionErrors" />).
    /// </summary>
    public const string TransactionRefused = "the server refused the transaction of a describe";

    // The SQLSTATE of a protocol violation, which PgBouncer gives every error of its own, and the message with which
    // PgBouncer in statement mode refuses a transaction.  PgBouncer does not translate its messages, so the refusal is
    // told from its other errors, a full pool's query_wait_timeout for one, by the message.
    private const string ProtocolViolation = "08P01";
    private const string StatementModeRefusal = "transaction blocks not allowed in statement pooling mode";

    // The settings of a session that change a description, in the order "open" answers them.
    private static readonly string[] SettingNames = ["search_path", "plan_cache_mode", "standard_conforming_strings"];

    // The server's version, then each setting of SettingNames in its order.
    private static readonly string OpenSql =
        "SELECT "
        + string.Join(
            ", ",
            SettingNames.Prepend("server_version").Select(static name => $"pg_catalog.current_setting('{name}')")
        );

    private const string ParameterTypesSql =
        "SELECT parameter_types::pg_catalog.oid[] FROM pg_catalog.pg_prepared_statements "
        + "WHERE name = $1 AND statement = $2";

    // Every relation asked for with its columns, in one query: by OID for "tables", and by schema and name for
    // "relations", whose text is the same but for the FROM and the WHERE of the first CTE, which are in TablesSql and
    // RelationsSql.  The relations are read first, so that what is said of a relation as a whole is computed once for
    // each relation and not once for each of its columns: whether a partitioned table has a foreign table anywhere in
    // its partition tree, which costs a walk of the catalog and is false and never null for any other, and whether
    // the relation has an INSTEAD rule on INSERT, UPDATE or DELETE (an ev_type of '1' is SELECT), never null.  The
    // join to the attributes is a left one, so that a table without a column of its own is answered too, with one row
    // that holds no attribute.
    private const string TableRowsHead = """
        WITH relations AS MATERIALIZED (
            SELECT
                c.oid,
                c.relkind,
                c.relnamespace,
                c.relname,
                c.relhassubclass,
                c.relkind = 'p' AND EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_partition_tree(c.oid) AS t
                    JOIN pg_catalog.pg_class AS f ON f.oid = t.relid
                    WHERE f.relkind = 'f'
                ) AS has_foreign_partition,
                EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_rewrite AS w
                    WHERE w.ev_class = c.oid AND w.ev_type <> '1' AND w.is_instead
                ) AS has_instead_rule
        """;

    private const string TableRowsTail = """
        )
        SELECT
            r.oid,
            r.relkind::pg_catalog.text,
            n.nspname::pg_catalog.text,
            r.relname::pg_catalog.text,
            a.attnum,
            a.attname::pg_catalog.text,
            a.attnotnull,
            a.attidentity::pg_catalog.text,
            a.attgenerated::pg_catalog.text,
            a.atttypid,
            k.convalidated,
            r.relhassubclass,
            r.has_foreign_partition,
            r.has_instead_rule
        FROM relations AS r
        JOIN pg_catalog.pg_namespace AS n ON n.oid = r.relnamespace
        LEFT JOIN pg_catalog.pg_attribute AS a ON a.attrelid = r.oid AND a.attnum > 0 AND NOT a.attisdropped
        LEFT JOIN pg_catalog.pg_constraint AS k
            ON k.conrelid = r.oid AND k.contype = 'n' AND k.conkey = ARRAY[a.attnum]
        ORDER BY r.oid, a.attnum
        """;

    private const string TablesSql = $"""
        {TableRowsHead}
            FROM pg_catalog.pg_class AS c
            WHERE c.oid = ANY ($1)
        {TableRowsTail}
        """;

    // The same rows for the relations a plan scans, found by schema and name: ROWS FROM pairs the two arrays, as it
    // does in PartitionsSql.  unnest(a, b) would do the same, but only unqualified, so a function of the user's could
    // take it.
    private const string RelationsSql = $"""
        {TableRowsHead}
            FROM ROWS FROM (
                pg_catalog.unnest($1::pg_catalog.text[]),
                pg_catalog.unnest($2::pg_catalog.text[])
            ) AS p (schema, name)
            JOIN pg_catalog.pg_namespace AS s ON s.nspname = p.schema
            JOIN pg_catalog.pg_class AS c ON c.relnamespace = s.oid AND c.relname = p.name
        {TableRowsTail}
        """;

    // The ancestors of each relation asked for, nearest first, in one query.  pg_partition_ancestors gives the
    // relation itself first, and nothing for a relation that is no partition.
    private const string PartitionsSql = """
        SELECT
            n.nspname::pg_catalog.text,
            c.relname::pg_catalog.text,
            ARRAY(
                SELECT a.relid::pg_catalog.oid
                FROM pg_catalog.pg_partition_ancestors(c.oid) WITH ORDINALITY AS a (relid, position)
                WHERE a.relid <> c.oid
                ORDER BY a.position
            )
        FROM ROWS FROM (
            pg_catalog.unnest($1::pg_catalog.text[]),
            pg_catalog.unnest($2::pg_catalog.text[])
        ) AS p (schema, name)
        JOIN pg_catalog.pg_namespace AS n ON n.nspname = p.schema
        JOIN pg_catalog.pg_class AS c ON c.relnamespace = n.oid AND c.relname = p.name
        ORDER BY 1, 2
        """;

    // PostgreSQL's FirstNormalObjectId: every object that initdb creates has a lower OID, so an object of pg_catalog at
    // or above it was added after, by a superuser or by an extension made in pg_catalog.
    private const string FirstNormalObjectId = "16384";

    // The names of the functions and of the operators, among the ones asked for, that a schema other than pg_catalog
    // holds, or that pg_catalog holds as an object that is not built in: two queries, one for each, with the names as
    // one positional text[].  Each name is of the collation "C", so that ORDER BY 1 gives ordinal order whatever the
    // database's collation.  The call is made only when the session finds pg_catalog first, and each comparison is of
    // the exact argument types of an operator of pg_catalog's, (name, text) and (oid, oid), so that one is taken: a
    // user's operator of a comparison's exact types beats one of pg_catalog's that needs a cast, wherever it stands on
    // the path.
    private const string OverloadedFunctionsSql = $"""
        SELECT DISTINCT p.proname::pg_catalog.text COLLATE "C"
        FROM pg_catalog.pg_proc AS p
        WHERE p.proname = ANY ($1::pg_catalog.text[])
            AND (
                p.pronamespace <> 'pg_catalog'::pg_catalog.regnamespace::pg_catalog.oid
                OR p.oid >= {FirstNormalObjectId}::pg_catalog.oid
            )
        ORDER BY 1
        """;

    private const string OverloadedOperatorsSql = $"""
        SELECT DISTINCT o.oprname::pg_catalog.text COLLATE "C"
        FROM pg_catalog.pg_operator AS o
        WHERE o.oprname = ANY ($1::pg_catalog.text[])
            AND (
                o.oprnamespace <> 'pg_catalog'::pg_catalog.regnamespace::pg_catalog.oid
                OR o.oid >= {FirstNormalObjectId}::pg_catalog.oid
            )
        ORDER BY 1
        """;

    // The server's spelling of each pair of a type and a modifier, in one query.
    private const string SpellingsSql = """
        SELECT p.oid, p.typmod, pg_catalog.format_type(p.oid, p.typmod)
        FROM ROWS FROM (
            pg_catalog.unnest($1::pg_catalog.oid[]),
            pg_catalog.unnest($2::pg_catalog.int4[])
        ) AS p (oid, typmod)
        ORDER BY 1, 2
        """;

    // The closure of the types asked for, in one query: a recursive term that follows the element of an array, the
    // base of a domain, the subtype of a range and the range of a multirange.  A domain has the category of its
    // base, so an array is a base type of the category A.
    private const string TypesSql = """
        WITH RECURSIVE closure (oid) AS (
            SELECT t.oid FROM pg_catalog.pg_type AS t WHERE t.oid = ANY ($1)
            UNION
            SELECT link.oid
            FROM closure AS c
            JOIN pg_catalog.pg_type AS t ON t.oid = c.oid
            LEFT JOIN pg_catalog.pg_range AS r ON r.rngtypid = t.oid
            LEFT JOIN pg_catalog.pg_range AS m ON m.rngmultitypid = t.oid
            CROSS JOIN LATERAL (
                VALUES
                    (CASE WHEN t.typtype = 'b' AND t.typcategory = 'A' THEN t.typelem END),
                    (CASE WHEN t.typtype = 'd' THEN t.typbasetype END),
                    (r.rngsubtype),
                    (m.rngtypid)
            ) AS link (oid)
            WHERE link.oid IS NOT NULL AND link.oid <> 0
        )
        SELECT
            t.oid,
            t.typname::pg_catalog.text,
            n.nspname::pg_catalog.text,
            t.typtype::pg_catalog.text,
            t.typcategory::pg_catalog.text,
            t.typelem,
            t.typbasetype,
            r.rngsubtype,
            mr.rngsubtype,
            pg_catalog.format_type(t.oid, -1),
            (
                SELECT pg_catalog.array_agg(e.enumlabel::pg_catalog.text ORDER BY e.enumsortorder)
                FROM pg_catalog.pg_enum AS e
                WHERE e.enumtypid = t.oid
            ),
            t.typtypmod
        FROM closure AS c
        JOIN pg_catalog.pg_type AS t ON t.oid = c.oid
        JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace
        LEFT JOIN pg_catalog.pg_range AS r ON r.rngtypid = t.oid
        LEFT JOIN pg_catalog.pg_range AS m ON m.rngmultitypid = t.oid
        LEFT JOIN pg_catalog.pg_range AS mr ON mr.rngtypid = m.rngtypid
        ORDER BY t.oid
        """;

    // One parameter and nothing of a user's: what Npgsql says of a type's name alone.
    private const string ProbeSql = "SELECT $1";

    /// <summary>Asks <c>open</c>.</summary>
    public Task<PostgresOpenAnswer> OpenAsync(CancellationToken token) =>
        AnswerAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(OpenSql, token);
                await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException("The server answered 'open' with no row.");
                }

                return new PostgresOpenAnswer(
                    VersionOf(reader.GetString(0)),
                    PostgresConnection.DriverVersion,
                    [.. SettingNames.Select((name, index) => new PostgresSetting(name, reader.GetString(index + 1)))],
                    null
                );
            },
            ConnectionErrors.AtOpen,
            static error => new PostgresOpenAnswer(null, null, null, error)
        );

    /// <summary>
    /// <c>server_version</c> as a sidecar records it: <c>18.4 (Debian 18.4-1.pgdg13+1)</c> is <c>18.4</c>.
    /// </summary>
    public static string VersionOf(string serverVersion)
    {
        var space = serverVersion.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? serverVersion : serverVersion[..space];
    }

    /// <summary>
    /// Asks <c>prepare</c>, which begins the describe's transaction
    /// (<see cref="PostgresConnection.BeginDescribeAsync" />) that every later call of the describe runs in and
    /// <c>deallocate</c> ends.
    /// </summary>
    /// <remarks>
    /// The one known cause of a read that fails is a connection that the server ended between the <c>PREPARE</c> and
    /// the read, and a lost connection has no position, so nothing is counted against the <c>PREPARE</c>'s text.  If
    /// a call is ever added between the two, this needs revisiting.  The read matches the statement's name and the
    /// text of the <c>PREPARE</c>, which the server lists exactly as it was sent, so that a statement of the same name
    /// that is not this one is never read for it.  A read that finds no row is <see cref="StatementNotListed" />, an
    /// answer and not a fault: it is a backend that is not the one prepared on.  After an answer without an error the
    /// savepoint is moved after the <c>PREPARE</c> (<see cref="PostgresConnection.KeepPreparedAsync" />), so that no
    /// later rollback to it gives up the locks that preparing took.  An answer with an error ends the transaction,
    /// since no <c>deallocate</c> follows it; a cancellation ends nothing, and the transaction dies with the session,
    /// as the statement does.
    /// </remarks>
    public Task<PostgresPrepareAnswer> PrepareAsync(PostgresPrepareRequest asked, CancellationToken token) =>
        AnswerAsync(
            async () =>
            {
                try
                {
                    await connection.BeginDescribeAsync(token);
                }
                catch (NpgsqlException exception) when (RefusedTransaction(exception) is { } refused)
                {
                    return refused;
                }

                PostgresPrepareAnswer answer;
                try
                {
                    answer = await PreparedAsync(asked, token);
                    if (answer.Error is null)
                    {
                        await connection.KeepPreparedAsync(token);
                    }
                }
                catch (NpgsqlException)
                {
                    await connection.EndDescribeAsync();
                    throw;
                }

                if (answer.Error is not null)
                {
                    await connection.EndDescribeAsync();
                }

                return answer;
            },
            ConnectionErrors.Of,
            static error => new PostgresPrepareAnswer(null, error)
        );

    /// <summary>
    /// The answer of <c>prepare</c> when the exception that began the describe's transaction is a pooler's refusal of
    /// it in statement mode, <c>08P01</c> with PgBouncer's message, or null for any other, which
    /// <see cref="ConnectionErrors.Of" /> answers.  Internal for a test.
    /// </summary>
    internal static PostgresPrepareAnswer? RefusedTransaction(NpgsqlException exception) =>
        exception is PostgresException { SqlState: ProtocolViolation, MessageText: StatementModeRefusal }
            ? new PostgresPrepareAnswer(
                null,
                new PostgresServerError(ProtocolViolation, TransactionRefused, null, null, null)
            )
            : null;

    // The PREPARE and the read of the statement's parameter types.
    private async Task<PostgresPrepareAnswer> PreparedAsync(PostgresPrepareRequest asked, CancellationToken token)
    {
        // One of the two texts that hold a user's SQL, with that of "check": the driver is given it as one statement
        // and may not split it.  The read gives it again as a parameter's value, which is never parsed.
        var text = ParameterPlaceholders.PrepareText(asked.Statement, asked.Types, asked.Sql);
        await connection.ExecuteAsOneStatementAsync(text, token);

        await using var read = await connection.CommandAsync(ParameterTypesSql, token);
        _ = read.Parameters.Add(new NpgsqlParameter { Value = asked.Statement });
        _ = read.Parameters.Add(new NpgsqlParameter { Value = text });
        if (await read.ExecuteScalarAsync(token) is not uint[] types)
        {
            return new PostgresPrepareAnswer(null, new PostgresServerError(null, StatementNotListed, null, null, null));
        }

        return new PostgresPrepareAnswer([.. types.Select(static oid => (long)oid)], null);
    }

    /// <summary>
    /// Asks <c>columns</c>: describes the prepared statement through an <c>EXECUTE</c> that is parsed and never run.
    /// </summary>
    /// <remarks>
    /// Under <see cref="CommandBehavior.SchemaOnly" /> the driver sends no <c>Execute</c>.  Without
    /// <see cref="CommandBehavior.KeyInfo" /> it fills the schema from the row description alone and asks the
    /// server nothing more.  The text holds a name the tool made and nothing of a user's.
    /// </remarks>
    public Task<PostgresColumnsAnswer> ColumnsAsync(PostgresColumnsRequest asked, CancellationToken token) =>
        InDescribeAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(ExecuteText(asked), token);
                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, token);
                var columns = new List<PostgresColumnRow>(reader.FieldCount);
                var modifiers = RowDescriptions.ModifiersOf(reader);

                // The async method is the base class's and gives the base type; each item is the driver's own.
                foreach (var column in (await reader.GetColumnSchemaAsync(token)).Cast<NpgsqlDbColumn>())
                {
                    columns.Add(
                        new PostgresColumnRow(
                            columns.Count,
                            column.ColumnName,
                            column.TypeOID,
                            column.TableOID,
                            column.ColumnAttributeNumber ?? 0,
                            column.ColumnSize,
                            column.NumericPrecision,
                            column.NumericScale,
                            modifiers[columns.Count]
                        )
                    );
                }

                return new PostgresColumnsAnswer(columns, null);
            },
            static error => new PostgresColumnsAnswer(null, error),
            token
        );

    /// <summary>Asks <c>tables</c>: one query of the catalog for every table the columns of a statement name.</summary>
    public Task<PostgresTablesAnswer> TablesAsync(PostgresTablesRequest asked, CancellationToken token) =>
        InDescribeAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(TablesSql, token);
                _ = command.Parameters.Add(OidArray(asked.Oids));
                return new PostgresTablesAnswer(await TableRowsAsync(command, token), null);
            },
            static error => new PostgresTablesAnswer(null, error),
            token
        );

    /// <summary>
    /// Asks <c>relations</c>: one query of the catalog for the relations a plan scans, by schema and name, answered
    /// as <c>tables</c> answers.  A relation that does not exist has no row.
    /// </summary>
    public Task<PostgresTablesAnswer> RelationsAsync(PostgresRelationsRequest asked, CancellationToken token) =>
        InDescribeAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(RelationsSql, token);
                AddRelations(command, asked.Relations);
                return new PostgresTablesAnswer(await TableRowsAsync(command, token), null);
            },
            static error => new PostgresTablesAnswer(null, error),
            token
        );

    // The OIDs as one positional oid[].
    private static NpgsqlParameter OidArray(IEnumerable<long> oids) =>
        new() { Value = oids.Select(static oid => (uint)oid).ToArray(), DataTypeName = "oid[]" };

    // The schemas and the names of the relations as two positional text[], $1 and $2, which ROWS FROM pairs.
    private static void AddRelations(NpgsqlCommand command, IReadOnlyList<PostgresRelationName> relations)
    {
        _ = command.Parameters.Add(
            new NpgsqlParameter
            {
                Value = relations.Select(static relation => relation.Schema).ToArray(),
                DataTypeName = "text[]",
            }
        );
        _ = command.Parameters.Add(
            new NpgsqlParameter
            {
                Value = relations.Select(static relation => relation.Name).ToArray(),
                DataTypeName = "text[]",
            }
        );
    }

    // The rows of TablesSql and of RelationsSql: a table for each run of rows of one OID, with its columns.
    private static async Task<List<PostgresTableRow>> TableRowsAsync(NpgsqlCommand command, CancellationToken token)
    {
        await using var reader = await command.ExecuteReaderAsync(token);
        var tables = new List<PostgresTableRow>();
        List<PostgresAttributeRow> columns = [];
        while (await reader.ReadAsync(token))
        {
            long oid = await reader.GetFieldValueAsync<uint>(0, token);
            if (tables.Count == 0 || tables[^1].Oid != oid)
            {
                columns = [];
                tables.Add(
                    new PostgresTableRow(
                        oid,
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        columns,
                        reader.GetBoolean(11),
                        reader.GetBoolean(12),
                        reader.GetBoolean(13)
                    )
                );
            }

            // The one row of a table without a column of its own holds no attribute.
            if (!await reader.IsDBNullAsync(4, token))
            {
                columns.Add(
                    new PostgresAttributeRow(
                        reader.GetInt16(4),
                        reader.GetString(5),
                        reader.GetBoolean(6),
                        reader.GetString(7),
                        reader.GetString(8),
                        await reader.GetFieldValueAsync<uint>(9, token),
                        await reader.IsDBNullAsync(10, token) ? null : reader.GetBoolean(10)
                    )
                );
            }
        }

        return tables;
    }

    /// <summary>Asks <c>partitions</c>: one query of the catalog for the ancestors of each relation.</summary>
    public Task<PostgresPartitionsAnswer> PartitionsAsync(PostgresPartitionsRequest asked, CancellationToken token) =>
        InDescribeAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(PartitionsSql, token);
                AddRelations(command, asked.Relations);
                await using var reader = await command.ExecuteReaderAsync(token);
                var rows = new List<PostgresPartitionRow>();
                while (await reader.ReadAsync(token))
                {
                    var ancestors = await reader.GetFieldValueAsync<uint[]>(2, token);
                    rows.Add(
                        new PostgresPartitionRow(
                            reader.GetString(0),
                            reader.GetString(1),
                            [.. ancestors.Select(static oid => (long)oid)]
                        )
                    );
                }

                return new PostgresPartitionsAnswer(rows, null);
            },
            static error => new PostgresPartitionsAnswer(null, error),
            token
        );

    /// <summary>
    /// Asks <c>overloads</c>: which of the function names and of the operator names asked for a schema other than
    /// <c>pg_catalog</c> holds, or <c>pg_catalog</c> holds as an object that is not built in, whatever its arguments
    /// and whether or not the session's search path names it.
    /// </summary>
    public Task<PostgresOverloadsAnswer> OverloadsAsync(PostgresOverloadsRequest asked, CancellationToken token) =>
        AnswerAsync(
            async () =>
            {
                var functions = await NamesAsync(OverloadedFunctionsSql, asked.Functions, token);
                var operators = await NamesAsync(OverloadedOperatorsSql, asked.Operators, token);
                return new PostgresOverloadsAnswer(functions, operators, null);
            },
            ConnectionErrors.Of,
            static error => new PostgresOverloadsAnswer(null, null, error)
        );

    // The one text column of a query that takes the names as its $1.
    private async Task<List<string>> NamesAsync(string sql, IReadOnlyList<string> names, CancellationToken token)
    {
        await using var command = await connection.CommandAsync(sql, token);
        _ = command.Parameters.Add(new NpgsqlParameter { Value = names.ToArray(), DataTypeName = "text[]" });
        await using var reader = await command.ExecuteReaderAsync(token);
        var held = new List<string>();
        while (await reader.ReadAsync(token))
        {
            held.Add(reader.GetString(0));
        }

        return held;
    }

    /// <summary>
    /// The text <c>columns</c> sends: the statement with a <c>NULL</c> for each parameter.  The server describes an
    /// <c>EXECUTE</c> whatever the number of its arguments, so a statement with a <c>$n</c> of its own is described
    /// too.
    /// </summary>
    public static string ExecuteText(PostgresColumnsRequest asked) => ExecuteText(asked.Statement, asked.Parameters);

    private static string ExecuteText(string statement, int parameters) =>
        parameters == 0
            ? $"EXECUTE {statement}"
            : $"EXECUTE {statement}({string.Join(", ", Enumerable.Repeat("NULL", parameters))})";

    /// <summary>Asks <c>types</c>.</summary>
    public Task<PostgresTypesAnswer> TypesAsync(PostgresTypesRequest asked, CancellationToken token) =>
        InDescribeAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(TypesSql, token);
                _ = command.Parameters.Add(OidArray(asked.Oids));
                await using var reader = await command.ExecuteReaderAsync(token);
                var rows = new List<PostgresTypeRow>();
                while (await reader.ReadAsync(token))
                {
                    rows.Add(
                        new PostgresTypeRow(
                            await reader.GetFieldValueAsync<uint>(0, token),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            await reader.GetFieldValueAsync<uint>(5, token),
                            await reader.GetFieldValueAsync<uint>(6, token),
                            await reader.IsDBNullAsync(7, token)
                                ? null
                                : await reader.GetFieldValueAsync<uint>(7, token),
                            await reader.IsDBNullAsync(8, token)
                                ? null
                                : await reader.GetFieldValueAsync<uint>(8, token),
                            reader.GetString(9),
                            await reader.IsDBNullAsync(10, token)
                                ? null
                                : await reader.GetFieldValueAsync<string[]>(10, token),
                            reader.GetInt32(11)
                        )
                    );
                }

                return new PostgresTypesAnswer(rows, null);
            },
            static error => new PostgresTypesAnswer(null, error),
            token
        );

    /// <summary>Asks <c>spellings</c>: the server's own spelling of each type with each modifier.</summary>
    public Task<PostgresSpellingsAnswer> SpellingsAsync(PostgresSpellingsRequest asked, CancellationToken token) =>
        InDescribeAsync(
            async () =>
            {
                await using var command = await connection.CommandAsync(SpellingsSql, token);
                _ = command.Parameters.Add(OidArray(asked.Pairs.Select(static pair => pair.Oid)));
                _ = command.Parameters.Add(
                    new NpgsqlParameter
                    {
                        Value = asked.Pairs.Select(static pair => pair.Modifier).ToArray(),
                        DataTypeName = "int4[]",
                    }
                );
                await using var reader = await command.ExecuteReaderAsync(token);
                var rows = new List<PostgresSpellingRow>();
                while (await reader.ReadAsync(token))
                {
                    rows.Add(
                        new PostgresSpellingRow(
                            await reader.GetFieldValueAsync<uint>(0, token),
                            reader.GetInt32(1),
                            reader.GetString(2)
                        )
                    );
                }

                return new PostgresSpellingsAnswer(rows, null);
            },
            static error => new PostgresSpellingsAnswer(null, error),
            token
        );

    /// <summary>
    /// Asks <c>check</c>: describes the SQL as generated code will send it, each parameter named as the query's
    /// list names it and bound by the name of its type, and runs nothing.
    /// </summary>
    /// <remarks>
    /// The text is a user's, so it goes as a batch of one command.  Npgsql refuses two things before it sends
    /// anything: a type name it cannot bind, and a text in which it finds a <c>;</c> that something follows.  It
    /// throws a <see cref="NotSupportedException" />, an <see cref="InvalidCastException" /> or, for a name that is
    /// too long, an <see cref="ArgumentException" />, and the answer is the same for each.  Which of the two
    /// refusals it was is found by asking for each parameter alone, with a text of the tool's own.  With the columns,
    /// the answer holds the text Npgsql sent and the order of its parameters (<see cref="BatchRewrites" />).
    /// </remarks>
    public Task<PostgresCheckAnswer> CheckAsync(PostgresCheckRequest asked, CancellationToken token) =>
        InDescribeAsync(
            async () =>
            {
                try
                {
                    await using var batch = await connection.BatchOfOneAsync(asked.Sql, token);
                    foreach (var parameter in asked.Parameters)
                    {
                        _ = batch.BatchCommands[0].Parameters.Add(Unvalued(parameter.Name, parameter.DataTypeName));
                    }

                    await using var reader = await batch.ExecuteReaderAsync(CommandBehavior.SchemaOnly, token);
                    var columns = new List<PostgresCheckColumn>(reader.FieldCount);
                    for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                    {
                        columns.Add(
                            new PostgresCheckColumn(
                                reader.GetName(ordinal),
                                reader.GetDataTypeOID(ordinal),
                                reader.GetDataTypeName(ordinal)
                            )
                        );
                    }

                    return new PostgresCheckAnswer(columns, null, null, BatchRewrites.Of(batch.BatchCommands[0]));
                }
                catch (Exception refused) when (IsDriverRefusal(refused))
                {
                    return await RefusedAsync(asked, refused.Message, token);
                }
            },
            CheckFailed,
            token
        );

    // The answer of a check that failed on the server: no columns, no bind error, and a text that was not refused.
    private static PostgresCheckAnswer CheckFailed(PostgresServerError error) => new(null, error, null, null, false);

    // The driver refused and sent nothing.  The first parameter whose type it refuses alone is the one that cannot
    // be bound; when it refuses none, it refused the text, and its message is the answer's error.
    private Task<PostgresCheckAnswer> RefusedAsync(
        PostgresCheckRequest asked,
        string message,
        CancellationToken token
    ) =>
        InDescribeAsync(
            async () =>
            {
                foreach (var parameter in asked.Parameters)
                {
                    if (await RefusalOfAsync(parameter.DataTypeName, token) is { } refusal)
                    {
                        return new PostgresCheckAnswer(null, null, new PostgresBindError(parameter.Name, refusal));
                    }
                }

                return new PostgresCheckAnswer(
                    null,
                    new PostgresServerError(null, message, null, null, null),
                    null,
                    null,
                    true
                );
            },
            CheckFailed,
            token
        );

    // What Npgsql says of a parameter of this type and no other, or null when it binds it.
    private async Task<string?> RefusalOfAsync(string dataTypeName, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(ProbeSql, token);
            _ = command.Parameters.Add(Unvalued(string.Empty, dataTypeName));
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, token);
            return null;
        }
        catch (Exception refused) when (IsDriverRefusal(refused))
        {
            return refused.Message;
        }
    }

    // What the driver throws when it refuses a command before it sends anything: a type name it cannot bind, a text
    // with a ";" that something follows, a name that is too long.  An NpgsqlException is none of these.
    private static bool IsDriverRefusal(Exception exception) =>
        exception is NotSupportedException or InvalidCastException or ArgumentException;

    // A parameter with a type's name and no value, as a describe needs one.
    private static NpgsqlParameter Unvalued(string name, string dataTypeName) =>
        new()
        {
            ParameterName = name,
            Value = DBNull.Value,
            DataTypeName = dataTypeName,
        };

    private const string ForceGenericPlanSql = "SET LOCAL plan_cache_mode = force_generic_plan";

    private const string NoPartitionPruningSql = "SET LOCAL enable_partition_pruning = off";

    /// <summary>
    /// Asks <c>explain</c>: the generic plan of the prepared statement, under the describe's savepoint, which it rolls
    /// back to afterwards.
    /// </summary>
    /// <remarks>
    /// <c>EXPLAIN</c> without <c>ANALYZE</c> plans the statement and does not execute it.  Planning evaluates an
    /// immutable function whose arguments are constants, inside the describe's transaction, which is rolled back: a
    /// function wrongly marked immutable runs, and an effect outside the transaction, a sequence's for one, remains
    /// (TD-0047).  Under <c>force_generic_plan</c> the <c>NULL</c> of a parameter is not folded into the plan, so
    /// <c>id = $1</c> stays a condition.  Partition pruning is off: the executor would prune every partition for the
    /// <c>NULL</c> of a parameter when it starts, and the plan would show an <c>Append</c> with no member, from
    /// which the walk cannot tell a partitioned table from a set operation (TD-0052).  Each setting is a
    /// <c>SET LOCAL</c>, which the rollback to the savepoint ends, after the plan or, through
    /// <see cref="InDescribeAsync{TAnswer}" />, after a failure at any step, so no setting reaches a later call.  The
    /// texts hold a name the tool made, settings of a closed list and nothing of a user's.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No describe's transaction is held: outside one, a <c>SET LOCAL</c> would change nothing.
    /// </exception>
    public Task<PostgresExplainAnswer> ExplainAsync(PostgresExplainRequest asked, CancellationToken token)
    {
        if (!connection.InDescribe)
        {
            throw new InvalidOperationException("'explain' is asked within a describe's transaction only.");
        }

        return InDescribeAsync(
            async () =>
            {
                foreach (var text in SetTexts(asked))
                {
                    await using var set = await connection.CommandAsync(text, token);
                    _ = await set.ExecuteNonQueryAsync(token);
                }

                string? plan;
                await using (var command = await connection.CommandAsync(ExplainText(asked), token))
                {
                    plan = await command.ExecuteScalarAsync(token) as string;
                }

                await connection.RestoreDescribeAsync(token);

                // An answer that is no text is no plan, which the session reads as unreadable: never a failed describe.
                return new PostgresExplainAnswer(plan, null);
            },
            static error => new PostgresExplainAnswer(null, error),
            token
        );
    }

    /// <summary>The text <c>explain</c> sends for the plan.</summary>
    public static string ExplainText(PostgresExplainRequest asked) =>
        "EXPLAIN (VERBOSE, FORMAT JSON) " + ExecuteText(asked.Statement, asked.Parameters);

    /// <summary>
    /// What <c>explain</c> sets for its transaction, in order: the generic plan, no partition pruning, then each
    /// setting.
    /// </summary>
    /// <exception cref="ArgumentException">A setting is not of <see cref="PlannerSettings" />.</exception>
    public static IReadOnlyList<string> SetTexts(PostgresExplainRequest asked) =>
        [ForceGenericPlanSql, NoPartitionPruningSql, .. asked.Settings.Select(PlannerSettings.SetText)];

    /// <summary>
    /// Asks <c>deallocate</c>: frees the statement inside the describe's transaction, on the backend that prepared it,
    /// then ends the transaction, whether the <c>DEALLOCATE</c> answered or failed.  A cancellation ends nothing: the
    /// transaction dies with the session, which the run closes next.
    /// </summary>
    public Task<PostgresDeallocateAnswer> DeallocateAsync(PostgresDeallocateRequest asked, CancellationToken token) =>
        AnswerAsync(
            async () =>
            {
                try
                {
                    await using var command = await connection.CommandAsync($"DEALLOCATE {asked.Statement}", token);
                    _ = await command.ExecuteNonQueryAsync(token);
                }
                catch (NpgsqlException)
                {
                    await connection.EndDescribeAsync();
                    throw;
                }

                await connection.EndDescribeAsync();
                return new PostgresDeallocateAnswer(null);
            },
            ConnectionErrors.Of,
            static error => new PostgresDeallocateAnswer(error)
        );

    /// <summary>
    /// Runs a call of a describe through <see cref="AnswerAsync{TAnswer}" />, with <see cref="ConnectionErrors.Of" />.
    /// An error of the server aborts the describe's transaction, so the transaction is first rolled back to its
    /// savepoint: every later call then runs as it would on its own, and <c>deallocate</c> can free the statement.
    /// An error of that rollback is dropped, since the call's own error is the answer and a connection that was lost
    /// answers the next call.  A call outside a describe's transaction, as a test makes one, restores nothing.
    /// </summary>
    private Task<TAnswer> InDescribeAsync<TAnswer>(
        Func<Task<TAnswer>> call,
        Func<PostgresServerError, TAnswer> failed,
        CancellationToken token
    ) =>
        AnswerAsync(
            async () =>
            {
                try
                {
                    return await call();
                }
                catch (NpgsqlException) when (connection.InDescribe)
                {
                    await RestoreAfterFailureAsync(token);
                    throw;
                }
            },
            ConnectionErrors.Of,
            failed
        );

    private async Task RestoreAfterFailureAsync(CancellationToken token)
    {
        try
        {
            await connection.RestoreDescribeAsync(token);
        }
        catch (NpgsqlException)
        {
            // The call's own error is the answer; a connection that was lost answers the next call too.
        }
    }

    /// <summary>
    /// Runs a call and gives its answer, or the answer of the error the server, Npgsql or the connection gave.  A
    /// cancellation passes through, and so does anything but an <see cref="NpgsqlException" />, which is a bug.
    /// Internal for a test.
    /// </summary>
    /// <param name="call">What the call runs.</param>
    /// <param name="errorOf">
    /// <see cref="ConnectionErrors.AtOpen" /> for <c>open</c> and <see cref="ConnectionErrors.Of" /> for every other
    /// call.
    /// </param>
    /// <param name="failed">The call's answer that holds an error.</param>
    internal static async Task<TAnswer> AnswerAsync<TAnswer>(
        Func<Task<TAnswer>> call,
        Func<NpgsqlException, PostgresServerError> errorOf,
        Func<PostgresServerError, TAnswer> failed
    )
    {
        try
        {
            return await call();
        }
        catch (NpgsqlException exception)
        {
            return failed(errorOf(exception));
        }
    }
}
