using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Repositories;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generation.Repositories;

// The RepositoryShape that the generator reads for one marked type, as the tests of RepositoryShapeReader and of
// ConnectionCandidateReader read it.
internal static class RepositoryShapes
{
    private const string Usings = """
        using System.Data.Common;
        using System.Threading;
        using System.Threading.Tasks;
        using Npgsql;
        using SqlSource;

        """;

    // The shape of the source's one marked type, at Repository on Npgsql 10, with the usings of the tests before it,
    // and the .sql file it claims by its name, of one query, since the generator reads the shape of a type with a
    // repository query alone.  The properties are what MSBuild tells the generator.
    public static RepositoryShape Of(
        string source,
        IReadOnlyDictionary<string, string?>? properties = null,
        MetadataReference[]? references = null
    )
    {
        var run = GeneratorHarness.Run(
            OutputKind.Repository,
            [new SourceFile(GeneratorHarness.SourcePath, Usings + source)],
            [new SqlFile("/app/Repo/" + MarkedTypeNameOf(source) + ".sql", SqlTexts.GetUser)],
            references: [NpgsqlReferences.Npgsql10, .. references ?? []],
            properties: properties
        );

        run.CompilationErrors.ShouldBeEmpty();
        return StepOutputs
            .Single<EquatableArray<RepositoryShape>>(run, TrackingNames.RepositoryShapes)
            .ShouldHaveSingleItem();
    }

    // The name of the type that carries the attribute, which names the .sql file it claims.
    private static string MarkedTypeNameOf(string source) =>
        CSharpSyntaxTree
            .ParseText(source, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Single(type =>
                type.AttributeLists.SelectMany(list => list.Attributes)
                    .Any(attribute => attribute.Name.ToString() == "SqlSourceGenerate")
            )
            .Identifier.ValueText;

    // An assembly named Library, with the generated attributes, as a project that uses SqlSource builds it, emitted to
    // an image as a build references it.
    public static PortableExecutableReference Library(string source)
    {
        var compilation = GeneratorHarness.CreateCompilation(
            [
                new SourceFile("/lib/Attributes.cs", AttributeSource.Text),
                new SourceFile("/lib/Embedded.cs", GeneratedFile.EmbeddedAttribute),
                new SourceFile("/lib/Library.cs", source),
            ],
            references: [NpgsqlReferences.Npgsql10],
            assemblyName: "Library"
        );
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        emitted
            .Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();
        return MetadataReference.CreateFromImage(image.ToArray());
    }
}
