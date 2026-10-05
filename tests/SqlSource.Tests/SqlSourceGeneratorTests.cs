using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public class SqlSourceGeneratorTests
{
    [Fact]
    public void RunGenerators_WithTrivialSource_ProducesNoOutputAndNoDiagnostics()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("public class Sample;", cancellationToken: cancellationToken)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var result = CSharpGeneratorDriver
            .Create(new SqlSourceGenerator())
            .RunGenerators(compilation, cancellationToken)
            .GetRunResult();

        result.GeneratedTrees.ShouldBeEmpty();
        result.Diagnostics.ShouldBeEmpty();
    }
}
