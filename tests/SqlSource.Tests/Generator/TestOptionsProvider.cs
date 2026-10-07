using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Tests.Generator;

// What MSBuild tells a generator, as the compiler hands it over: the project's SqlSourceTokenValidation and
// SqlSourceDialect properties, and the SqlSourceDialect metadata of each .sql file, by the file's path.  A null value
// is a project that does not set the property, and a path that is not listed is a file without the metadata.
internal sealed class TestOptionsProvider(
    string? tokenValidation,
    string? dialect = null,
    IReadOnlyDictionary<string, string>? fileDialects = null
) : AnalyzerConfigOptionsProvider
{
    private static readonly Options None = new([]);

    // The keys are spelled out here, not taken from the generator, so that a change to the generator's spelling
    // fails a test.
    public override AnalyzerConfigOptions GlobalOptions { get; } =
        new Options(
            new Dictionary<string, string?>
            {
                ["build_property.SqlSourceTokenValidation"] = tokenValidation,
                ["build_property.SqlSourceDialect"] = dialect,
            }
        );

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
        fileDialects is not null && fileDialects.TryGetValue(textFile.Path, out var value)
            ? new Options(
                new Dictionary<string, string?> { ["build_metadata.SqlSourceDialectFile.SqlSourceDialect"] = value }
            )
            : None;

    private sealed class Options(Dictionary<string, string?> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value) && value is not null;
    }
}
