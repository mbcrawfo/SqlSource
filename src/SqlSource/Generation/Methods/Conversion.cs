using System;
using SqlSource.Settings.Libraries;

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
/// The element of the property's type as a converter's types are written, <c>ClrTypeName.KeywordTextOf</c>, so that a
/// <c>System.Nullable&lt;T&gt;</c> element is <c>T?</c>, for a conversion by element; null otherwise.  The converter
/// takes or returns it without the <c>?</c> of a nullable element, <c>global::App.UserId</c> of
/// <c>global::App.UserId?</c>.
/// </param>
/// <param name="ElementIsValueType">
/// Whether the element that the converter takes or returns is a value type, for a conversion by element; false
/// otherwise.
/// </param>
/// <param name="WarningIds">
/// The ids of the compiler's warnings that a call of the converter gives, which the file of its method disables, in
/// ordinal order: the converter's <see cref="Converters.Converter.WarningIds" />.
/// </param>
/// <param name="Library">
/// The library whose generated conversion the converter is, for an edge of origin
/// <see cref="Converters.ConverterOrigin.Library" />; null for any other.
/// </param>
/// <param name="MayReturnNull">
/// Whether the call, on a read, can return null for a value that is not a <c>NULL</c>: the parse of a JSON
/// <c>null</c> into a <see cref="Converters.JsonTypes.IsNode" /> type.  A not-null element read by element then
/// throws on it.  Also the serializer's read of a reference type, whose JSON <c>null</c> it gives as null.
/// </param>
/// <param name="IsSerializer">
/// Whether the call is System.Text.Json's serializer, <c>JsonSerializer.Deserialize&lt;T&gt;</c> or
/// <c>JsonSerializer.Serialize&lt;T&gt;</c> with its default options, for a project's own class in a type that holds
/// JSON, rather than a converter.  It has no <paramref name="Library" />: no class is emitted for it.
/// </param>
internal sealed record Conversion(
    string Method,
    string DatabaseText,
    bool DatabaseIsValueType,
    bool IsByElement,
    string? ElementText,
    bool ElementIsValueType,
    EquatableArray<string> WarningIds = default,
    SqlLibrary? Library = null,
    bool MayReturnNull = false,
    bool IsSerializer = false
)
{
    /// <summary>
    /// The element of <see cref="DatabaseText" /> that the converter takes or returns, for a conversion by element,
    /// <c>string</c> of <c>string[]</c>; null otherwise.
    /// </summary>
    public string? DatabaseElementText => IsByElement ? ConversionPlanner.ElementOf(DatabaseText) : null;

    /// <summary>
    /// The element of the array that a column converted by element is read as: <c>long?</c> of <c>long[]</c> when
    /// the database's element is a value type, since Npgsql refuses to read an array that holds a <c>NULL</c> as
    /// <c>long[]</c>, and <c>string</c> of <c>string[]</c> otherwise; null when not by element.
    /// </summary>
    public string? ReadElementText =>
        DatabaseIsValueType && DatabaseElementText is { } element ? element + "?" : DatabaseElementText;

    /// <summary>
    /// Whether the property's element is written nullable, <c>global::App.UserId?</c> or <c>global::App.Name?</c>,
    /// for a conversion by element: a <c>NULL</c> element then stays null both ways, the converter not called for it.
    /// </summary>
    public bool ElementIsNullable => ElementText is { } element && element.EndsWith("?", StringComparison.Ordinal);
}
