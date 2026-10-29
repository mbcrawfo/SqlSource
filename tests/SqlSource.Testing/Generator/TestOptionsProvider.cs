using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Testing.Generator;

// What MSBuild tells a generator, as the compiler hands it over: the project's properties.  A null value is a project
// that does not set the property.  A property is named as MSBuild names it: SqlSourceBuildFolders.  No .sql file or C#
// file has metadata.
//
// The generator reads SqlSourceBuildFolders and SqlSourceTransitivePackageAssemblies alone, properties of the build and
// no settings.
internal sealed class TestOptionsProvider(IReadOnlyDictionary<string, string?>? properties = null)
    : AnalyzerConfigOptionsProvider
{
    private const string PropertyPrefix = "build_property.";

    private static readonly Options None = new([]);

    public override AnalyzerConfigOptions GlobalOptions { get; } = CreateGlobal(properties);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => None;

    private static Options CreateGlobal(IReadOnlyDictionary<string, string?>? properties)
    {
        var values = new Dictionary<string, string?>();
        foreach (var pair in properties ?? new Dictionary<string, string?>())
        {
            values[PropertyPrefix + pair.Key] = pair.Value;
        }

        return new Options(values);
    }

    private sealed class Options(Dictionary<string, string?> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value) && value is not null;
    }
}
