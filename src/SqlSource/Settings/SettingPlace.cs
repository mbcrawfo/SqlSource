namespace SqlSource.Settings;

/// <summary>The kind of place a setting was written in.</summary>
internal enum SettingPlaceKind
{
    /// <summary>The attribute of a type.</summary>
    Attribute,

    /// <summary>A marker of a file's header, before its first query.</summary>
    FileHeader,

    /// <summary>A marker of one query, or of a file that is one query.</summary>
    Query,

    /// <summary>A <c>sqlsource.json</c> file, which applies to its folder and every folder below.</summary>
    SettingsFile,
}

/// <summary>
/// The place a setting was written in.  A diagnostic about a value it gave names it.  A settings file also carries
/// its path and its folder's depth, which rank it against the settings files above and below it.
/// </summary>
/// <param name="Kind">The kind of place.</param>
/// <param name="Path">The settings file's path as the compiler gives it, or null for any other kind.</param>
/// <param name="Depth">
/// How many folders the settings file's folder is below the root, so a nearer file has the larger depth; 0 for any
/// other kind.
/// </param>
internal readonly record struct SettingPlace(SettingPlaceKind Kind, string? Path, int Depth)
{
    /// <summary>The attribute of a type.</summary>
    public static SettingPlace Attribute => new(SettingPlaceKind.Attribute, null, 0);

    /// <summary>A marker of a file's header, before its first query.</summary>
    public static SettingPlace FileHeader => new(SettingPlaceKind.FileHeader, null, 0);

    /// <summary>A marker of one query, or of a file that is one query.</summary>
    public static SettingPlace Query => new(SettingPlaceKind.Query, null, 0);

    /// <summary>The settings file at the path, whose folder is the depth below the root.</summary>
    public static SettingPlace OfSettingsFile(string path, int depth) =>
        new(SettingPlaceKind.SettingsFile, path, depth);
}
