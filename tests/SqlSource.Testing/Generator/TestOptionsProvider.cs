using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Testing.Generator;

// What MSBuild tells a generator, as the compiler hands it over: the project's properties, and the metadata of each
// .sql file by the file's path.  A null value is a project that does not set the property, and a path that is not
// listed is a file without metadata.  A property or a metadata is named as MSBuild names it: SqlSourceBuildFolders.
//
// The generator reads SqlSourceBuildFolders alone, a property of the build and no setting.  The metadata, and any other
// property, are for a test that the generator reads none of the settings that MSBuild once gave.
internal sealed class TestOptionsProvider(
    IReadOnlyDictionary<string, string?>? properties = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? fileMetadata = null
) : AnalyzerConfigOptionsProvider
{
    private const string PropertyPrefix = "build_property.";

    private const string MetadataPrefix = "build_metadata.SqlSourceSettingsFile.";

    private static readonly Options None = new([]);

    public override AnalyzerConfigOptions GlobalOptions { get; } = CreateGlobal(properties);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        if (fileMetadata is null || !fileMetadata.TryGetValue(textFile.Path, out var metadata))
        {
            return None;
        }

        var values = new Dictionary<string, string?>();
        foreach (var pair in metadata)
        {
            values[MetadataPrefix + pair.Key] = pair.Value;
        }

        return new Options(values);
    }

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
