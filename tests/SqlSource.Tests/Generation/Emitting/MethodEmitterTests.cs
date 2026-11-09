using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Placement;
using SqlSource.Testing.Snapshot;
using SqlSource.Tests.Generation.Emitting.QueryMethods;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class MethodEmitterTests
{
    // QueryMethodWriterTests' GetUser under another name, at the same place, as a query that gives two methods would
    // have it.
    private static readonly QueryMethod ListUsers = QueryMethodWriterTests.GetUser with { Name = "ListUsers" };

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

    // The source of an extension class that holds GetUser and ListUsers, without the dropped names.
    private static string? Emit(EquatableArray<string> dropped)
    {
        var methods = new TypeMethods(
            "App.Sample",
            "App.Sample.Methods.g.cs",
            "App",
            "App",
            TestSidecars.Of(new TypeDeclaration("class", "Sample", "Sample", string.Empty, 0)),
            "SampleExtensions",
            true,
            EquatableArray<NestedClassPlacement>.Empty,
            false,
            TestModels.NameLocation("Sample.cs"),
            TestSidecars.Of(QueryMethodWriterTests.GetUser, ListUsers),
            EquatableArray<DiagnosticInfo>.Empty,
            EquatableArray<SettingsFileLibraryUse>.Empty,
            MethodEngines.None,
            EquatableArray<string>.Empty,
            EquatableArray<LibraryValue>.Empty,
            EquatableArray<LibraryUse>.Empty
        );

        return MethodEmitter.Emit(methods, dropped, false).Source;
    }
}
