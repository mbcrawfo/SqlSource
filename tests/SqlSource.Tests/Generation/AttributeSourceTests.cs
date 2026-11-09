using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Testing.Generator;
using Xunit;
using RoslynOutputKind = Microsoft.CodeAnalysis.OutputKind;

namespace SqlSource.Tests.Generation;

public sealed class AttributeSourceTests
{
    // The language of a project that targets a framework older than .NET 8, which gets the attribute's file too.
    private static readonly CSharpParseOptions CSharp73 = new(LanguageVersion.CSharp7_3);

    // The settings of output, generator parameters, models, collections and placement are the files' alone.
    [Theory]
    [InlineData("GeneratorOutput")]
    [InlineData("GeneratorModelType")]
    [InlineData("GeneratorCollectionType")]
    [InlineData("public string Parameters")]
    [InlineData("public GeneratorOutput Output")]
    [InlineData("public string ModelNamespace")]
    [InlineData("enum SqlLocation")]
    [InlineData("enum MethodLocation")]
    [InlineData("public SqlLocation SqlLocation")]
    [InlineData("public MethodLocation MethodLocation")]
    [InlineData("public string NestedClass")]
    public void Text_RemovedSettings_AreNotDeclared(string declaration) =>
        AttributeSource.Text.ShouldNotContain(declaration);

    // A project that sees the internals of another one that uses SqlSource sees none of the other's copies.
    [Fact]
    public void Text_EveryType_IsEmbedded()
    {
        var compilation = CompileText();

        var types = compilation
            .GetSymbolsWithName(_ => true, SymbolFilter.Type, TestContext.Current.CancellationToken)
            .OfType<INamedTypeSymbol>()
            .Where(type => type.ContainingNamespace.ToDisplayString() == "SqlSource")
            .ToList();

        types.Count.ShouldBe(3);
        types.ShouldAllBe(type =>
            type.GetAttributes()
                .Any(attribute =>
                    attribute.AttributeClass!.ToDisplayString() == "Microsoft.CodeAnalysis.EmbeddedAttribute"
                )
        );
    }

    // A library's converters have to be found from the project that references it, so the attribute stays in metadata.
    [Fact]
    public void Text_ConverterAttribute_TargetsMethodsAndIsNotConditional()
    {
        var compilation = CompileText();

        compilation.GetDiagnostics(TestContext.Current.CancellationToken).ShouldBeEmpty();
        var attribute = compilation
            .GetTypeByMetadataName(AttributeSource.ConverterAttributeMetadataName)
            .ShouldNotBeNull();
        attribute.DeclaredAccessibility.ShouldBe(Accessibility.Internal);
        attribute.IsSealed.ShouldBeTrue();
        attribute.BaseType!.ToDisplayString().ShouldBe("System.Attribute");
        var usage = attribute.GetAttributes().Single(a => a.AttributeClass!.Name == "AttributeUsageAttribute");
        usage.ConstructorArguments.Single().Value.ShouldBe((int)AttributeTargets.Method);
        attribute.GetAttributes().ShouldNotContain(a => a.AttributeClass!.Name == "ConditionalAttribute");
    }

    // A repository's connection may be a member of a base class in a referenced assembly, so the attribute stays in
    // metadata.
    [Fact]
    public void Text_ConnectionAttribute_TargetsMembersAndParametersAndIsNotConditional()
    {
        var compilation = CompileText();

        compilation.GetDiagnostics(TestContext.Current.CancellationToken).ShouldBeEmpty();
        var attribute = compilation
            .GetTypeByMetadataName(AttributeSource.ConnectionAttributeMetadataName)
            .ShouldNotBeNull();
        attribute.DeclaredAccessibility.ShouldBe(Accessibility.Internal);
        attribute.IsSealed.ShouldBeTrue();
        attribute.BaseType!.ToDisplayString().ShouldBe("System.Attribute");
        var usage = attribute.GetAttributes().Single(a => a.AttributeClass!.Name == "AttributeUsageAttribute");
        usage
            .ConstructorArguments.Single()
            .Value.ShouldBe(
                (int)(
                    AttributeTargets.Field
                    | AttributeTargets.Property
                    | AttributeTargets.Method
                    | AttributeTargets.Parameter
                )
            );
        attribute.GetAttributes().ShouldNotContain(a => a.AttributeClass!.Name == "ConditionalAttribute");
        attribute
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(property => $"{property.Type.ToDisplayString()} {property.Name}")
            .ShouldBe(["string Database"]);
    }

    [Fact]
    public void Text_GenerateAttribute_HasTheInterfaceProperties()
    {
        var attribute = CompileText().GetTypeByMetadataName(AttributeSource.AttributeMetadataName).ShouldNotBeNull();

        attribute
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(property => $"{property.Type.ToDisplayString()} {property.Name}")
            .ShouldBe([
                $"string {AttributeSource.SourceProperty}",
                $"bool {AttributeSource.GenerateInterfaceProperty}",
                $"string {AttributeSource.InterfaceNameProperty}",
            ]);
    }

    [Fact]
    public void Text_ConverterAttributeOnAMethod_StaysInMetadata()
    {
        var library = CompileLibrary(
            "Library",
            AttributeSource.Text,
            GeneratedFile.EmbeddedAttribute,
            "public static class C { [SqlSource.SqlSourceConverter] public static int F(long v) => (int)v; }"
        );
        using var image = new MemoryStream();
        library.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        image.Position = 0;
        var consumer = CSharpCompilation.Create(
            "Consumer",
            references: References().Append(MetadataReference.CreateFromImage(image.ToArray())),
            options: new CSharpCompilationOptions(RoslynOutputKind.DynamicallyLinkedLibrary)
        );

        var method = consumer.GetTypeByMetadataName("C")!.GetMembers("F").OfType<IMethodSymbol>().Single();

        method
            .GetAttributes()
            .Select(attribute => attribute.AttributeClass!.ToDisplayString())
            .ShouldBe(["SqlSource.SqlSourceConverterAttribute"]);
    }

    private static CSharpCompilation CompileLibrary(string name, params string[] sources) =>
        CSharpCompilation.Create(
            name,
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            References(),
            new CSharpCompilationOptions(RoslynOutputKind.DynamicallyLinkedLibrary)
        );

    private static IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

    private static CSharpCompilation CompileText() =>
        CSharpCompilation.Create(
            "AttributeText",
            [
                CSharpSyntaxTree.ParseText(AttributeSource.Text, CSharp73),
                CSharpSyntaxTree.ParseText(GeneratedFile.EmbeddedAttribute, CSharp73),
            ],
            References(),
            new CSharpCompilationOptions(RoslynOutputKind.DynamicallyLinkedLibrary)
        );
}
