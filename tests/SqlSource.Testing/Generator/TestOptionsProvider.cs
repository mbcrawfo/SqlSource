using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Testing.Generator;

// What MSBuild tells a generator, as the compiler hands it over: the project's properties.  A null value is a project
// that does not set the property.  A property is named as MSBuild names it: SqlSourceBuildFolders.  No .sql file or C#
// file has metadata.
//
// The generator reads SqlSourceBuildFolders, SqlSourceTransitivePackageAssemblies and SqlSourcePublicExceptions alone,
// properties of the build and no settings of the queries.
internal sealed class TestOptionsProvider(IReadOnlyDictionary<string, string?>? properties = null)
    : AnalyzerConfigOptionsProvider
{
    private const string PropertyPrefix = "build_property.";

    private static readonly TestAnalyzerConfigOptions None = new(new Dictionary<string, string?>());

    public override AnalyzerConfigOptions GlobalOptions { get; } = CreateGlobal(properties);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => None;

    private static TestAnalyzerConfigOptions CreateGlobal(IReadOnlyDictionary<string, string?>? properties)
    {
        var values = new Dictionary<string, string?>();
        foreach (var pair in properties ?? new Dictionary<string, string?>())
        {
            values[PropertyPrefix + pair.Key] = pair.Value;
        }

        return new TestAnalyzerConfigOptions(values);
    }
}
