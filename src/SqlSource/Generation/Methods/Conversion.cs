namespace SqlSource.Generation.Methods;

/// <summary>
/// The converter that a column is read through, or a parameter bound through: what the method reads or binds as the
/// database's type, and the call that converts between it and the property's type.
/// </summary>
/// <param name="Method">The converter's call target, <c>global::App.Email.FromDatabase</c>.</param>
/// <param name="DatabaseText">
/// The type the base map reads the database type as, which the column is read as or the parameter bound as: for an
/// array converted element by element, the array whole, <c>string[]</c>.
/// </param>
/// <param name="DatabaseIsValueType">
/// Whether the database side that the converter takes or returns is a value type: that of
/// <paramref name="DatabaseText" /> for a conversion of the whole value, and that of its element for one by element.
/// </param>
/// <param name="IsByElement">
/// Whether the converter converts each element of an array, the property's type being an array of
/// <paramref name="ElementText" /> and <paramref name="DatabaseText" /> an array of the converter's database side.
/// </param>
/// <param name="ElementText">
/// The element of the property's type, which the converter takes or returns, for a conversion by element; null
/// otherwise.
/// </param>
/// <param name="ElementIsValueType">
/// Whether <paramref name="ElementText" /> is a value type, for a conversion by element; false otherwise.
/// </param>
/// <param name="WarningIds">
/// The ids of the compiler's warnings that a call of the converter gives, which the file of its method disables, in
/// ordinal order: the converter's <see cref="Converters.Converter.WarningIds" />.
/// </param>
internal sealed record Conversion(
    string Method,
    string DatabaseText,
    bool DatabaseIsValueType,
    bool IsByElement,
    string? ElementText,
    bool ElementIsValueType,
    EquatableArray<string> WarningIds = default
)
{
    /// <summary>
    /// The element of <see cref="DatabaseText" /> that the converter takes or returns, for a conversion by element,
    /// <c>string</c> of <c>string[]</c>; null otherwise.
    /// </summary>
    public string? DatabaseElementText => IsByElement ? ConversionPlanner.ElementOf(DatabaseText) : null;
}
