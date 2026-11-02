using System.Collections.Immutable;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Converters;

/// <summary>
/// The edges that SqlSource generates for the libraries a project references, and the text of the class that holds
/// their methods, or, for NetTopologySuite, the shared instances of its spatial read path.  An edge is offered
/// whenever the project references the library, whether the library's setting is on or not: the setting decides only
/// what an unmapped column becomes.
/// </summary>
internal static class LibraryConverters
{
    /// <summary>
    /// What starts the declaration of each library's class, before its name: it is marked
    /// <c>[Microsoft.CodeAnalysis.Embedded]</c>, so that a project that sees the internals of another one whose methods
    /// call it too does not see the other's.
    /// </summary>
    public const string ClassDeclaration =
        GeneratedText.Indent
        + "[global::Microsoft.CodeAnalysis.Embedded]\n"
        + GeneratedText.Indent
        + "internal static class ";

    /// <summary>
    /// The edges of every library <paramref name="probe" /> finds, in the order of <see cref="ConverterSet.Comparer"
    /// />: NodaTime's when the project references it, and System.Text.Json's when it does.
    /// </summary>
    public static EquatableArray<Converter> Of(LibraryProbe probe) =>
        Merge(
            probe.NodaTime ? NodaTimeConversions.Edges : EquatableArray<Converter>.Empty,
            probe.Json ? JsonConversions.Edges : EquatableArray<Converter>.Empty
        );

    /// <summary>
    /// The text of the file of <paramref name="library" />'s class.  It is nullable because the output it feeds takes a
    /// file that may have no text.
    /// </summary>
    public static string? SourceOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime => NodaTimeConversions.Source,
            SqlLibrary.NetTopologySuite => NetTopologySuiteConversions.Source,
            SqlLibrary.Json => JsonConversions.Source,
        };

    /// <summary>
    /// The name of the file of <paramref name="library" />'s class, made from the library's name.  No type's file can
    /// have it, since a <c>-</c> in a type's name is followed only by its arity's digits, and no model's or type's
    /// methods' can, since those end in <c>-Model.g.cs</c> and <c>-Methods.g.cs</c>; and it is not the attribute's.
    /// </summary>
    public static string HintNameOf(SqlLibrary library) => "SqlSource-" + library + "Conversions.g.cs";

    // Two libraries' edges, each in the order of ConverterSet.Comparer, as one in that order: either as it is when the
    // other has none.
    private static EquatableArray<Converter> Merge(EquatableArray<Converter> first, EquatableArray<Converter> second)
    {
        if (second.Count == 0)
        {
            return first;
        }

        if (first.Count == 0)
        {
            return second;
        }

        var merged = ImmutableArray.CreateBuilder<Converter>(first.Count + second.Count);
        var left = 0;
        var right = 0;
        while (left < first.Count || right < second.Count)
        {
            merged.Add(
                right == second.Count
                || (left < first.Count && ConverterSet.Comparer.Compare(first[left], second[right]) <= 0)
                    ? first[left++]
                    : second[right++]
            );
        }

        return new EquatableArray<Converter>(merged.MoveToImmutable());
    }
}
