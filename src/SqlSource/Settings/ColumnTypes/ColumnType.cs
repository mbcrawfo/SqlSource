using SqlSource.Settings.ClrTypes;

namespace SqlSource.Settings.ColumnTypes;

/// <summary>
/// One entry of the column types: the C# type of the columns read from one column of a table or view.
/// </summary>
/// <param name="Key">
/// The column's source, <c>table.column</c> or <c>schema.table.column</c>, as <see cref="ColumnTypeKey.TryNormalize" />
/// gives it.
/// </param>
/// <param name="Type">
/// The C# type as <see cref="ClrTypeName.Text" /> writes it, or null for <c>default</c>, which takes the key out of
/// the entries of the levels below.
/// </param>
/// <param name="Place">Where the entry was written.</param>
internal sealed record ColumnType(string Key, string? Type, SettingPlace Place);
