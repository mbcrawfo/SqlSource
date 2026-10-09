using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Tests.Generator;

// What MSBuild tells a generator, as the compiler hands it over: the project's properties, and the metadata of each
// .sql file by the file's path.  A null value is a project that does not set the property, and a path that is not
// listed is a file without metadata.  A property or a metadata is named as MSBuild names it: SqlSourceOutput.
//
// The keys are spelled out here, not taken from the generator, so that a change to the generator's spelling fails a
// test.
internal sealed class TestOptionsProvider(
    string? generatorParameters,
    string? dialect = null,
    IReadOnlyDictionary<string, string>? fileDialects = null,
    IReadOnlyDictionary<string, string?>? properties = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? fileMetadata = null
) : AnalyzerConfigOptionsProvider
{
    private const string PropertyPrefix = "build_property.";

    private const string MetadataPrefix = "build_metadata.SqlSourceSettingsFile.";

    private static readonly Options None = new([]);

    public override AnalyzerConfigOptions GlobalOptions { get; } =
        CreateGlobal(generatorParameters, dialect, properties);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        var values = new Dictionary<string, string?>();
        if (fileDialects is not null && fileDialects.TryGetValue(textFile.Path, out var fileDialect))
        {
            values[MetadataPrefix + "SqlSourceDialect"] = fileDialect;
        }

        if (fileMetadata is not null && fileMetadata.TryGetValue(textFile.Path, out var metadata))
        {
            foreach (var pair in metadata)
            {
                values[MetadataPrefix + pair.Key] = pair.Value;
            }
        }

        return values.Count == 0 ? None : new Options(values);
    }

    private static Options CreateGlobal(
        string? generatorParameters,
        string? dialect,
        IReadOnlyDictionary<string, string?>? properties
    )
    {
        var values = new Dictionary<string, string?>
        {
            [PropertyPrefix + "SqlSourceGeneratorParameters"] = generatorParameters,
            [PropertyPrefix + "SqlSourceDialect"] = dialect,
        };
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
