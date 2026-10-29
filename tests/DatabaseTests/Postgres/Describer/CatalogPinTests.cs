using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// What the describer's tables of pg_catalog hold, pinned against a real server's catalog: the families of casts of
// SetOperationTypes, the built-in types, the total functions and aggregates, the overloads and the types that carry a
// modifier.  A row the server does not have, or one it has that a table leaves out, fails on that server.  Split from
// PlanOutputTests, which holds what a plan prints.
[Collection(UsesPostgres.Name)]
public sealed class CatalogPinTests(PostgresDatabases postgres)
{
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

        expected.Count.ShouldBe(27);
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

    // Every overload pg_catalog holds of each function name CatalogOverloads reads is a row of the table, with the
    // arguments and the implementation the row gives, and every row is one: an aggregate by its transition function,
    // its final one and its initial value.  None is strict, which would make it NULL for a NULL argument before its C
    // function ran, and none returns a set.  Each aggregate's functions of a moving window and of a plan in parts are
    // the ones CatalogOverloads' remarks name.  The intersection of two of each range type is of that type, no implicit
    // or assignment cast leads from one, and the only operators * of pg_catalog over a range, a multirange or a
    // pseudo-type are range_intersect and the multirange's.  Every mismatch is listed.
    [Fact]
    public async Task Catalog_CatalogOverloads_ArePinned()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        var token = TestContext.Current.CancellationToken;
        var found = new HashSet<(string Name, string Arguments, string Implementation)>();
        var mismatches = new List<string>();
        await using (
            var command = source.CreateCommand(
                """
                SELECT p.proname::text,
                    pg_catalog.pg_get_function_identity_arguments(p.oid),
                    CASE
                        WHEN p.prokind = 'a' THEN a.aggtransfn::text || '/'
                            || CASE WHEN a.aggfinalfn = 0 THEN '-' ELSE a.aggfinalfn::text END || '/'
                            || coalesce(a.agginitval, '')
                        ELSE p.prosrc
                    END,
                    p.proisstrict OR p.proretset
                FROM pg_catalog.pg_proc AS p
                LEFT JOIN pg_catalog.pg_aggregate AS a ON a.aggfnoid = p.oid
                WHERE p.pronamespace = 'pg_catalog'::regnamespace AND p.proname = ANY ($1)
                """
            )
        )
        {
            _ = command.Parameters.Add(new NpgsqlParameter { Value = CatalogOverloads.FunctionNames.ToArray() });
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var row = (reader.GetString(0), reader.GetString(1), reader.GetString(2));
                _ = found.Add(row);
                if (reader.GetBoolean(3))
                {
                    mismatches.Add($"strict or returning a set {Describe(row)}");
                }
            }
        }

        var listed = CatalogOverloads.Overloads.ToHashSet();
        mismatches.AddRange(listed.Except(found).Select(static row => $"missing {Describe(row)}"));
        mismatches.AddRange(found.Except(listed).Select(static row => $"not listed {Describe(row)}"));

        // The functions of the moving and the partial modes, aggcombinefn/aggserialfn/aggdeserialfn/aggmtransfn/
        // aggminvtransfn/aggmfinalfn/aggminitval: array_agg is computed in parts from 16.
        var parts = postgres.Major >= 16;
        var support = new HashSet<(string Name, string Arguments, string Implementation)>
        {
            ("count", "", "int8pl/-/-/int8inc/int8dec/-/0"),
            ("count", "\"any\"", "int8pl/-/-/int8inc_any/int8dec_any/-/0"),
            (
                "array_agg",
                "anynonarray",
                parts ? "array_agg_combine/array_agg_serialize/array_agg_deserialize/-/-/-/" : "-/-/-/-/-/-/"
            ),
            (
                "array_agg",
                "anyarray",
                parts
                    ? "array_agg_array_combine/array_agg_array_serialize/array_agg_array_deserialize/-/-/-/"
                    : "-/-/-/-/-/-/"
            ),
        };
        var foundSupport = new HashSet<(string Name, string Arguments, string Implementation)>();
        await using (
            var command = source.CreateCommand(
                """
                SELECT p.proname::text,
                    pg_catalog.pg_get_function_identity_arguments(p.oid),
                    a.aggcombinefn::text || '/' || a.aggserialfn::text || '/' || a.aggdeserialfn::text || '/'
                        || a.aggmtransfn::text || '/' || a.aggminvtransfn::text || '/' || a.aggmfinalfn::text || '/'
                        || coalesce(a.aggminitval, '')
                FROM pg_catalog.pg_proc AS p
                JOIN pg_catalog.pg_aggregate AS a ON a.aggfnoid = p.oid
                WHERE p.pronamespace = 'pg_catalog'::regnamespace AND p.proname = ANY ($1)
                """
            )
        )
        {
            _ = command.Parameters.Add(new NpgsqlParameter { Value = CatalogOverloads.FunctionNames.ToArray() });
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                _ = foundSupport.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        mismatches.AddRange(support.Except(foundSupport).Select(static row => $"missing modes {Describe(row)}"));
        mismatches.AddRange(foundSupport.Except(support).Select(static row => $"other modes {Describe(row)}"));

        var ranges = CatalogOverloads
            .FunctionNames.Where(static name => CatalogOverloads.RangeTypeOf(name) is not null)
            .ToArray();
        foreach (var range in ranges)
        {
            await using var command = source.CreateCommand(
                $"SELECT pg_catalog.pg_typeof('empty'::{range} * 'empty'::{range})::oid"
            );
            var type = (uint)(await command.ExecuteScalarAsync(token)).ShouldNotBeNull();
            if (type != CatalogOverloads.RangeTypeOf(range))
            {
                mismatches.Add($"{range} * {range} is of the type {type}");
            }
        }

        await using (
            var command = source.CreateCommand(
                """
                SELECT c.castsource::regtype::text || ' to ' || c.casttarget::regtype::text
                FROM pg_catalog.pg_cast AS c
                WHERE c.castsource = ANY ($1) AND c.castcontext IN ('i', 'a')
                UNION ALL
                SELECT 'operator *(' || o.oprleft::regtype::text || ', ' || o.oprright::regtype::text || ') by '
                    || o.oprcode::text
                FROM pg_catalog.pg_operator AS o
                JOIN pg_catalog.pg_type AS l ON l.oid = o.oprleft
                JOIN pg_catalog.pg_type AS r ON r.oid = o.oprright
                WHERE o.oprnamespace = 'pg_catalog'::regnamespace
                    AND o.oprname = '*'
                    AND (l.typtype IN ('r', 'm', 'p') OR r.typtype IN ('r', 'm', 'p'))
                    AND (o.oprleft, o.oprright, o.oprcode::text) NOT IN (
                        ('anyrange'::regtype, 'anyrange'::regtype, 'range_intersect'),
                        ('anymultirange'::regtype, 'anymultirange'::regtype, 'multirange_intersect')
                    )
                """
            )
        )
        {
            _ = command.Parameters.Add(
                new NpgsqlParameter
                {
                    Value = ranges.Select(static range => (uint)CatalogOverloads.RangeTypeOf(range)!.Value).ToArray(),
                    DataTypeName = "oid[]",
                }
            );
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                mismatches.Add(reader.GetString(0));
            }
        }

        mismatches.ShouldBeEmpty();
        listed.Count.ShouldBe(20);
        ranges.Length.ShouldBe(6);

        static string Describe((string Name, string Arguments, string Implementation) row) =>
            $"{row.Name}({row.Arguments}) by {row.Implementation}";
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
}
