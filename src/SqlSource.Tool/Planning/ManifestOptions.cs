using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The values of a manifest under the keys the compiler would give them, so that the generator's own readers of
/// settings read them: <c>build_property.&lt;Name&gt;</c> for a property of the project, and
/// <c>build_metadata.SqlSourceSettingsFile.&lt;Name&gt;</c> for the metadata of a file.
/// </summary>
internal sealed class ManifestOptions : AnalyzerConfigOptions
{
    private const string PropertyPrefix = "build_property.";

    private const string MetadataPrefix = "build_metadata.SqlSourceSettingsFile.";

    private readonly ImmutableDictionary<string, string> _values;

    private readonly string _prefix;

    private ManifestOptions(ImmutableDictionary<string, string> values, string prefix)
    {
        _values = values;
        _prefix = prefix;
    }

    public static ManifestOptions ForProject(ProjectManifest manifest) => new(manifest.Properties, PropertyPrefix);

    public static ManifestOptions ForFile(ManifestFile file) => new(file.Metadata, MetadataPrefix);

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        if (key.StartsWith(_prefix, StringComparison.Ordinal))
        {
            return _values.TryGetValue(key[_prefix.Length..], out value);
        }

        value = null;
        return false;
    }
}
