using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Generation.Paths;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator;

// Runs the generator over in-memory C# and .sql files.
//
// tests/SqlSource.Tests.RoslynFloor references this library and runs it on Roslyn 4.8.0, the oldest compiler the
// generator supports.  So the files of this folder use only API that Roslyn 4.8.0 has, and the C# they hand to the
// compiler is C# 12 at most.
internal static class GeneratorHarness
{
    public const string SourcePath = "/app/Repo/Sample.cs";

    // The project's settings file of a run, in the farthest folder of every path the tests use, so that a test's own
    // settings file is nearer and wins.  The harness writes it from a run's output, dialect, generator parameters and
    // settings.
    public const string ProjectSettingsPath = "/sqlsource.json";

    // The path of the .sql file that most tests use.
    public const string UsersPath = "/app/Repo/Users.sql";

    // That path as the generator normalises it, relative to the project's folder.
    public const string UsersNormalizedPath = "app/Repo/Users.sql";

    // The file that most tests give the generator: one query, GetUser, at UsersPath.
    public static readonly SqlFile UsersFile = new(UsersPath, SqlTexts.GetUser);

    // A settings file's text as the generator reads it: with comments and trailing commas.
    private static readonly JsonDocumentOptions SettingsText = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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

    // The analyzers that the compiler finds in the generator's assembly, looked up the way it looks them up in a
    // package: by their attribute, not by name.
    private static readonly ImmutableArray<DiagnosticAnalyzer> PackageAnalyzers = new AnalyzerFileReference(
        typeof(SqlSourceGenerator).Assembly.Location,
        new LoadedAssembly()
    ).GetAnalyzers(LanguageNames.CSharp);

    // The source of the type App.Sample, whose attribute has SqlLocation.Direct and these arguments after it: null for
    // none, or text such as Parameters = "keep-comments".
    public static string SampleSource(string? arguments) =>
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct"
        + (arguments is null ? string.Empty : ", " + arguments)
        + ")]\npublic partial class Sample;\n";

    // A settings file at the path, with this JSON.  The compiler gives one to the generator as an additional file, as
    // it gives a .sql file, so a test passes it among the .sql files.
    public static SqlFile SettingsFile(string path, string json) => new(path, json);

    // Every run states its output, the output of the project's settings file: null writes none, for a test that needs
    // the project not to set it, of the default, of the output's levels or of the MSBuild path.
    public static GeneratorRun Run(OutputKind? output, string source, params SqlFile[] sqlFiles) =>
        Run(output, [new SourceFile(SourcePath, source)], sqlFiles);

    public static GeneratorRun Run(
        OutputKind? output,
        SourceFile[] sources,
        SqlFile[] sqlFiles,
        bool supportedFramework = true,
        LanguageVersion languageVersion = LanguageVersion.CSharp12,
        MetadataReference[]? references = null,
        string? generatorParameters = null,
        string? dialect = null,
        IReadOnlyDictionary<string, string?>? properties = null,
        string? settings = null
    )
    {
        var parseOptions = ParseOptions.WithLanguageVersion(languageVersion);
        var compilation = CreateCompilation(sources, supportedFramework, parseOptions, references);

        // The properties and each file's dialect and metadata are what MSBuild tells the generator, for the tests of
        // the MSBuild path.
        var options = new TestOptionsProvider(
            null,
            null,
            null,
            sqlFiles.Where(file => file.Dialect is not null).ToDictionary(file => file.Path, file => file.Dialect!),
            properties,
            sqlFiles.Where(file => file.Metadata is not null).ToDictionary(file => file.Path, file => file.Metadata!)
        );
        var projectSettings = ProjectSettings(output, dialect, generatorParameters, settings);
        var files = WithProjectSettings(sqlFiles, projectSettings);
        var driver = CreateDriver(files.Select(file => file.ToAdditionalText()), options, parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updated,
                out _,
                TestContext.Current.CancellationToken
            );

        var result = driver.GetRunResult();

        // An exception in the generator makes the compiler drop everything it generates, for every type.
        result.Results.Single().Exception.ShouldBeNull();
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
            ],
            result
        )
        {
            ProjectSettings = projectSettings,
        };
    }

    // The JSON of the project's settings file: the output, spelled as a user writes it, the dialect as given, the
    // generator parameters as the array of their words, and then each entry of settings, a JSON object as a user
    // writes the file, which replaces the key it names.  Null when all four are null.  The spelling is the tests' own,
    // not taken from the generator, so that a change to the generator's spelling fails a test.
    private static string? ProjectSettings(
        OutputKind? output,
        string? dialect = null,
        string? generatorParameters = null,
        string? settings = null
    )
    {
        if (output is null && dialect is null && generatorParameters is null && settings is null)
        {
            return null;
        }

        var json = new JsonObject();
        if (output is { } kind)
        {
            json["output"] = Spell(kind);
        }

        if (dialect is not null)
        {
            json["dialect"] = dialect;
        }

        if (generatorParameters is not null)
        {
            json["generatorParameters"] = Words(generatorParameters);
        }

        if (settings is not null)
        {
            var entries =
                JsonNode.Parse(settings, documentOptions: SettingsText) as JsonObject
                ?? throw new ArgumentException("The settings are not a JSON object.", nameof(settings));
            foreach (var entry in entries.ToList())
            {
                json[entry.Key] = entry.Value?.DeepClone();
            }
        }

        return Json(json);
    }

    // The text of a settings file's JSON, indented, with < and > as a user writes them in a type's name.
    public static string Json(JsonNode json) => json.ToJsonString(Written);

    // Generator parameters as a settings file writes them: the array of their words, split at white space.
    public static JsonArray Words(string words) =>
        [
            .. words
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => (JsonNode?)JsonValue.Create(word)),
        ];

    // The files with the project's settings file after them, or the files alone when it is null.  A file of the test's
    // own at its path is refused: the generator reads one file of a folder, so one of the two would be dropped unseen.
    private static SqlFile[] WithProjectSettings(SqlFile[] files, string? projectSettings)
    {
        if (projectSettings is null)
        {
            return files;
        }

        if (files.Any(file => SqlPath.Normalize(file.Path) == SqlPath.Normalize(ProjectSettingsPath)))
        {
            throw new InvalidOperationException(
                $"The run writes the project's settings file {ProjectSettingsPath}, and a file of the test is at that "
                    + "path.  Put its keys in the run's settings instead."
            );
        }

        return [.. files, SettingsFile(ProjectSettingsPath, projectSettings)];
    }

    private static string Spell(OutputKind output) =>
        output switch
        {
            OutputKind.Sql => "Sql",
            OutputKind.Models => "Models",
            OutputKind.CodeGen => "CodeGen",
            _ => throw new ArgumentOutOfRangeException(nameof(output), output, null),
        };

    // Without the runtime's references the compilation has no System.ArgumentException, which is how the generator
    // sees a project that targets a framework older than .NET 8.
    public static CSharpCompilation CreateCompilation(
        SourceFile[] sources,
        bool supportedFramework = true,
        CSharpParseOptions? parseOptions = null,
        IEnumerable<MetadataReference>? references = null,
        string assemblyName = "TestAssembly"
    ) =>
        CSharpCompilation.Create(
            assemblyName,
            sources.Select(source =>
                CSharpSyntaxTree.ParseText(
                    source.Text,
                    parseOptions ?? ParseOptions,
                    source.Path,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ),
            (supportedFramework ? RuntimeReferences : []).Concat(references ?? []),
            // Every warning wave is reported: CS8981, a lower-case type name, is only from warning level 7.
            new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary, warningLevel: 9999)
        );

    // Step tracking is on, so that a test can read why each step of the pipeline ran.  The options are what MSBuild
    // tells the generator.
    public static GeneratorDriver CreateDriver(
        IEnumerable<AdditionalText> sqlFiles,
        TestOptionsProvider options,
        CSharpParseOptions? parseOptions = null
    ) =>
        CSharpGeneratorDriver.Create(
            [new SqlSourceGenerator().AsSourceGenerator()],
            sqlFiles,
            parseOptions ?? ParseOptions,
            options,
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true)
        );

    // Another assembly that the generator ran on too, compiled and ready to reference.  With InternalsVisibleTo for
    // "TestAssembly" in its source, it is what a test project sees of the project it tests.
    public static MetadataReference CreateReference(OutputKind? output, string assemblyName, string source) =>
        CreateProject(output, assemblyName, source).Reference;

    // The same with the .sql files the generator reads, the references and the dialect of the project, and the code the
    // generator gave it by name.  The output and the dialect are its settings file, as a run's are.
    public static (MetadataReference Reference, IReadOnlyDictionary<string, string> Sources) CreateProject(
        OutputKind? output,
        string assemblyName,
        string source,
        SqlFile[]? sqlFiles = null,
        MetadataReference[]? references = null,
        string? dialect = null
    )
    {
        var compilation = CreateCompilation(
            [new SourceFile("/other/Other.cs", source)],
            references: references,
            assemblyName: assemblyName
        );
        var driver = CreateDriver(
                WithProjectSettings(sqlFiles ?? [], ProjectSettings(output, dialect))
                    .Select(file => file.ToAdditionalText()),
                new TestOptionsProvider(null, null)
            )
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updated,
                out _,
                TestContext.Current.CancellationToken
            );

        using var image = new MemoryStream();
        updated.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        return (MetadataReference.CreateFromImage(image.ToArray()), SourcesOf(driver));
    }

    // The compiler's warnings and errors for the source together with the generated code, as a build of the project
    // reports them: after the analyzers that the package brings have run.  Without them it is what the compiler
    // reports on its own.
    public static async Task<IReadOnlyList<string>> BuildAsync(
        OutputKind? output,
        string source,
        SqlFile[] sqlFiles,
        MetadataReference[] references,
        bool warningsAsErrors = false,
        bool packageAnalyzers = true,
        string? dialect = null
    )
    {
        var (diagnostics, _) = await BuildProjectAsync(
            output,
            source,
            sqlFiles,
            references,
            warningsAsErrors,
            packageAnalyzers,
            dialect
        );
        return diagnostics;
    }

    // The same, with the code the generator gave the project by name.
    public static async Task<(
        IReadOnlyList<string> Diagnostics,
        IReadOnlyDictionary<string, string> Sources
    )> BuildProjectAsync(
        OutputKind? output,
        string source,
        SqlFile[] sqlFiles,
        MetadataReference[] references,
        bool warningsAsErrors = false,
        bool packageAnalyzers = true,
        string? dialect = null
    )
    {
        var compilation = CreateCompilation([new SourceFile(SourcePath, source)], references: references);
        if (warningsAsErrors)
        {
            compilation = compilation.WithOptions(
                compilation.Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error)
            );
        }

        var driver = CreateDriver(
                WithProjectSettings(sqlFiles, ProjectSettings(output, dialect)).Select(file => file.ToAdditionalText()),
                new TestOptionsProvider(null, null)
            )
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updated,
                out _,
                TestContext.Current.CancellationToken
            );

        var diagnostics = packageAnalyzers
            ? await updated
                .WithAnalyzers(PackageAnalyzers)
                .GetAllDiagnosticsAsync(TestContext.Current.CancellationToken)
            : updated.GetDiagnostics(TestContext.Current.CancellationToken);

        return (
            [.. diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).Select(Format)],
            SourcesOf(driver)
        );
    }

    // The code the generator gave, by name.
    private static Dictionary<string, string> SourcesOf(GeneratorDriver driver) =>
        driver
            .GetRunResult()
            .Results.Single()
            .GeneratedSources.ToDictionary(
                generated => generated.HintName,
                generated => generated.SourceText.ToString()
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

// The generator's assembly is already loaded, by the reference to its project.
internal sealed class LoadedAssembly : IAnalyzerAssemblyLoader
{
    public void AddDependencyLocation(string fullPath) { }

    public Assembly LoadFromPath(string fullPath) => typeof(SqlSourceGenerator).Assembly;
}

// Dialect is the SqlSourceDialect metadata of the file's AdditionalFiles item, and Metadata its other metadata by
// MSBuild name, for the tests of the MSBuild path.  Null is a file without it.
internal sealed record SqlFile(
    string Path,
    string? Text,
    string? Dialect = null,
    IReadOnlyDictionary<string, string>? Metadata = null
)
{
    public AdditionalText ToAdditionalText() => new InMemoryAdditionalText(Path, Text);
}

// What one run of the generator produced.  The diagnostics are formatted by GeneratorHarness.
//   Diagnostics: what the generator reported.
//   CompilationErrors: the compiler's errors for the source together with the generated code.
//   GeneratedCodeWarnings: the compiler's warnings inside generated code, where a consumer cannot fix them.
//   Result: the driver's own result, for a test that reads a diagnostic's severity or what a named step gave.
internal sealed record GeneratorRun(
    IReadOnlyDictionary<string, string> Sources,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> CompilationErrors,
    IReadOnlyList<string> GeneratedCodeWarnings,
    GeneratorDriverRunResult Result
)
{
    // The text of the project's settings file that the harness wrote, or null when it wrote none.
    public string? ProjectSettings { get; init; }

    // The generator reported nothing, and the compiler found no error and no warning in what it generated.
    public void ShouldBeClean()
    {
        Diagnostics.ShouldBeEmpty();
        CompilationErrors.ShouldBeEmpty();
        GeneratedCodeWarnings.ShouldBeEmpty();
    }

    // The one severity of every diagnostic of the id that the generator reported.
    public DiagnosticSeverity SeverityOf(string id) =>
        Result
            .Diagnostics.Where(diagnostic => diagnostic.Id == id)
            .Select(diagnostic => diagnostic.Severity)
            .Distinct()
            .Single();
}

// The lines that start a generated file: Header a file of queries, ModelHeader a file of models.  RowBuffer is the
// buffer a file of methods ends with when one reads its rows into it, at the depth of a namespace.  They are the
// tests' own text, not read from the product, so the snapshots that hold them still say what the text is.
internal static class GeneratedFile
{
    public const string ModelHeader = "// <auto-generated/>\n#nullable enable\n";

    public const string Header =
        ModelHeader + "#pragma warning disable CS0108, CS0114 // A query can have the name of an inherited member.\n";

    public const string RowBuffer =
        RowBufferToImmutable + RowBufferAsImmutableLine + RowBufferToGrow + RowBufferGrowLine + RowBufferAfterGrow;

    // The buffer up to the line of ToImmutableArray that is longer than the width of a line here, up to the line of
    // Grow that is, and after it.
    private const string RowBufferToImmutable = """
            /// <summary>
            /// The rows of a query method, read into buffers from the shared array pool: one of up to 1,024 elements is
            /// rented and returned cleared, and a larger one is left to the garbage collector.
            /// </summary>
            file struct SqlSourceRowBuffer<T>
                where T : class
            {
                private object[]? items;

                private int count;

                public void Add(T row)
                {
                    if (items is null || count == items.Length)
                    {
                        Grow();
                    }

                    items![count++] = row;
                }

                public T[] ToArray()
                {
                    if (items is null)
                    {
                        return global::System.Array.Empty<T>();
                    }

                    var result = new T[count];
                    Rows().CopyTo(result);
                    Release();
                    return result;
                }

                public global::System.Collections.Generic.List<T> ToList()
                {
                    if (items is null)
                    {
                        return new global::System.Collections.Generic.List<T>();
                    }

                    var result = new global::System.Collections.Generic.List<T>(count);
                    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(result, count);
                    Rows().CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(result));
                    Release();
                    return result;
                }

                public global::System.Collections.Immutable.ImmutableArray<T> ToImmutableArray()
                {
                    if (items is null)
                    {
                        return global::System.Collections.Immutable.ImmutableArray<T>.Empty;
                    }


        """;

    private const string RowBufferAsImmutableLine =
        "            return global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray("
        + "ToArray());\n";

    private const string RowBufferToGrow = """
                }

                private global::System.ReadOnlySpan<T> Rows() =>
                    global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                        ref global::System.Runtime.CompilerServices.Unsafe.As<object, T>(
                            ref global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items!)),
                        count);

                private void Release()
                {
                    Return(items!);
                    items = null;
                    count = 0;
                }

                private void Grow()
                {
                    var length = items is null ? 16 : items.Length * 2;

        """;

    private const string RowBufferGrowLine =
        "            var next = length <= 1024 ? global::System.Buffers.ArrayPool<object>.Shared.Rent(length) : "
        + "new object[length];\n";

    private const string RowBufferAfterGrow = """
                    if (items is not null)
                    {
                        global::System.Array.Copy(items, next, count);
                        Return(items);
                    }

                    items = next;
                }

                private void Return(object[] buffer)
                {
                    if (buffer.Length <= 1024)
                    {
                        global::System.Array.Clear(buffer, 0, count);
                        global::System.Buffers.ArrayPool<object>.Shared.Return(buffer);
                    }
                }
            }

        """;
}
