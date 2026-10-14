using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Schema;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What runs on a session's connection: the SQL the tool sends, and the function that asks the server each call of
/// the describer.  A function of this class is the <c>live</c> one of its call, and under a replay none runs.
/// </summary>
/// <remarks>
/// In each function, what the server can cause is the answer's error.  A cancellation passes through, and
/// anything else is a bug.
/// </remarks>
internal sealed class PostgresLive(PostgresConnection connection)
{
    // The settings of a session that change a description, in the order "open" answers them.
    private static readonly string[] SettingNames = ["search_path", "plan_cache_mode", "standard_conforming_strings"];

    private const string OpenSql =
        "SELECT pg_catalog.current_setting('server_version'), pg_catalog.current_setting('search_path'), "
        + "pg_catalog.current_setting('plan_cache_mode'), pg_catalog.current_setting('standard_conforming_strings')";

    private const string ParameterTypesSql =
        "SELECT parameter_types::oid[] FROM pg_catalog.pg_prepared_statements WHERE name = @name";

    // Every relation asked for with its columns, in one query.  The relations are read first, so that what is said of
    // a relation as a whole is computed once for each relation and not once for each of its columns: whether a
    // partitioned table has a foreign table anywhere in its partition tree, which costs a walk of the catalog and is
    // false and never null for any other, and whether the relation has an INSTEAD rule on INSERT, UPDATE or DELETE
    // (an ev_type of '1' is SELECT), never null.  The join to the attributes is a left one, so that a table without
    // a column of its own is answered too, with one row that holds no attribute.
    private const string TablesSql = """
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
            FROM pg_catalog.pg_class AS c
            WHERE c.oid = ANY (@oids)
        )
        SELECT
            r.oid,
            r.relkind::text,
            n.nspname::text,
            r.relname::text,
            a.attnum,
            a.attname::text,
            a.attnotnull,
            a.attidentity::text,
            a.attgenerated::text,
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

    // The closure of the types asked for, in one query: a recursive term that follows the element of an array, the
    // base of a domain, the subtype of a range and the range of a multirange.  A domain has the category of its
    // base, so an array is a base type of the category A.
    private const string TypesSql = """
        WITH RECURSIVE closure (oid) AS (
            SELECT t.oid FROM pg_catalog.pg_type AS t WHERE t.oid = ANY (@oids)
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
            t.typname::text,
            n.nspname::text,
            t.typtype::text,
            t.typcategory::text,
            t.typelem,
            t.typbasetype,
            r.rngsubtype,
            mr.rngsubtype,
            pg_catalog.format_type(t.oid, -1),
            (
                SELECT pg_catalog.array_agg(e.enumlabel::text ORDER BY e.enumsortorder)
                FROM pg_catalog.pg_enum AS e
                WHERE e.enumtypid = t.oid
            )
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
    public async Task<PostgresOpenAnswer> OpenAsync(CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(OpenSql, token);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                throw new InvalidOperationException("The server answered 'open' with no row.");
            }

            return new PostgresOpenAnswer(
                PostgresSession.VersionOf(reader.GetString(0)),
                PostgresConnection.DriverVersion,
                [.. SettingNames.Select((name, index) => new PostgresSetting(name, reader.GetString(index + 1)))],
                null
            );
        }
        catch (NpgsqlException exception)
        {
            return new PostgresOpenAnswer(null, null, null, PostgresConnection.ErrorOf(exception));
        }
    }

    /// <summary>Asks <c>prepare</c>.</summary>
    public async Task<PostgresPrepareAnswer> PrepareAsync(PostgresPrepareRequest asked, CancellationToken token)
    {
        try
        {
            // The one text that holds a user's SQL: the driver is given it as one statement and may not split it.
            var text = ParameterPlaceholders.PrepareText(asked.Statement, asked.Types, asked.Sql);
            await connection.ExecuteAsOneStatementAsync(text, token);

            await using var read = await connection.CommandAsync(ParameterTypesSql, token);
            _ = read.Parameters.AddWithValue("name", asked.Statement);
            var types =
                await read.ExecuteScalarAsync(token) as uint[]
                ?? throw new InvalidOperationException($"The server lists no prepared statement '{asked.Statement}'.");
            return new PostgresPrepareAnswer([.. types.Select(static oid => (long)oid)], null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresPrepareAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    /// <summary>
    /// Asks <c>columns</c>: describes the prepared statement through an <c>EXECUTE</c> that is parsed and never run.
    /// </summary>
    /// <remarks>
    /// Under <see cref="CommandBehavior.SchemaOnly" /> the driver sends no <c>Execute</c>.  Without
    /// <see cref="CommandBehavior.KeyInfo" /> it fills the schema from the row description alone and asks the
    /// server nothing more.  The text holds a name the tool made and nothing of a user's.
    /// </remarks>
    public async Task<PostgresColumnsAnswer> ColumnsAsync(PostgresColumnsRequest asked, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(ExecuteText(asked), token);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, token);
            var columns = new List<PostgresColumnRow>(reader.FieldCount);

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
                        column.NumericScale
                    )
                );
            }

            return new PostgresColumnsAnswer(columns, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresColumnsAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    /// <summary>Asks <c>tables</c>: one query of the catalog for every table the columns of a statement name.</summary>
    public async Task<PostgresTablesAnswer> TablesAsync(PostgresTablesRequest asked, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(TablesSql, token);
            _ = command.Parameters.Add(
                new NpgsqlParameter("oids", asked.Oids.Select(static oid => (uint)oid).ToArray())
                {
                    DataTypeName = "oid[]",
                }
            );
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

            return new PostgresTablesAnswer(tables, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresTablesAnswer(null, PostgresConnection.ErrorOf(exception));
        }
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
    public async Task<PostgresTypesAnswer> TypesAsync(PostgresTypesRequest asked, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(TypesSql, token);
            _ = command.Parameters.Add(
                new NpgsqlParameter("oids", asked.Oids.Select(static oid => (uint)oid).ToArray())
                {
                    DataTypeName = "oid[]",
                }
            );
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
                        await reader.IsDBNullAsync(7, token) ? null : await reader.GetFieldValueAsync<uint>(7, token),
                        await reader.IsDBNullAsync(8, token) ? null : await reader.GetFieldValueAsync<uint>(8, token),
                        reader.GetString(9),
                        await reader.IsDBNullAsync(10, token)
                            ? null
                            : await reader.GetFieldValueAsync<string[]>(10, token)
                    )
                );
            }

            return new PostgresTypesAnswer(rows, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresTypesAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    /// <summary>
    /// Asks <c>check</c>: describes the SQL as generated code will send it, each parameter named as the query's
    /// list names it and bound by the name of its type, and runs nothing.
    /// </summary>
    /// <remarks>
    /// The text is a user's, so it goes as a batch of one command.  Npgsql refuses two things before it sends
    /// anything: a type name it cannot bind, and a text in which it finds a <c>;</c> that something follows.  It
    /// throws a <see cref="NotSupportedException" />, an <see cref="InvalidCastException" /> or, for a name that is
    /// too long, an <see cref="ArgumentException" />, and the answer is the same for each.  Which of the two
    /// refusals it was is found by asking for each parameter alone, with a text of the tool's own.
    /// </remarks>
    public async Task<PostgresCheckAnswer> CheckAsync(PostgresCheckRequest asked, CancellationToken token)
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

            return new PostgresCheckAnswer(columns, null, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresCheckAnswer(null, PostgresConnection.ErrorOf(exception), null);
        }
        catch (NotSupportedException refused)
        {
            return await RefusedAsync(asked, refused.Message, token);
        }
        catch (InvalidCastException refused)
        {
            return await RefusedAsync(asked, refused.Message, token);
        }
        catch (ArgumentException refused)
        {
            return await RefusedAsync(asked, refused.Message, token);
        }
    }

    // The driver refused and sent nothing.  The first parameter whose type it refuses alone is the one that cannot
    // be bound; when it refuses none, it refused the text, and its message is the answer's error.
    private async Task<PostgresCheckAnswer> RefusedAsync(
        PostgresCheckRequest asked,
        string message,
        CancellationToken token
    )
    {
        try
        {
            foreach (var parameter in asked.Parameters)
            {
                if (await RefusalOfAsync(parameter.DataTypeName, token) is { } refusal)
                {
                    return new PostgresCheckAnswer(null, null, new PostgresBindError(parameter.Name, refusal));
                }
            }

            return new PostgresCheckAnswer(null, new PostgresServerError(null, message, null, null, null), null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresCheckAnswer(null, PostgresConnection.ErrorOf(exception), null);
        }
    }

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
        catch (NotSupportedException refused)
        {
            return refused.Message;
        }
        catch (InvalidCastException refused)
        {
            return refused.Message;
        }
        catch (ArgumentException refused)
        {
            return refused.Message;
        }
    }

    // A parameter with a type's name and no value, as a describe needs one.
    private static NpgsqlParameter Unvalued(string name, string dataTypeName) =>
        new()
        {
            ParameterName = name,
            Value = DBNull.Value,
            DataTypeName = dataTypeName,
        };

    private const string ForceGenericPlanSql = "SET LOCAL plan_cache_mode = force_generic_plan";

    /// <summary>
    /// Asks <c>explain</c>: the generic plan of the prepared statement, in a transaction that is rolled back.
    /// </summary>
    /// <remarks>
    /// <c>EXPLAIN</c> without <c>ANALYZE</c> plans the statement and does not execute it.  Planning evaluates an
    /// immutable function whose arguments are constants, inside the transaction that is rolled back: a function
    /// wrongly marked immutable runs, and an effect outside the transaction, a sequence's for one, remains
    /// (TD-0047).  Under <c>force_generic_plan</c> the <c>NULL</c> of a parameter is not folded into the plan, so
    /// <c>id = $1</c> stays a condition.  Disposing a transaction that was not ended rolls it back, so a failure at
    /// any step leaves no setting behind.  The texts hold a name the tool made, settings of a closed list and
    /// nothing of a user's.
    /// </remarks>
    public async Task<PostgresExplainAnswer> ExplainAsync(PostgresExplainRequest asked, CancellationToken token)
    {
        try
        {
            await using var transaction = await connection.BeginAsync(token);
            foreach (var text in SetTexts(asked))
            {
                await using var set = await connection.CommandAsync(text, token);
                _ = await set.ExecuteNonQueryAsync(token);
            }

            await using var command = await connection.CommandAsync(ExplainText(asked), token);
            var plan = await command.ExecuteScalarAsync(token) as string;
            await transaction.RollbackAsync(token);

            // An answer that is no text is no plan, which the session reads as unreadable: never a failed describe.
            return new PostgresExplainAnswer(plan, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresExplainAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    /// <summary>The text <c>explain</c> sends for the plan.</summary>
    public static string ExplainText(PostgresExplainRequest asked) =>
        "EXPLAIN (VERBOSE, FORMAT JSON) " + ExecuteText(asked.Statement, asked.Parameters);

    /// <summary>What <c>explain</c> sets for its transaction, in order: the generic plan, then each setting.</summary>
    /// <exception cref="ArgumentException">A setting is not of <see cref="PlannerSettings" />.</exception>
    public static IReadOnlyList<string> SetTexts(PostgresExplainRequest asked) =>
        [ForceGenericPlanSql, .. asked.Settings.Select(PlannerSettings.SetText)];

    /// <summary>Asks <c>deallocate</c>.</summary>
    public async Task<PostgresDeallocateAnswer> DeallocateAsync(
        PostgresDeallocateRequest asked,
        CancellationToken token
    )
    {
        try
        {
            await using var command = await connection.CommandAsync($"DEALLOCATE {asked.Statement}", token);
            _ = await command.ExecuteNonQueryAsync(token);
            return new PostgresDeallocateAnswer(null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresDeallocateAnswer(PostgresConnection.ErrorOf(exception));
        }
    }
}
