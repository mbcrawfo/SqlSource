using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
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

    [Fact]
    public void GeneratedTypes_ComparedWithTheFile_AreEveryTypeItDeclares()
    {
        var declared = Declaration()
            .Matches(AttributeSource.Text)
            .Select(match => "SqlSource." + match.Groups["name"].Value);

        AttributeSource.GeneratedTypes.ShouldBe(declared, ignoreOrder: true);
    }

    // The file goes into every project that references the package, so a project on an older language gets it too.
    [Fact]
    public void Text_CompiledAsCSharp73_DeclaresUnexpectedRowCountException()
    {
        var compilation = CompileText();

        compilation.GetDiagnostics(TestContext.Current.CancellationToken).ShouldBeEmpty();
        var exception = compilation
            .GetTypeByMetadataName(AttributeSource.UnexpectedRowCountMetadataName)
            .ShouldNotBeNull();
        exception.BaseType!.ToDisplayString().ShouldBe("System.InvalidOperationException");
    }

    [Theory]
    [InlineData(false, "The query 'GetUser' returned no rows.")]
    [InlineData(true, "The query 'GetUser' returned more than one row.")]
    public void UnexpectedRowCountException_Created_NamesTheQueryAndWhatWentWrong(bool moreThanOne, string message)
    {
        using var image = new MemoryStream();
        CompileText().Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        var type = Assembly
            .Load(image.ToArray())
            .GetType(AttributeSource.UnexpectedRowCountMetadataName)
            .ShouldNotBeNull();

        var exception = (InvalidOperationException)Activator.CreateInstance(type, "GetUser", moreThanOne)!;

        exception.Message.ShouldBe(message);
        type.GetProperty("QueryName")!.GetValue(exception).ShouldBe("GetUser");
    }

    private static CSharpCompilation CompileText() =>
        CSharpCompilation.Create(
            "AttributeText",
            [CSharpSyntaxTree.ParseText(AttributeSource.Text, new CSharpParseOptions(LanguageVersion.CSharp7_3))],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(RoslynOutputKind.DynamicallyLinkedLibrary)
        );

    [GeneratedRegex(@"internal (?:enum|sealed class) (?<name>\w+)")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^\s*(?<name>\w+) = (?<value>\d+),", RegexOptions.Multiline)]
    private static partial Regex Member();
}
