using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Types;

/// <summary>The C# type of one parameter or column of a query, which its model's property has.</summary>
/// <param name="Text">The C# type as a model writes it, a keyword or <c>global::</c>-qualified.</param>
/// <param name="IsValueType">Whether the C# type is a value type.</param>
/// <param name="IsPublic">Whether the C# type is accessible from any assembly.</param>
/// <param name="WarningIds">
/// The ids of the compiler's warnings that a use of the C# type gives, which its model disables, in ordinal order.
/// </param>
/// <param name="Key">
/// The key that answered: of the type override's entry, or of the row of the base map, which may be the key of an
/// array's element or of a domain's base; for a type that a marker or the column types chose, the key an entry of the
/// type override would have for the database type, which a converter's message names.
/// </param>
/// <param name="Place">
/// The place that chose the type, as <c>SQLSRC250</c> to <c>SQLSRC252</c> word it: the one that wrote the type
/// override's or the column types' entry, or the marker that gave it.  Null for a type of the base map.
/// </param>
/// <param name="Database">
/// What the base map reads the database type as, which a converter converts from and to, set exactly when
/// <paramref name="Place" /> is.  It changes no model's C#, so <c>ModelCollector.FindDifference</c> does not compare
/// it.
/// </param>
/// <param name="Mapped">
/// What the messages of <c>SQLSRC250</c> to <c>SQLSRC252</c> name as mapped when it is not <paramref name="Key" />:
/// <c>@author</c> for a parameter whose <c>-- param:</c> marker chose its type, and for a column the key of the column
/// types' entry, <c>users.id</c>, or the name its <c>-- column:</c> marker gives.  Null for a type the override or the
/// base map gave.  It changes no model's C# either, and is not compared.
/// </param>
/// <param name="Library">
/// The library whose row gave the type, or null for any other type.  A library row has no <paramref name="Place" />,
/// <paramref name="Database" /> or <paramref name="Mapped" />, so no converter applies: the driver's plugin reads and
/// writes the type.
/// </param>
/// <param name="WithoutLibrary">
/// The type the parameter or column has without the library, set exactly when <paramref name="Library" /> is: what the
/// type override and the base map give it, for a type that claims the query at an output the library does not apply
/// to.
/// </param>
internal sealed record PropertyType(
    string Text,
    bool IsValueType,
    bool IsPublic,
    EquatableArray<string> WarningIds,
    string Key,
    string? Place,
    DatabaseType? Database = null,
    string? Mapped = null,
    SqlLibrary? Library = null,
    PropertyType? WithoutLibrary = null
);
