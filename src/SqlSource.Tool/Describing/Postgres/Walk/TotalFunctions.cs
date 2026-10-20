using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// The operators and functions of <c>pg_catalog</c> that never return <c>NULL</c> for arguments that are not, and the
/// aggregates that never return <c>NULL</c> over a group of a row whose arguments are not, by their exact argument
/// types, with the type of what each returns.
/// </summary>
/// <remarks>
/// <para>
/// A plan prints an operator or a function bare only when the session would resolve the same one from its name and
/// the argument types it prints, each argument with its implicit cast.  When the session finds <c>pg_catalog</c>
/// first and <c>pg_catalog</c> holds that name with exactly those argument types, the resolution finds that one: an
/// operator or a function whose argument types are the call's exactly is chosen before any that needs a coercion or
/// is polymorphic, and of several with the same argument types the one earliest on the path is.  So a row is looked
/// up by its exact signature alone, and only when the session finds <c>pg_catalog</c> first, which is the caller's to
/// know (<see cref="PostgresSearchPath" />).
/// </para>
/// <para>
/// A row is total, not merely strict: a strict function returns <c>NULL</c> for a <c>NULL</c> argument, and may for
/// others.  Each row was read from the C function that implements it, in the source of PostgreSQL 14 and 18, and the
/// table holds that function's name with the row, as <c>prosrc</c> gives it, for an operator of its <c>oprcode</c>:
/// <c>int2pl</c>, <c>int4mul</c>, <c>int84mi</c>, <c>int28div</c> and the rest of the integer arithmetic,
/// <c>numeric_add</c>, <c>numeric_sub</c>, <c>numeric_mul</c>, <c>numeric_div</c>, <c>float4pl</c>,
/// <c>float48mul</c>, <c>float8div</c> and the rest of the floating point arithmetic; <c>int4eq</c>,
/// <c>int82lt</c>, <c>numeric_eq</c>, <c>float84ge</c>, <c>texteq</c>, <c>text_lt</c>, <c>bpchareq</c>,
/// <c>date_eq</c>, <c>timestamp_lt</c>, which compares a <c>timestamp with time zone</c> too, <c>booleq</c> and the
/// rest of the comparisons; <c>xideq</c>, which compares an <c>xid</c> with an <c>integer</c> too, <c>textcat</c>,
/// <c>lower</c>, <c>upper</c>, <c>textlen</c>, <c>timestamp_trunc</c> and <c>timestamptz_trunc</c>.  Each raises an
/// error where it cannot compute, an overflow or a division by zero, and returns a value otherwise.
/// <c>Catalog_TotalFunctions_ArePgCatalogsOwn</c> in <c>PlanOutputTests</c> holds every row to the catalog, its
/// implementation included.
/// </para>
/// <para>
/// An aggregate is found by the same rule, and the table holds the names of its transition function and its final
/// one, <c>aggtransfn</c> and <c>aggfinalfn</c>, <c>-</c> for none.  Of these the final value is <c>NULL</c> only
/// when no input was not <c>NULL</c>: <c>max</c>, <c>min</c>, <c>bool_and</c>, <c>bool_or</c>, <c>every</c> and
/// <c>sum</c> of a <c>real</c> or a <c>double precision</c> have a strict transition with no initial value,
/// <c>int4larger</c>, <c>text_smaller</c>, <c>booland_statefunc</c>, <c>float8pl</c> and the rest, whose state is the
/// first input that is not <c>NULL</c>; <c>sum</c> of a <c>smallint</c> or an <c>integer</c> has <c>int2_sum</c> and
/// <c>int4_sum</c>, which keep a state of <c>NULL</c> until such an input; and the rest have a final function that
/// is <c>NULL</c> only for a count of no input, <c>numeric_poly_sum</c>, <c>numeric_sum</c>,
/// <c>numeric_poly_avg</c>, <c>numeric_avg</c>, <c>int8_avg</c> and <c>float8_avg</c>, or for a state that saw none,
/// <c>string_agg_finalfn</c>.  A <c>DISTINCT</c> and an <c>ORDER BY</c> inside the call change neither.  So over a
/// group that has a row, whose every input is not <c>NULL</c>, none is <c>NULL</c>.
/// <c>Catalog_TotalAggregates_ArePgCatalogsOwn</c> holds every row to the catalog, its two functions included.
/// </para>
/// </remarks>
internal static class TotalFunctions
{
    /// <summary>The type of a value that is proved not <c>NULL</c> but of no known type: no row takes it.</summary>
    public const long UnknownType = 0;

    // The final function of an aggregate that has none, as aggfinalfn prints it.
    private const string NoFinal = "-";

    // The number types an arithmetic operator takes two of the same.
    private static readonly long[] NumberTypes =
    [
        BuiltInTypes.Int2,
        BuiltInTypes.Int4,
        BuiltInTypes.Int8,
        BuiltInTypes.Numeric,
        BuiltInTypes.Float4,
        BuiltInTypes.Float8,
    ];

    // The integer types an operator takes mixed, the narrowest first.
    private static readonly long[] IntegerTypes = [BuiltInTypes.Int2, BuiltInTypes.Int4, BuiltInTypes.Int8];

    // The types a comparison takes two of the same.
    private static readonly long[] ComparedTypes =
    [
        .. NumberTypes,
        BuiltInTypes.Text,
        BuiltInTypes.Bpchar,
        BuiltInTypes.Date,
        BuiltInTypes.Timestamp,
        BuiltInTypes.Timestamptz,
        BuiltInTypes.Bool,
    ];

    private static readonly (string Name, long Left, long Right, long Result, string Implementation)[] OperatorRows =
    [
        .. BuildOperators(),
    ];

    private static readonly (string Name, long[] Arguments, long Result, string Implementation)[] FunctionRows =
    [
        ("lower", [BuiltInTypes.Text], BuiltInTypes.Text, "lower"),
        ("upper", [BuiltInTypes.Text], BuiltInTypes.Text, "upper"),
        ("length", [BuiltInTypes.Text], BuiltInTypes.Int4, "textlen"),
        ("date_trunc", [BuiltInTypes.Text, BuiltInTypes.Timestamp], BuiltInTypes.Timestamp, "timestamp_trunc"),
        ("date_trunc", [BuiltInTypes.Text, BuiltInTypes.Timestamptz], BuiltInTypes.Timestamptz, "timestamptz_trunc"),
    ];

    // The types max and min take, with the transition function of each.
    private static readonly (long Type, string Larger, string Smaller)[] Extremes =
    [
        (BuiltInTypes.Int2, "int2larger", "int2smaller"),
        (BuiltInTypes.Int4, "int4larger", "int4smaller"),
        (BuiltInTypes.Int8, "int8larger", "int8smaller"),
        (BuiltInTypes.Numeric, "numeric_larger", "numeric_smaller"),
        (BuiltInTypes.Float4, "float4larger", "float4smaller"),
        (BuiltInTypes.Float8, "float8larger", "float8smaller"),
        (BuiltInTypes.Text, "text_larger", "text_smaller"),
        (BuiltInTypes.Bpchar, "bpchar_larger", "bpchar_smaller"),
        (BuiltInTypes.Date, "date_larger", "date_smaller"),
        (BuiltInTypes.Timestamp, "timestamp_larger", "timestamp_smaller"),
        (BuiltInTypes.Timestamptz, "timestamptz_larger", "timestamptz_smaller"),
    ];

    private static readonly (
        string Name,
        long[] Arguments,
        long Result,
        string Transition,
        string Final
    )[] AggregateRows =
    [
        .. Extremes.Select(static row => ("max", new[] { row.Type }, row.Type, row.Larger, NoFinal)),
        .. Extremes.Select(static row => ("min", new[] { row.Type }, row.Type, row.Smaller, NoFinal)),
        ("sum", [BuiltInTypes.Int2], BuiltInTypes.Int8, "int2_sum", NoFinal),
        ("sum", [BuiltInTypes.Int4], BuiltInTypes.Int8, "int4_sum", NoFinal),
        ("sum", [BuiltInTypes.Int8], BuiltInTypes.Numeric, "int8_avg_accum", "numeric_poly_sum"),
        ("sum", [BuiltInTypes.Numeric], BuiltInTypes.Numeric, "numeric_avg_accum", "numeric_sum"),
        ("sum", [BuiltInTypes.Float4], BuiltInTypes.Float4, "float4pl", NoFinal),
        ("sum", [BuiltInTypes.Float8], BuiltInTypes.Float8, "float8pl", NoFinal),
        ("avg", [BuiltInTypes.Int2], BuiltInTypes.Numeric, "int2_avg_accum", "int8_avg"),
        ("avg", [BuiltInTypes.Int4], BuiltInTypes.Numeric, "int4_avg_accum", "int8_avg"),
        ("avg", [BuiltInTypes.Int8], BuiltInTypes.Numeric, "int8_avg_accum", "numeric_poly_avg"),
        ("avg", [BuiltInTypes.Numeric], BuiltInTypes.Numeric, "numeric_avg_accum", "numeric_avg"),
        ("avg", [BuiltInTypes.Float4], BuiltInTypes.Float8, "float4_accum", "float8_avg"),
        ("avg", [BuiltInTypes.Float8], BuiltInTypes.Float8, "float8_accum", "float8_avg"),
        (
            "string_agg",
            [BuiltInTypes.Text, BuiltInTypes.Text],
            BuiltInTypes.Text,
            "string_agg_transfn",
            "string_agg_finalfn"
        ),
        ("bool_and", [BuiltInTypes.Bool], BuiltInTypes.Bool, "booland_statefunc", NoFinal),
        ("bool_or", [BuiltInTypes.Bool], BuiltInTypes.Bool, "boolor_statefunc", NoFinal),
        ("every", [BuiltInTypes.Bool], BuiltInTypes.Bool, "booland_statefunc", NoFinal),
    ];

    // The name and the number of arguments of each function and each aggregate, which a call is checked against before
    // its arguments are proved.
    private static readonly FrozenSet<(string Name, int Arguments)> FunctionShapes = FunctionRows
        .Select(static row => (row.Name, row.Arguments.Length))
        .ToFrozenSet();

    private static readonly FrozenSet<(string Name, int Arguments)> AggregateShapes = AggregateRows
        .Select(static row => (row.Name, row.Arguments.Length))
        .ToFrozenSet();

    private static readonly FrozenDictionary<(string Name, long Left, long Right), long> OperatorsBySignature =
        OperatorRows.ToFrozenDictionary(static row => (row.Name, row.Left, row.Right), static row => row.Result);

    /// <summary>
    /// Every operator of the table: its name, its left and right argument types, its result, and the C function that
    /// implements it, the <c>prosrc</c> of its <c>oprcode</c>.  <c>Catalog_TotalFunctions_ArePgCatalogsOwn</c> reads
    /// them from here.
    /// </summary>
    internal static IEnumerable<(string Name, long Left, long Right, long Result, string Implementation)> Operators =>
        OperatorRows;

    /// <summary>
    /// Every function of the table: its name, its argument types, its result, and the C function that implements it,
    /// its <c>prosrc</c>.  <c>Catalog_TotalFunctions_ArePgCatalogsOwn</c> reads them from here.
    /// </summary>
    internal static IEnumerable<(string Name, long[] Arguments, long Result, string Implementation)> Functions =>
        FunctionRows;

    /// <summary>
    /// Every aggregate of the table: its name, its argument types, its result, and the names of its transition
    /// function and its final one, <c>-</c> for none.  <c>Catalog_TotalAggregates_ArePgCatalogsOwn</c> reads them from
    /// here.
    /// </summary>
    internal static IEnumerable<(
        string Name,
        long[] Arguments,
        long Result,
        string Transition,
        string Final
    )> Aggregates => AggregateRows;

    /// <summary>The result of the operator of the table of that name and argument types, or null.</summary>
    public static long? OperatorResult(string name, long left, long right) =>
        OperatorsBySignature.TryGetValue((name, left, right), out var result) ? result : null;

    /// <summary>Whether the table holds a function of that name that takes that many arguments.</summary>
    public static bool HasFunction(string name, int arguments) => FunctionShapes.Contains((name, arguments));

    /// <summary>Whether the table holds an aggregate of that name that takes that many arguments.</summary>
    public static bool HasAggregate(string name, int arguments) => AggregateShapes.Contains((name, arguments));

    /// <summary>The result of the function of the table of that name and argument types, or null.</summary>
    public static long? FunctionResult(string name, IReadOnlyList<long> arguments) =>
        ResultOf(FunctionRows.Select(static row => (row.Name, row.Arguments, row.Result)), name, arguments);

    /// <summary>The result of the aggregate of the table of that name and argument types, or null.</summary>
    public static long? AggregateResult(string name, IReadOnlyList<long> arguments) =>
        ResultOf(AggregateRows.Select(static row => (row.Name, row.Arguments, row.Result)), name, arguments);

    private static long? ResultOf(
        IEnumerable<(string Name, long[] Arguments, long Result)> rows,
        string name,
        IReadOnlyList<long> arguments
    )
    {
        foreach (var (rowName, rowArguments, result) in rows)
        {
            if (string.Equals(rowName, name, StringComparison.Ordinal) && rowArguments.SequenceEqual(arguments))
            {
                return result;
            }
        }

        return null;
    }

    // + - * / over two of one number type, two integers mixed with the wider result, and real with double precision
    // either way; = <> < <= > >= over two of one compared type, two integers mixed, and real with double precision
    // either way; =(xid, integer) and ||(text, text).
    private static IEnumerable<(
        string Name,
        long Left,
        long Right,
        long Result,
        string Implementation
    )> BuildOperators()
    {
        foreach (var name in new[] { "+", "-", "*", "/" })
        {
            foreach (var type in NumberTypes)
            {
                yield return (name, type, type, type, Arithmetic(name, type, type));
            }

            foreach (var (left, right) in MixedIntegers())
            {
                var wider = Array.IndexOf(IntegerTypes, left) > Array.IndexOf(IntegerTypes, right) ? left : right;
                yield return (name, left, right, wider, Arithmetic(name, left, right));
            }

            yield return (
                name,
                BuiltInTypes.Float4,
                BuiltInTypes.Float8,
                BuiltInTypes.Float8,
                Arithmetic(name, BuiltInTypes.Float4, BuiltInTypes.Float8)
            );
            yield return (
                name,
                BuiltInTypes.Float8,
                BuiltInTypes.Float4,
                BuiltInTypes.Float8,
                Arithmetic(name, BuiltInTypes.Float8, BuiltInTypes.Float4)
            );
        }

        foreach (var name in new[] { "=", "<>", "<", "<=", ">", ">=" })
        {
            foreach (var type in ComparedTypes)
            {
                yield return (name, type, type, BuiltInTypes.Bool, Comparison(name, type, type));
            }

            foreach (var (left, right) in MixedIntegers())
            {
                yield return (name, left, right, BuiltInTypes.Bool, Comparison(name, left, right));
            }

            yield return (
                name,
                BuiltInTypes.Float4,
                BuiltInTypes.Float8,
                BuiltInTypes.Bool,
                Comparison(name, BuiltInTypes.Float4, BuiltInTypes.Float8)
            );
            yield return (
                name,
                BuiltInTypes.Float8,
                BuiltInTypes.Float4,
                BuiltInTypes.Bool,
                Comparison(name, BuiltInTypes.Float8, BuiltInTypes.Float4)
            );
        }

        yield return ("=", BuiltInTypes.Xid, BuiltInTypes.Int4, BuiltInTypes.Bool, "xideq");
        yield return ("||", BuiltInTypes.Text, BuiltInTypes.Text, BuiltInTypes.Text, "textcat");
    }

    // The C function of an arithmetic operator: int4mul, int84pl, float48div, numeric_sub.
    private static string Arithmetic(string name, long left, long right)
    {
        var suffix = name switch
        {
            "+" => "pl",
            "-" => "mi",
            "*" => "mul",
            _ => "div",
        };
        if (left == BuiltInTypes.Numeric)
        {
            return "numeric_"
                + (
                    name switch
                    {
                        "+" => "add",
                        "-" => "sub",
                        _ => suffix,
                    }
                );
        }

        return Prefix(left, right) + suffix;
    }

    // The C function of a comparison: int4eq, int82lt, float84ge, numeric_le, texteq, text_lt, bpcharne, date_gt,
    // timestamp_ge for both timestamps, boolle.
    private static string Comparison(string name, long left, long right)
    {
        var suffix = name switch
        {
            "=" => "eq",
            "<>" => "ne",
            "<" => "lt",
            "<=" => "le",
            ">" => "gt",
            _ => "ge",
        };
        return left switch
        {
            BuiltInTypes.Numeric => "numeric_" + suffix,
            BuiltInTypes.Text => suffix is "eq" or "ne" ? "text" + suffix : "text_" + suffix,
            BuiltInTypes.Bpchar => "bpchar" + suffix,
            BuiltInTypes.Date => "date_" + suffix,
            BuiltInTypes.Timestamp or BuiltInTypes.Timestamptz => "timestamp_" + suffix,
            BuiltInTypes.Bool => "bool" + suffix,
            _ => Prefix(left, right) + suffix,
        };
    }

    // The prefix of the C functions of two number types: int4 for two integers, int84 for a bigint and an integer,
    // float48 for a real and a double precision.
    private static string Prefix(long left, long right)
    {
        var (family, first) = Short(left);
        return left == right ? family + first : family + first + Short(right).Width;
    }

    private static (string Family, string Width) Short(long type) =>
        type switch
        {
            BuiltInTypes.Int2 => ("int", "2"),
            BuiltInTypes.Int4 => ("int", "4"),
            BuiltInTypes.Int8 => ("int", "8"),
            BuiltInTypes.Float4 => ("float", "4"),
            BuiltInTypes.Float8 => ("float", "8"),
            _ => throw new InvalidOperationException($"No C function prefix for the type {type}."),
        };

    // Two different integer types, in each order.
    private static IEnumerable<(long Left, long Right)> MixedIntegers() =>
        IntegerTypes.SelectMany(static left =>
            IntegerTypes.Where(right => right != left).Select(right => (left, right))
        );
}
