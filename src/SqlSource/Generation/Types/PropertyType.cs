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
/// array's element or of a domain's base.
/// </param>
/// <param name="Place">
/// The place that wrote the type override's entry, as <c>SQLSRC250</c> to <c>SQLSRC252</c> word it, for a type the
/// override names, or null for a type of the base map.
/// </param>
/// <param name="Database">
/// What the base map reads the database type as, which a converter converts from and to, set exactly when
/// <paramref name="Place" /> is.  It changes no model's C#, so <c>ModelCollector.FindDifference</c> does not compare
/// it.
/// </param>
/// <param name="Mapped">
/// What the messages of <c>SQLSRC250</c> to <c>SQLSRC252</c> name as mapped when it is not <paramref name="Key" />:
/// <c>@author</c> for a parameter whose <c>-- param:</c> marker chose its type.  Null for a type the override or the
/// base map gave.  It changes no model's C# either, and is not compared.
/// </param>
internal sealed record PropertyType(
    string Text,
    bool IsValueType,
    bool IsPublic,
    EquatableArray<string> WarningIds,
    string Key,
    string? Place,
    DatabaseType? Database = null,
    string? Mapped = null
);
