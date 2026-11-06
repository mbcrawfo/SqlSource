using System.Collections.Generic;
using System.Globalization;
using SqlSource.Generation.Methods;
using SqlSource.Parsing;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// The names of what a query's method declares for its lists, from its <see cref="LocalNames" />: each list's locals,
/// the locals and loop variables that count, bind and write the values, and the static local functions that help.
/// A method without a list has none, so its other locals keep the names they had.
/// </summary>
internal sealed class ListLocals
{
    private ListLocals(List<ListLocal> each, LocalNames names)
    {
        Each = each;
        Total = names.Take("total");
        Index = names.Take("index");
        End = names.Take("end");
        Element = names.Take("element");
        Written = names.Take("written");
        CountOf = names.Take("CountOf");
        PaddedSize = names.Take("PaddedSize");
        DigitsUpTo = names.Take("DigitsUpTo");
        ElementType = names.Take("TElement");
    }

    /// <summary>Each list, in the order of the query's parameters.</summary>
    public IReadOnlyList<ListLocal> Each { get; }

    /// <summary>The number of parameters the method sends, before it pads its lists.</summary>
    public string Total { get; }

    /// <summary>The index of the value a loop binds or writes.</summary>
    public string Index { get; }

    /// <summary>The index after the last that a list's value is bound at, past its count for the padding.</summary>
    public string End { get; }

    /// <summary>The loop's element, the value bound.</summary>
    public string Element { get; }

    /// <summary>The number of characters an index was written in.</summary>
    public string Written { get; }

    /// <summary>The static local function that reads a list once and gives its count.</summary>
    public string CountOf { get; }

    /// <summary>The static local function that gives the padded size of a count.</summary>
    public string PaddedSize { get; }

    /// <summary>The static local function that gives the number of digits of every index up to a size.</summary>
    public string DigitsUpTo { get; }

    /// <summary>The type parameter of <see cref="CountOf" />, the element.</summary>
    public string ElementType { get; }

    /// <summary>
    /// The names of the method's lists, or null when its input has none.
    /// </summary>
    public static ListLocals? Of(MethodInput? input, LocalNames names)
    {
        if (input is null)
        {
            return null;
        }

        List<ListLocal>? each = null;
        foreach (var parameter in input.Parameters)
        {
            if (parameter.IsList)
            {
                var ordinal = (each?.Count ?? 0).ToString(CultureInfo.InvariantCulture);
                (each ??= []).Add(
                    new ListLocal(
                        parameter,
                        names.Take("list" + ordinal),
                        names.Take("count" + ordinal),
                        names.Take("size" + ordinal),
                        names.Take("padded" + ordinal)
                    )
                );
            }
        }

        return each is null ? null : new ListLocals(each, names);
    }

    /// <summary>
    /// The index in <see cref="Each" /> of the list that a use, as the SQL writes it with its prefix, is of.
    /// </summary>
    public int IndexOf(string use)
    {
        var name = use.Substring(1);
        for (var index = 0; index < Each.Count; index++)
        {
            if (SqlParameterName.Comparer.Equals(Each[index].Parameter.Name, name))
            {
                return index;
            }
        }

        throw new KeyNotFoundException("The use " + use + " is of no list of the method.");
    }
}

/// <summary>
/// The locals of one list of a query's method.
/// </summary>
/// <param name="Parameter">The list.</param>
/// <param name="Values">The list's value, read once, as a collection.</param>
/// <param name="Count">The number of its values, 0 for null.</param>
/// <param name="Size">The number of parameters it sends: its count, padded, and 1 when it is empty.</param>
/// <param name="Padded">The padded size, before it is known to fit.</param>
internal sealed record ListLocal(MethodParameter Parameter, string Values, string Count, string Size, string Padded);
