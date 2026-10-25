using SqlSource.Generation.Converters;

namespace SqlSource.Generation.Types;

/// <summary>
/// The C# types that the type override names, as the compilation resolves them, and the converters declared on those
/// of a referenced assembly.  Two steps after it hand each on alone, so that a change to one runs nothing that reads
/// only the other.
/// </summary>
/// <param name="Types">One record for each name, in the order of the names.</param>
/// <param name="Converters">
/// The methods marked <c>[SqlSourceConverter]</c> on each type that a name found, of a referenced assembly, each type
/// read once, in the order of <see cref="ConverterSet.Comparer" />.
/// </param>
internal sealed record ResolvedTypes(EquatableArray<MappedType> Types, EquatableArray<Converter> Converters)
{
    /// <summary>No types and no converters, for no names.</summary>
    public static ResolvedTypes Empty { get; } = new(EquatableArray<MappedType>.Empty, EquatableArray<Converter>.Empty);
}
