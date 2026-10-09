namespace SqlSource.Generation;

/// <summary>
/// A value of an MSBuild property, or of the metadata of an item, that is not valid.  It has no position: the
/// compiler does not say where either was set.
/// </summary>
/// <param name="Name">The name MSBuild knows the setting by: <c>SqlSourceGeneratorParameters</c>.</param>
/// <param name="Value">The value, or the part of it that is not valid, as written.</param>
internal sealed record InvalidSetting(string Name, string Value);
