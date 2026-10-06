using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace SqlSource.Tests.Generator;

// Runs the generator over in-memory C# and .sql files.
//
// The files of this folder are also compiled into tests/SqlSource.Tests.RoslynFloor, which runs them on Roslyn 4.8.0,
// the oldest compiler the generator supports.  So they use only API that Roslyn 4.8.0 has, and the C# they hand to
// the compiler is C# 12 at most.
internal static class GeneratorHarness
{
    public const string SourcePath = "/app/Repo/Sample.cs";

    // The newest language version that Roslyn 4.8.0 parses.  Documentation comments are checked, as they are in a
    // project that builds a documentation file, so that a malformed or missing one in generated code is reported.
    public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp12, DocumentationMode.Diagnose);

    // The assemblies of the runtime the tests run on, which is .NET 8 or later.
    private static readonly ImmutableArray<MetadataReference> RuntimeReferences =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    public static GeneratorRun Run(string source, params SqlFile[] sqlFiles) =>
        Run([new SourceFile(SourcePath, source)], sqlFiles);

    public static GeneratorRun Run(SourceFile[] sources, SqlFile[] sqlFiles, bool supportedFramework = true)
    {
        var compilation = CreateCompilation(sources, supportedFramework);
        var driver = CreateDriver(sqlFiles.Select(file => file.ToAdditionalText()))
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updated,
                out _,
                TestContext.Current.CancellationToken
            );

        var result = driver.GetRunResult();
        var diagnostics = updated.GetDiagnostics(TestContext.Current.CancellationToken);
        return new GeneratorRun(
            result
                .Results.Single()
                .GeneratedSources.ToDictionary(source => source.HintName, source => source.SourceText.ToString()),
            [.. result.Diagnostics.Select(Format)],
            [.. diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(Format)],
            [
                .. diagnostics
                    .Where(diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Warning
                        && result.GeneratedTrees.Contains(diagnostic.Location.SourceTree!)
                    )
                    .Select(Format),
            ]
        );
    }

    // Without references the compilation has no System.ArgumentException, which is how the generator sees a project
    // that targets a framework older than .NET 8.
    public static CSharpCompilation CreateCompilation(SourceFile[] sources, bool supportedFramework = true) =>
        CSharpCompilation.Create(
            "TestAssembly",
            sources.Select(source =>
                CSharpSyntaxTree.ParseText(
                    source.Text,
                    ParseOptions,
                    source.Path,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ),
            supportedFramework ? RuntimeReferences : [],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    // Step tracking is on, so that a test can read why each step of the pipeline ran.
    public static GeneratorDriver CreateDriver(IEnumerable<AdditionalText> sqlFiles) =>
        CSharpGeneratorDriver.Create(
            [new SqlSourceGenerator().AsSourceGenerator()],
            sqlFiles,
            ParseOptions,
            optionsProvider: null,
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true)
        );

    // "SQLSRC001 /app/Repo/Sample.cs(3,14)-(3,20): message", with lines and columns counted from one as an IDE
    // shows them.
    private static string Format(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var start = Format(span.StartLinePosition);
        var end = Format(span.EndLinePosition);
        return $"{diagnostic.Id} {span.Path}{start}-{end}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";
    }

    private static string Format(LinePosition position) =>
        string.Create(CultureInfo.InvariantCulture, $"({position.Line + 1},{position.Character + 1})");
}

internal sealed record SourceFile(string Path, string Text);

internal sealed record SqlFile(string Path, string? Text)
{
    public AdditionalText ToAdditionalText() => new InMemoryAdditionalText(Path, Text);
}

// What one run of the generator produced.  The diagnostics are formatted by GeneratorHarness.
//   Diagnostics: what the generator reported.
//   CompilationErrors: the compiler's errors for the source together with the generated code.
//   GeneratedCodeWarnings: the compiler's warnings inside generated code, where a consumer cannot fix them.
internal sealed record GeneratorRun(
    IReadOnlyDictionary<string, string> Sources,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> CompilationErrors,
    IReadOnlyList<string> GeneratedCodeWarnings
);
