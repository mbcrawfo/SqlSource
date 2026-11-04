namespace SqlSource.Settings;

/// <summary>
/// A setting's value with the place that wrote it, for a setting whose diagnostics name where it came from.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="Place">The place that wrote it: a marker of a file's header or of a query, or a settings file.</param>
internal readonly record struct Placed<T>(T Value, SettingPlace Place)
    where T : struct;
