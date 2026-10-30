namespace SqlSource.SettingsFiles;

/// <summary>The JSON type the value of a key of a settings file must have.</summary>
internal enum SettingsFileValueType
{
    /// <summary>A string.</summary>
    String,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>An array whose elements are strings.</summary>
    ArrayOfStrings,

    /// <summary>An object whose members' values are strings.</summary>
    ObjectOfStrings,

    /// <summary>
    /// An object whose members' values are strings, or objects of a <c>type</c> string and an optional <c>json</c>
    /// boolean: the column types.
    /// </summary>
    ObjectOfColumnTypes,
}
