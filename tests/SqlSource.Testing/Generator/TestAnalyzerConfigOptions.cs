using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Testing.Generator;

// The options of an analyzer config, keyed as given (build_property.X in full).  A null value is a key that is not set.
internal sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string?> values) : AnalyzerConfigOptions
{
    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
        values.TryGetValue(key, out value) && value is not null;
}
