using System.Collections.Immutable;
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
    /// The metadata name of each library's class of conversions, which a project gets when its methods call one.  A
    /// project that sees the internals of another one whose methods call it too sees it twice, and
    /// <c>AttributeConflictSuppressor</c> turns the compiler's warning off for these as for
    /// <see cref="AttributeSource.GeneratedTypes" />.
    /// </summary>
    public static ImmutableArray<string> ClassMetadataNames { get; } =
        ImmutableArray.Create(NodaTimeConversions.MetadataName, NetTopologySuiteConversions.MetadataName);

    /// <summary>
    /// The edges of every library <paramref name="probe" /> finds, in the order of <see cref="ConverterSet.Comparer"
    /// />: NodaTime's when the project references it, else none.
    /// </summary>
    public static EquatableArray<Converter> Of(LibraryProbe probe) =>
        probe.NodaTime ? NodaTimeConversions.Edges : EquatableArray<Converter>.Empty;

    /// <summary>
    /// The text of the file of <paramref name="library" />'s class, or null for a library with none.
    /// </summary>
    public static string? SourceOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime => NodaTimeConversions.Source,
            SqlLibrary.NetTopologySuite => NetTopologySuiteConversions.Source,
            _ => null,
        };

    /// <summary>
    /// The name of the file of <paramref name="library" />'s class, made from the library's name.  No type's file can
    /// have it, since a <c>-</c> in a type's name is followed only by its arity's digits, and no model's or type's
    /// methods' can, since those end in <c>-Model.g.cs</c> and <c>-Methods.g.cs</c>; and it is not the attribute's.
    /// </summary>
    public static string HintNameOf(SqlLibrary library) => "SqlSource-" + library + "Conversions.g.cs";
}
