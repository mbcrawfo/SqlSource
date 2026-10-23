using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// The names of functions and operators that only <c>pg_catalog</c> holds: no other schema holds a function or an
/// operator of one, whatever the order of the search path.
/// </summary>
/// <param name="Functions">The names of functions, as <c>pg_proc.proname</c> spells them.</param>
/// <param name="Operators">The names of operators, as <c>pg_operator.oprname</c> spells them.</param>
internal sealed record CatalogNames(IReadOnlySet<string> Functions, IReadOnlySet<string> Operators)
{
    /// <summary>No name: every name may be another schema's.</summary>
    public static CatalogNames None { get; } = new(FrozenSet<string>.Empty, FrozenSet<string>.Empty);
}

/// <summary>
/// The names of <c>pg_catalog</c> whose every overload is never <c>NULL</c> under a condition the plan's text shows,
/// read by their name and not by their argument types.
/// </summary>
/// <remarks>
/// <para>
/// A call of a polymorphic signature, <c>count("any")</c> or <c>*(anyrange, anyrange)</c>, prints bare with its
/// arguments as they are, and another schema's function or operator whose argument types are the call's exactly beats
/// it whatever the order of the path, so <see cref="TotalFunctions" />' rule of an exact signature cannot read it.  A
/// name that no schema but <c>pg_catalog</c> holds is another thing: the call is one of <c>pg_catalog</c>'s overloads
/// of the name, whichever the server chose.  So a name of this table is read only when the session finds
/// <c>pg_catalog</c> first and no other schema holds a function or an operator of it
/// (<see cref="ExpressionContext.CatalogAlone" />), and only when every overload <c>pg_catalog</c> holds of it is one
/// the table lists: an overload the table does not list may be <c>NULL</c> where these are not.
/// </para>
/// <para>
/// What each name reads: <c>count(...)</c> with any argument, an optional <c>FILTER (...)</c> and an optional
/// <c>OVER</c>, a <c>bigint</c> always; <c>array_agg(...)</c> as an aggregate with nothing after its parenthesis, of no
/// known type, where the node that computes it is one each of whose groups has a row
/// (<see cref="ExpressionContext.GroupsHaveRows" />); <c>jsonb_build_object(...)</c> and <c>json_build_object(...)</c>
/// with no argument written <c>VARIADIC</c>, a <c>jsonb</c> or a <c>json</c> always; a constructor of a built-in range
/// type with two or three arguments, of that range type always; and <c>(A * B)</c> with both operands proved of one
/// of those range types, of that type.  The arguments of the forms that are not <c>NULL</c> always are read for their
/// balance only, since none can make the call <c>NULL</c>.
/// </para>
/// <para>
/// Why each overload is never <c>NULL</c> under its condition, from the C function that implements it in the source of
/// PostgreSQL 14 and 18: <c>count()</c> and <c>count("any")</c> start from <c>0</c> with no final function, and their
/// transitions <c>int8inc</c> and <c>int8inc_any</c> are strict, so a <c>NULL</c> input is skipped; over no row and in
/// an empty frame the count is <c>0</c>.  <c>array_agg_transfn</c> and <c>array_agg_array_transfn</c> are not strict
/// and make their state at the first row, a <c>NULL</c> input included, the second raising an error for a
/// <c>NULL</c> or an empty array; <c>array_agg_finalfn</c> and <c>array_agg_array_finalfn</c> return <c>NULL</c> only
/// for a state that saw no row, whatever the <c>DISTINCT</c> or the <c>ORDER BY</c>.  <c>jsonb_build_object</c> and
/// <c>json_build_object</c> return <c>NULL</c> only for a <c>VARIADIC</c> array that is <c>NULL</c>, and raise an error
/// for a <c>NULL</c> key or an odd number of arguments; <c>jsonb_build_object_noargs</c> and
/// <c>json_build_object_noargs</c> return <c>{}</c>.  <c>range_constructor2</c> reads a <c>NULL</c> bound as infinite,
/// <c>range_constructor3</c> raises an error for a <c>NULL</c> third argument, and both raise an error for a lower
/// bound above the upper.  <c>range_intersect</c> is strict and returns the empty range for two that do not overlap.
/// Of the operators <c>*</c>, <c>*(anyrange, anyrange)</c> is the one that takes two ranges, since
/// <c>*(anymultirange, anymultirange)</c> would need a cast from a range that <c>pg_cast</c> holds as explicit only.
/// </para>
/// <para>
/// An aggregate runs other functions in a window and in a parallel plan, and they keep the property.  In a window whose
/// frame moves, <c>count</c> runs as a moving aggregate, from the initial value <c>0</c>, with <c>int8inc</c> or
/// <c>int8inc_any</c> as its transition and <c>int8dec</c> or <c>int8dec_any</c> as the inverse, all strict, so a
/// <c>NULL</c> input is skipped either way, and with no final function: the state is a count, never <c>NULL</c>.  A
/// <c>Finalize</c> node combines the partial counts with <c>int8pl</c>, each of them a count from <c>0</c>.  From
/// PostgreSQL 16, <c>array_agg</c> has <c>array_agg_combine</c> and <c>array_agg_array_combine</c>, which are not
/// strict and give a state that saw no row only when both of theirs saw none, with <c>array_agg_serialize</c>,
/// <c>array_agg_deserialize</c> and the arrays' own, which serialize and deserialize the state and keep a state that
/// saw a row not null; before 16 it has none, and no plan computes it in parts.  So over a group that has a row, the
/// state the final function reads saw one.
/// </para>
/// <para>
/// A row goes in this table only with <c>Catalog_CatalogOverloads_ArePinned</c> in <c>PlanOutputTests</c>, which holds
/// the overloads of each name to the catalog of 14 and 18, their implementations included, none strict, since a strict
/// one is <c>NULL</c> for a <c>NULL</c> argument before its C function runs, the functions of each aggregate's moving
/// and partial modes, and the intersection to each range type.
/// </para>
/// </remarks>
internal static class CatalogOverloads
{
    // The built-in range types, each with its constructor's name and the type of its bounds as the catalog prints it.
    private static readonly (string Name, long Type, string Bound)[] Ranges =
    [
        ("int4range", BuiltInTypes.Int4range, "integer"),
        ("int8range", BuiltInTypes.Int8range, "bigint"),
        ("numrange", BuiltInTypes.Numrange, "numeric"),
        ("tsrange", BuiltInTypes.Tsrange, "timestamp without time zone"),
        ("tstzrange", BuiltInTypes.Tstzrange, "timestamp with time zone"),
        ("daterange", BuiltInTypes.Daterange, "date"),
    ];

    // An aggregate's implementation is aggtransfn/aggfinalfn/agginitval, "-" for no final function and nothing for no
    // initial value.
    private static readonly (string Name, string Arguments, string Implementation)[] Rows =
    [
        ("count", "", "int8inc/-/0"),
        ("count", "\"any\"", "int8inc_any/-/0"),
        ("array_agg", "anynonarray", "array_agg_transfn/array_agg_finalfn/"),
        ("array_agg", "anyarray", "array_agg_array_transfn/array_agg_array_finalfn/"),
        ("jsonb_build_object", "VARIADIC \"any\"", "jsonb_build_object"),
        ("jsonb_build_object", "", "jsonb_build_object_noargs"),
        ("json_build_object", "VARIADIC \"any\"", "json_build_object"),
        ("json_build_object", "", "json_build_object_noargs"),
        .. Ranges.SelectMany(static range =>
            new[]
            {
                (range.Name, $"{range.Bound}, {range.Bound}", "range_constructor2"),
                (range.Name, $"{range.Bound}, {range.Bound}, text", "range_constructor3"),
            }
        ),
    ];

    private static readonly ReadOnlyCollection<(string Name, string Arguments, string Implementation)> ReadOnlyRows =
        Array.AsReadOnly(Rows);

    private static readonly FrozenSet<long> RangeOids = Ranges.Select(static range => range.Type).ToFrozenSet();

    private static readonly FrozenDictionary<string, long> RangeTypes = Ranges.ToFrozenDictionary(
        static range => range.Name,
        static range => range.Type,
        StringComparer.Ordinal
    );

    /// <summary>The names of the functions and aggregates of the table.</summary>
    public static IReadOnlyList<string> FunctionNames { get; } =
    ["count", "array_agg", "jsonb_build_object", "json_build_object", .. Ranges.Select(static range => range.Name)];

    /// <summary>The names of the operators of the table.</summary>
    public static IReadOnlyList<string> OperatorNames { get; } = ["*"];

    /// <summary>
    /// Every overload <c>pg_catalog</c> holds of each function name: its name, its arguments as
    /// <c>pg_get_function_identity_arguments</c> prints them, and its implementation, the <c>prosrc</c> of a function
    /// and <c>aggtransfn/aggfinalfn/agginitval</c> of an aggregate, <c>-</c> for no final function and nothing for no
    /// initial value.  <c>Catalog_CatalogOverloads_ArePinned</c> reads them from here.
    /// </summary>
    public static IEnumerable<(string Name, string Arguments, string Implementation)> Overloads => ReadOnlyRows;

    /// <summary>The OID of the range type a constructor of the table builds, or null for any other name.</summary>
    public static long? RangeTypeOf(string constructor) =>
        RangeTypes.TryGetValue(constructor, out var type) ? type : null;

    /// <summary>Whether the type is one of the built-in range types of the table.</summary>
    public static bool IsRange(long type) => RangeOids.Contains(type);
}
