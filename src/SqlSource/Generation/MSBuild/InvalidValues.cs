using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Generation.MSBuild;

/// <summary>
/// What is not valid in one project's MSBuild properties and item metadata, each value once, in the order the errors
/// are reported in.  The generator and the <c>sqlsource</c> tool each fill one and read it back, so that the order is
/// defined here and the errors of a build do not depend on the order of its files.
/// </summary>
internal sealed class InvalidValues
{
    private readonly SortedSet<string> _dialects = [with(StringComparer.Ordinal)];

    private readonly HashSet<InvalidSetting> _settings = [];

    /// <summary>The dialects that are not valid, each once, in ordinal order.</summary>
    public IReadOnlyCollection<string> Dialects => _dialects;

    /// <summary>The settings that are not valid, each once, by name and then by value, both ordinal.</summary>
    public IEnumerable<InvalidSetting> Settings =>
        _settings
            .OrderBy(static setting => setting.Name, StringComparer.Ordinal)
            .ThenBy(static setting => setting.Value, StringComparer.Ordinal);

    /// <summary>
    /// Adds a dialect that is not valid, as written.  Null, for a dialect that is valid or not set, is ignored.
    /// </summary>
    public void AddDialect(string? value)
    {
        if (value is not null)
        {
            _ = _dialects.Add(value);
        }
    }

    /// <summary>Adds settings that are not valid.</summary>
    public void AddSettings(IEnumerable<InvalidSetting> settings) => _settings.UnionWith(settings);
}
