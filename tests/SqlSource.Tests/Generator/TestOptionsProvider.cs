using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Tests.Generator;

// The project's MSBuild properties as the compiler hands them to a generator.  Only SqlSourceTokenValidation is
// there, and a null value is a project that does not set it.
internal sealed class TestOptionsProvider(string? tokenValidation) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(tokenValidation);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(null);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(null);

    private sealed class Options(string? tokenValidation) : AnalyzerConfigOptions
    {
        // The key is spelled out here, not taken from the generator, so that a change to the generator's spelling
        // fails a test.
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            value = key == "build_property.SqlSourceTokenValidation" ? tokenValidation : null;
            return value is not null;
        }
    }
}
