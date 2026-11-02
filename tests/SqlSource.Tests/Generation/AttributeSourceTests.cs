using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;
using RoslynOutputKind = Microsoft.CodeAnalysis.OutputKind;

namespace SqlSource.Tests.Generation;

// The generator reads an enum argument of the attribute as a number and casts it to its own form of the enum.  The
// two must agree member by member, or a value a user writes would mean another.
public sealed partial class AttributeSourceTests
{
    // The generator's own forms are internal, so a public theory names them and the test looks them up.
    [Theory]
    [InlineData("SqlLocation", nameof(MemberPlacement))]
    [InlineData("GeneratorOutput", nameof(OutputKind))]
    [InlineData("GeneratorModelType", nameof(ModelKind))]
    [InlineData("GeneratorCollectionType", nameof(CollectionKind))]
    [InlineData("MethodLocation", nameof(MethodPlacement))]
    public void EmittedEnum_ComparedWithTheGeneratorsOwnForm_HasTheSameMembersAndNumbers(string emitted, string ownName)
    {
        var own = new[]
        {
            typeof(MemberPlacement),
            typeof(OutputKind),
            typeof(ModelKind),
            typeof(CollectionKind),
            typeof(MethodPlacement),
        }.Single(type => type.Name == ownName);
        var body = Regex
            .Match(AttributeSource.Text, @"internal enum " + emitted + @"\s*\{(?<body>[^}]*)\}")
            .Groups["body"]
            .Value;

        var members = Member()
            .Matches(body)
            .Select(match => match.Groups["name"].Value + "=" + match.Groups["value"].Value);

        members.ShouldBe(
            Enum.GetValues(own)
                .Cast<object>()
                .Select(value => value + "=" + Convert.ToInt32(value, CultureInfo.InvariantCulture))
        );
    }

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

        types.Count.ShouldBe(7);
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

    // The language of a project that targets a framework older than .NET 8, which gets the attribute's file too.
    private static readonly CSharpParseOptions CSharp73 = new(LanguageVersion.CSharp7_3);

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

    [GeneratedRegex(@"^\s*(?<name>\w+) = (?<value>\d+),", RegexOptions.Multiline)]
    private static partial Regex Member();
}
