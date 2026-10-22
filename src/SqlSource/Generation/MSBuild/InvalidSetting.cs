using System;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Generation.MSBuild;

/// <summary>
/// A value of an MSBuild property, or of the metadata of an item, that is not valid.  It has no position: the
/// compiler does not say where either was set.
/// </summary>
/// <param name="Name">
/// The name MSBuild knows the setting by, as <c>SqlSourceGeneratorParameters</c>: one of the names of
/// <see cref="MSBuildSettings" />.
/// </param>
/// <param name="Value">The value, or the part of it that is not valid, as written.</param>
internal sealed record InvalidSetting(string Name, string Value)
{
    /// <summary>
    /// The help that ends the message of <c>SQLSRC014</c>: empty, but for an entry of the type override whose key is
    /// written in brackets, which <see cref="TypeOverrideGrammar.BracketHelp" /> gives.  It follows from the value, so
    /// it is no part of what makes two settings one.
    /// </summary>
    public string Help =>
        Name == MSBuildSettings.TypeOverridesName ? TypeOverrideGrammar.BracketHelp(Value.AsSpan()) : string.Empty;
}
