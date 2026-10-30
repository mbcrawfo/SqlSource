using SqlSource.Diagnostics;

namespace SqlSource.Generation.Converters;

/// <summary>
/// A method marked <c>[SqlSourceConverter]</c>: an edge from its argument's type to its return type.  It holds no
/// symbol, so it is equal from one edit to the next until the method itself changes.
/// </summary>
/// <param name="Method">The call's target, <c>global::App.Email.FromDatabase</c>.</param>
/// <param name="DisplayName">The method as a message names it, <c>App.Email.FromDatabase(string)</c>.</param>
/// <param name="ArgumentText">
/// The type of its argument as <see cref="Settings.ClrTypes.ClrTypeName.Text" /> writes it, without a <c>?</c> at its
/// top, <c>global::App.UserId</c> of <c>App.UserId?</c>, since a converter never sees null; or empty for a method
/// without one.
/// </param>
/// <param name="ArgumentIsValueType">Whether the type of its argument, without that <c>?</c>, is a value type.</param>
/// <param name="ReturnText">Its return type as <see cref="Settings.ClrTypes.ClrTypeName.Text" /> writes it.</param>
/// <param name="ReturnIsValueType">Whether its return type is a value type.</param>
/// <param name="WarningIds">
/// The ids of the compiler's warnings that a call of it gives, for the method or a containing type marked
/// <c>[Obsolete]</c> or <c>[Experimental]</c>, in ordinal order, each once.
/// </param>
/// <param name="Fault">
/// Why it is not a converter, as the clause of <c>SQLSRC277</c> words it, the first of its faults; null for a
/// converter.
/// </param>
/// <param name="Origin">Where it was found.</param>
/// <param name="Location">The method's name, or null for a method of a referenced assembly.</param>
/// <param name="Library">
/// The library an edge of origin <see cref="ConverterOrigin.Library" /> belongs to and the engines and drivers it
/// serves; null for any other.
/// </param>
internal sealed record Converter(
    string Method,
    string DisplayName,
    string ArgumentText,
    bool ArgumentIsValueType,
    string ReturnText,
    bool ReturnIsValueType,
    EquatableArray<string> WarningIds,
    string? Fault,
    ConverterOrigin Origin,
    LocationInfo? Location,
    LibraryScope? Library = null
)
{
    /// <summary>
    /// Whether the converter serves <paramref name="target" />: always for one that is not a library's, else when its
    /// scope holds the target.
    /// </summary>
    public bool Serves(ConverterTargets target) => Library is null || (Library.Targets & target) != 0;
}
