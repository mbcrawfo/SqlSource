using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Exceptions;
using Xunit;

namespace SqlSource.Tests.Generation.Exceptions;

// The exceptions' file goes into every project that references the package, unless a reference gives it the
// exceptions, so a project on an older language gets it too.
public sealed class ExceptionSourceTests
{
    [Theory]
    [InlineData(false, Accessibility.Internal)]
    [InlineData(true, Accessibility.Public)]
    public void TextFor_CompiledAsCSharp73_DeclaresTheExceptions(bool isPublic, Accessibility accessibility)
    {
        var compilation = CompileText(isPublic, Net6);

        compilation.GetDiagnostics(TestContext.Current.CancellationToken).ShouldBeEmpty();
        foreach (
            var (name, baseType) in new[]
            {
                (ExceptionSource.UnexpectedRowCountMetadataName, "System.InvalidOperationException"),
                (ExceptionSource.TransactionRolledBackMetadataName, "System.InvalidOperationException"),
                (ExceptionSource.CollectionItemMetadataName, "System.Data.Common.DbException"),
            }
        )
        {
            var exception = compilation.GetTypeByMetadataName(name).ShouldNotBeNull();
            exception.BaseType!.ToDisplayString().ShouldBe(baseType);
            exception.DeclaredAccessibility.ShouldBe(accessibility);
            exception.IsSealed.ShouldBeTrue();
        }
    }

    // The symbol that the SDK defines for a project that targets .NET 6 or later.
    private const string Net6 = "NET6_0_OR_GREATER";

    // A project on an older framework, whose DbException has none of the members, gets the exception without them.
    [Fact]
    public void TextFor_BeforeNet6_DeclaresCollectionItemExceptionWithoutTheOverrides()
    {
        var compilation = CompileText(isPublic: false);

        compilation.GetDiagnostics(TestContext.Current.CancellationToken).ShouldBeEmpty();
        var exception = compilation.GetTypeByMetadataName(ExceptionSource.CollectionItemMetadataName).ShouldNotBeNull();
        exception
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(property => property.Name)
            .ShouldBe(["QueryName", "ItemIndex"]);
    }

    [Fact]
    public void TextFor_CollectionItemException_DeclaresTheConstructorAndTheMembers()
    {
        var exception = CompileText(isPublic: false, Net6)
            .GetTypeByMetadataName(ExceptionSource.CollectionItemMetadataName)
            .ShouldNotBeNull();

        exception
            .InstanceConstructors.Single()
            .Parameters.Select(parameter => parameter.Type.ToDisplayString())
            .ShouldBe(["string", "int", "System.Data.Common.DbException"]);
        exception.GetMembers("QueryName").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("string");
        exception.GetMembers("ItemIndex").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("int");
        foreach (var name in new[] { "SqlState", "IsTransient", "DbBatchCommand" })
        {
            exception.GetMembers(name).OfType<IPropertySymbol>().Single().IsOverride.ShouldBeTrue(name);
        }
    }

    [Fact]
    public void UnexpectedRowCountException_CreatedForAnItem_NamesTheQueryAndTheItem()
    {
        var type = Load(ExceptionSource.UnexpectedRowCountMetadataName);

        var none = (InvalidOperationException)Activator.CreateInstance(type, "AddUser", false, 3)!;
        var many = (InvalidOperationException)Activator.CreateInstance(type, "AddUser", true, 3)!;
        var plain = (InvalidOperationException)Activator.CreateInstance(type, "AddUser", false)!;

        none.Message.ShouldBe("The query 'AddUser' returned no rows for item 3.");
        many.Message.ShouldBe("The query 'AddUser' returned more than one row for item 3.");
        type.GetProperty("ItemIndex")!.GetValue(none).ShouldBe(3);
        type.GetProperty("QueryName")!.GetValue(none).ShouldBe("AddUser");
        plain.Message.ShouldBe("The query 'AddUser' returned no rows.");
        type.GetProperty("ItemIndex")!.GetValue(plain).ShouldBeNull();
    }

    [Fact]
    public void CollectionItemException_Created_NamesTheQueryAndTheItemAndPassesOnTheInnerExceptionsMembers()
    {
        var type = Load(ExceptionSource.CollectionItemMetadataName);
        var inner = new FakeDbException("duplicate key", "23505", transient: true, errorCode: 2627);

        var exception = (DbException)Activator.CreateInstance(type, "AddUser", 3, inner)!;

        exception.Message.ShouldBe("The query 'AddUser' failed at item 3: duplicate key");
        exception.InnerException.ShouldBeSameAs(inner);
        exception.SqlState.ShouldBe("23505");
        exception.IsTransient.ShouldBeTrue();
        exception.ErrorCode.ShouldBe(inner.ErrorCode);
        exception.ErrorCode.ShouldBe(2627);
        type.GetProperty("QueryName")!.GetValue(exception).ShouldBe("AddUser");
        type.GetProperty("ItemIndex")!.GetValue(exception).ShouldBe(3);
    }

    [Fact]
    public void CollectionItemException_InnerWithNothingToPassOn_PassesOnNothing()
    {
        var type = Load(ExceptionSource.CollectionItemMetadataName);
        var inner = new FakeDbException("failed", sqlState: null, transient: false, errorCode: 0);

        var exception = (DbException)Activator.CreateInstance(type, "AddUser", 0, inner)!;

        exception.SqlState.ShouldBeNull();
        exception.IsTransient.ShouldBeFalse();
        exception.BatchCommand.ShouldBeNull();
    }

    // A driver's exception, which DbException lets a subclass give a state, a transience and an error code.  Only the
    // test creates it, so it has no standard constructors.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1194:Implement exception constructors")]
    private sealed class FakeDbException(string message, string? sqlState, bool transient, int errorCode)
        : DbException(message, errorCode)
    {
        public override string? SqlState => sqlState;

        public override bool IsTransient => transient;
    }

    // The internal text, the default, compiled and loaded.  Its constructors are public, so Activator calls them.
    private static Type Load(string metadataName)
    {
        using var image = new MemoryStream();
        CompileText(isPublic: false, Net6)
            .Emit(image, cancellationToken: TestContext.Current.CancellationToken)
            .Success.ShouldBeTrue();
        return Assembly.Load(image.ToArray()).GetType(metadataName).ShouldNotBeNull();
    }

    private static IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

    private static CSharpCompilation CompileText(bool isPublic, params string[] preprocessorSymbols) =>
        CSharpCompilation.Create(
            "ExceptionText",
            [
                CSharpSyntaxTree.ParseText(
                    ExceptionSource.TextFor(isPublic),
                    new CSharpParseOptions(
                        LanguageVersion.CSharp7_3,
                        DocumentationMode.Diagnose
                    ).WithPreprocessorSymbols(preprocessorSymbols)
                ),
            ],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
}
