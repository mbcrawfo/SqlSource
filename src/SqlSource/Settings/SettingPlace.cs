namespace SqlSource.Settings;

/// <summary>
/// The place a setting was written in.  A diagnostic about a value it gave names it.
/// </summary>
internal enum SettingPlace
{
    /// <summary>A property of the project.</summary>
    Property,

    /// <summary>The metadata of a file's item.</summary>
    Metadata,

    /// <summary>The attribute of a type.</summary>
    Attribute,

    /// <summary>A marker of a file's header, before its first query.</summary>
    FileHeader,

    /// <summary>A marker of one query, or of a file that is one query.</summary>
    Query,
}
