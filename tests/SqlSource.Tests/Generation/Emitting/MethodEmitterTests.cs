using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Placement;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using SqlSource.Tests.Generation.Emitting.QueryMethods;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class MethodEmitterTests
{
    // QueryMethodWriterTests' GetUser under another name, at the same place, as a query that gives two methods would
    // have it.
    private static readonly QueryMethod ListUsers = QueryMethodWriterTests.GetUser with { Name = "ListUsers" };

    private static readonly TypeDeclaration Sample = new("class", "Sample", "Sample", string.Empty, 0);

    // A method is dropped by its name, so the other method of its query's place is kept.
    [Fact]
    public void Emit_DroppedNameOfOneOfTwoMethodsOfOnePlace_KeepsTheOther()
    {
        var source = Emit(new EquatableArray<string>(["GetUser"])).ShouldNotBeNull();

        source.ShouldContain(" ListUsers(\n");
        source.ShouldNotContain(" GetUser(\n");
    }

    [Fact]
    public void Emit_NothingDropped_KeepsBoth()
    {
        var source = Emit(EquatableArray<string>.Empty).ShouldNotBeNull();

        source.ShouldContain(" GetUser(\n");
        source.ShouldContain(" ListUsers(\n");
    }

    [Fact]
    public void Emit_EveryMethodDropped_GivesNoSource() =>
        Emit(new EquatableArray<string>(["GetUser", "ListUsers"])).ShouldBeNull();

    // A repository whose every method failed still gets its interface, so the file holds the type's part, which
    // implements it, and the interface, empty.
    [Fact]
    public void Emit_InterfaceWithoutMethods_WritesTheTypeAndTheEmptyInterface() =>
        MethodEmitter
            .Emit(Of([Sample], [], "ISample", true), EquatableArray<string>.Empty, false)
            .Source.ShouldBe(
                GeneratedFile.Header
                    + "\nnamespace App\n{\n"
                    + "    partial class Sample : global::App.ISample\n    {\n    }\n\n"
                    + "    /// <summary>\n"
                    + "    /// The public repository methods of <c>App.Sample</c>, which it implements.\n"
                    + "    /// </summary>\n"
                    + "    public partial interface ISample\n    {\n    }\n}\n"
            );

    // The interface is the type's sibling in the class it is nested in, and the type's part names it through that
    // class, with the class's type parameters and its own.
    [Fact]
    public void Emit_RepositoryNestedInAGenericClass_ImplementsItsSibling()
    {
        var outer = new TypeDeclaration("class", "Outer", "Outer", "<U>", 1);
        var repo = new TypeDeclaration("class", "Repo", "Repo", "<T>", 1);

        var source = MethodEmitter
            .Emit(Of([outer, repo], [RepositoryMethodWriterTests.Repository], "IRepo", false), default, false)
            .Source.ShouldNotBeNull();

        source.ShouldContain(
            "    partial class Outer<U>\n    {\n        partial class Repo<T> : global::App.Outer<U>.IRepo<T>\n"
        );
        source.ShouldContain("        internal partial interface IRepo<T>\n        {\n");
        source.ShouldContain(
            "            global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(\n"
                + "                global::App.GetUserParams parameters,\n"
                + "                global::System.Threading.CancellationToken cancellationToken = default);\n"
                + "        }\n    }\n}\n"
        );
    }

    // The interface follows the type's part, and the extension class follows both.
    [Fact]
    public void Emit_InterfaceAndExtensionClass_WritesTheInterfaceBetween()
    {
        var source = MethodEmitter
            .Emit(Of([Sample], [RepositoryMethodWriterTests.Repository, ListUsers], "ISample", true), default, false)
            .Source.ShouldNotBeNull();

        var type = source.IndexOf("    partial class Sample : global::App.ISample\n", System.StringComparison.Ordinal);
        var contract = source.IndexOf("    public partial interface ISample\n", System.StringComparison.Ordinal);
        var extensions = source.IndexOf(" static partial class SampleExtensions\n", System.StringComparison.Ordinal);
        type.ShouldBeGreaterThanOrEqualTo(0);
        contract.ShouldBeGreaterThan(type);
        extensions.ShouldBeGreaterThan(contract);
    }

    // The source of an extension class that holds GetUser and ListUsers, without the dropped names.
    private static string? Emit(EquatableArray<string> dropped) =>
        MethodEmitter
            .Emit(Of([Sample], [QueryMethodWriterTests.GetUser, ListUsers], null, false), dropped, false)
            .Source;

    // The methods of a type of the namespace App, nested in the types before its own.
    private static TypeMethods Of(
        TypeDeclaration[] types,
        QueryMethod[] methods,
        string? interfaceName,
        bool interfaceIsPublic
    ) =>
        new(
            "App.Sample",
            "App.Sample.Methods.g.cs",
            "App",
            "App",
            TestSidecars.Of(types),
            "SampleExtensions",
            true,
            interfaceName,
            interfaceIsPublic,
            EquatableArray<NestedClassPlacement>.Empty,
            false,
            TestModels.NameLocation("Sample.cs"),
            TestSidecars.Of(methods),
            EquatableArray<DiagnosticInfo>.Empty,
            EquatableArray<SettingsFileLibraryUse>.Empty,
            MethodEngines.None,
            EquatableArray<string>.Empty,
            EquatableArray<LibraryValue>.Empty,
            EquatableArray<LibraryUse>.Empty
        );
}
