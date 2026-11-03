using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
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

    // With no name dropped, both methods of the place are written.
    [Fact]
    public void Emit_NothingDropped_KeepsBoth()
    {
        var source = Emit(EquatableArray<string>.Empty).ShouldNotBeNull();

        source.ShouldContain(" GetUser(\n");
        source.ShouldContain(" ListUsers(\n");
    }

    // A type whose every method is dropped gets no file.
    [Fact]
    public void Emit_EveryMethodDropped_GivesNoSource() =>
        Emit(new EquatableArray<string>(["GetUser", "ListUsers"])).ShouldBeNull();

    // The source of an extension class that holds GetUser and ListUsers, without the dropped names.
    private static string? Emit(EquatableArray<string> dropped)
    {
        var methods = new TypeMethods(
            "App.Sample",
            "App.Sample.Methods.g.cs",
            MethodHost.Of(MethodPlacement.ExtensionClass),
            "App",
            "App",
            TestSidecars.Of(new TypeDeclaration("class", "Sample", "Sample", string.Empty, 0)),
            "SampleExtensions",
            true,
            TestModels.NameLocation("Sample.cs"),
            TestSidecars.Of(QueryMethodWriterTests.GetUser, ListUsers),
            EquatableArray<DiagnosticInfo>.Empty,
            EquatableArray<SettingsFileLibraryUse>.Empty,
            CodeGenEngines.None,
            EquatableArray<string>.Empty,
            EquatableArray<LibraryValue>.Empty,
            EquatableArray<LibraryUse>.Empty
        );

        return MethodEmitter.Emit(methods, dropped, false).Source;
    }
}
