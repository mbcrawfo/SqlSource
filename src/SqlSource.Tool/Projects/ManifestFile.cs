using System.Collections.Immutable;

namespace SqlSource.Tool.Projects;

/// <summary>
/// One <c>.sql</c> file of a project manifest.
/// </summary>
/// <param name="Path">The full path, as MSBuild gives it.</param>
/// <param name="Metadata">The metadata of the file's item that has a value, by name, trimmed.</param>
internal sealed record ManifestFile(string Path, ImmutableDictionary<string, string> Metadata);
