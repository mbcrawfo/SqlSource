using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Types.Maps;

namespace SqlSource.Generation.Converters;

/// <summary>
/// Every converter of the project, from its own source and from the mapped types, with <c>SQLSRC277</c> and
/// <c>SQLSRC278</c> at the methods of its source.
/// </summary>
/// <param name="Converters">
/// The converters, in the order of <see cref="Comparer" />: ordinal by argument, then by return type, then by method,
/// then by display name.
/// A mapped type's converter of an edge that one of the project's source has is left out, so that a project can replace
/// a library's conversion.
/// </param>
/// <param name="Diagnostics">
/// <c>SQLSRC277</c> at each method of the project's source that is not a converter, and <c>SQLSRC278</c> at each that
/// doubles an edge.
/// </param>
internal sealed record ConverterSet(EquatableArray<Converter> Converters, EquatableArray<DiagnosticInfo> Diagnostics)
{
    /// <summary>The order of <see cref="Converters" />, whole, so that it does not depend on the order found.</summary>
    public static IComparer<Converter> Comparer { get; } = new ConverterComparer();

    // Without an init accessor, so that no with expression can change the converters and leave the index below behind.
    public EquatableArray<Converter> Converters { get; } = Converters;

    // The places in Converters, ordered by return type and then by place, so that Touching finds a return type with a
    // search.  It is made from Converters, so it is not part of the set's equality.
    private readonly ImmutableArray<int> _byReturn = OrderByReturn(Converters);

    /// <summary>
    /// The converters as found, from the project's source or from the mapped types, in the order of
    /// <see cref="Comparer" />.
    /// </summary>
    public static EquatableArray<Converter> Sort(ImmutableArray<Converter> converters) =>
        converters.IsEmpty ? EquatableArray<Converter>.Empty : new(converters.Sort(Comparer));

    /// <summary>
    /// The converters of the project's source and of the mapped types together, with the diagnostics of the project's
    /// source.  A method with a fault is kept, as an edge.  Two declarations of one method, as the two of a partial
    /// method, are one converter.  A method with a fault is no part of a <c>SQLSRC278</c>.
    /// </summary>
    public static ConverterSet Of(EquatableArray<Converter> project, EquatableArray<Converter> mappedTypes)
    {
        var all = ImmutableArray.CreateBuilder<Converter>(project.Count + mappedTypes.Count);
        foreach (var converter in project.OrderBy(static converter => converter, Comparer))
        {
            if (all.Count == 0 || !IsSameMethod(all[all.Count - 1], converter))
            {
                all.Add(converter);
            }
        }

        var projectEdges = new HashSet<(string Argument, string Return)>(
            all.Select(static converter => (converter.ArgumentText, converter.ReturnText))
        );
        var diagnostics = Report(all);
        all.AddRange(
            mappedTypes.Where(converter => !projectEdges.Contains((converter.ArgumentText, converter.ReturnText)))
        );

        all.Sort(Comparer);
        return new ConverterSet(new EquatableArray<Converter>(all.ToImmutable()), diagnostics);
    }

    /// <summary>
    /// The converters whose argument or return type is <paramref name="typeText" />, each once, in the order of
    /// <see cref="Converters" />.
    /// </summary>
    public EquatableArray<Converter> Touching(string typeText)
    {
        var places = new List<int>();
        var start = SortedSearch.LowerBound(Converters.Count, new ArgumentProbe(Converters, typeText));
        for (var place = start; place < Converters.Count && IsText(Converters[place].ArgumentText, typeText); place++)
        {
            places.Add(place);
        }

        var returnStart = SortedSearch.LowerBound(_byReturn.Length, new ReturnProbe(Converters, _byReturn, typeText));
        for (
            var index = returnStart;
            index < _byReturn.Length && IsText(Converters[_byReturn[index]].ReturnText, typeText);
            index++
        )
        {
            // A converter of the type to itself is found by its argument already.
            if (!IsText(Converters[_byReturn[index]].ArgumentText, typeText))
            {
                places.Add(_byReturn[index]);
            }
        }

        if (places.Count == 0)
        {
            return EquatableArray<Converter>.Empty;
        }

        places.Sort();
        return new EquatableArray<Converter>(places.Select(place => Converters[place]).ToImmutableArray());
    }

    public bool Equals(ConverterSet? other) =>
        other is not null && Converters.Equals(other.Converters) && Diagnostics.Equals(other.Diagnostics);

    public override int GetHashCode() => unchecked((Converters.GetHashCode() * 31) + Diagnostics.GetHashCode());

    // At each converter of the project's source, in their order: SQLSRC277 for one with a fault, and SQLSRC278 for one
    // of an edge that another without a fault has, naming the first other.
    private static EquatableArray<DiagnosticInfo> Report(ImmutableArray<Converter>.Builder converters)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var edges = converters
            .Where(static converter => converter.Location is not null && converter.Fault is null)
            .GroupBy(static converter => (converter.ArgumentText, converter.ReturnText))
            .Where(static edge => edge.Skip(1).Any())
            .ToDictionary(static edge => edge.Key, static edge => edge.ToList());
        foreach (var converter in converters)
        {
            if (converter.Location is not { } location)
            {
                continue;
            }

            if (converter.Fault is { } fault)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(SqlDiagnostics.NotAConverter, location, converter.DisplayName, fault)
                );
            }
            else if (edges.TryGetValue((converter.ArgumentText, converter.ReturnText), out var doubled))
            {
                var other = doubled.First(candidate => !ReferenceEquals(candidate, converter));
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.DuplicateConverter,
                        location,
                        converter.DisplayName,
                        other.DisplayName,
                        PostgresTypeMap.AsWritten(converter.ArgumentText),
                        PostgresTypeMap.AsWritten(converter.ReturnText)
                    )
                );
            }
        }

        return diagnostics.Count == 0
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable());
    }

    // The display name tells overloads apart, by their type parameters and the modifiers of their argument.  Two local
    // functions of one name in two blocks of one method have one display name, so only their places tell them apart.
    private static bool IsSameMethod(Converter left, Converter right) =>
        IsText(left.Method, right.Method)
        && IsText(left.DisplayName, right.DisplayName)
        && IsText(left.ArgumentText, right.ArgumentText)
        && IsText(left.ReturnText, right.ReturnText)
        && (left.Fault != ConverterReader.LocalFunctionFault || Equals(left.Location, right.Location));

    private static bool IsText(string left, string right) => left.Equals(right, StringComparison.Ordinal);

    private static ImmutableArray<int> OrderByReturn(EquatableArray<Converter> converters) =>
        Enumerable
            .Range(0, converters.Count)
            .OrderBy(place => converters[place].ReturnText, StringComparer.Ordinal)
            .ThenBy(static place => place)
            .ToImmutableArray();

    // Structs, so that SortedSearch does not box them.  Each compares the text of a place in a list ordered by it,
    // ordinally.
    private readonly struct ArgumentProbe(EquatableArray<Converter> converters, string typeText) : SortedSearch.IProbe
    {
        public int CompareAt(int index) => string.CompareOrdinal(converters[index].ArgumentText, typeText);
    }

    private readonly struct ReturnProbe(
        EquatableArray<Converter> converters,
        ImmutableArray<int> byReturn,
        string typeText
    ) : SortedSearch.IProbe
    {
        public int CompareAt(int index) => string.CompareOrdinal(converters[byReturn[index]].ReturnText, typeText);
    }

    // Ordinal by argument, return type, method and display name, then the project's before the mapped types', then by
    // position, so that two declarations of one method keep the first.
    private sealed class ConverterComparer : IComparer<Converter>
    {
        public int Compare(Converter? x, Converter? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null || y is null)
            {
                return x is null ? -1 : 1;
            }

            var order = string.CompareOrdinal(x.ArgumentText, y.ArgumentText);
            if (order == 0)
            {
                order = string.CompareOrdinal(x.ReturnText, y.ReturnText);
            }

            if (order == 0)
            {
                order = string.CompareOrdinal(x.Method, y.Method);
            }

            if (order == 0)
            {
                order = string.CompareOrdinal(x.DisplayName, y.DisplayName);
            }

            if (order == 0)
            {
                order = x.Origin.CompareTo(y.Origin);
            }

            if (order == 0)
            {
                order = string.CompareOrdinal(x.Location?.Path, y.Location?.Path);
            }

            return order == 0 ? (x.Location?.Span.Start ?? 0).CompareTo(y.Location?.Span.Start ?? 0) : order;
        }
    }
}
