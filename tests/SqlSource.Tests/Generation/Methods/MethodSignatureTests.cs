using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using SqlSource.Tests.Generation.Emitting;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodSignatureTests
{
    [Fact]
    public void Of_InputWithTokens_KeyIsTheInputThenStringPerToken() =>
        KeyOf(QueryMethodWriterTests.GetUser).ShouldBe("global::App.GetUserParams,string");

    [Fact]
    public void Of_NoInputWithTokens_KeyIsCommaStringPerToken() =>
        KeyOf(
                QueryMethodWriterTests.GetUser with
                {
                    Segments = TestSidecars.Of(TestModels.Literal("SELECT 1 FROM "), Token("a"), Token("b")),
                    Input = null,
                }
            )
            .ShouldBe(",string,string");

    [Fact]
    public void Of_NoInputNoTokens_KeyIsEmpty() =>
        KeyOf(
                QueryMethodWriterTests.GetUser with
                {
                    Segments = TestSidecars.Of(TestModels.Literal("SELECT 1")),
                    Input = null,
                }
            )
            .ShouldBeEmpty();

    private static string KeyOf(QueryMethod method)
    {
        var methods = new TypeMethods(
            "App.Sample",
            "App.Sample.Methods.g.cs",
            MethodPlacement.ExtensionClass,
            "App",
            "App",
            TestSidecars.Of(new TypeDeclaration("class", "Sample", "Sample", string.Empty, 0)),
            "SampleExtensions",
            true,
            TestModels.NameLocation("Sample.cs"),
            TestSidecars.Of(method),
            EquatableArray<DiagnosticInfo>.Empty,
            EquatableArray<SettingsFileLibraryUse>.Empty,
            CodeGenEngines.None,
            EquatableArray<string>.Empty,
            EquatableArray<LibraryValue>.Empty,
            EquatableArray<LibraryUse>.Empty
        );

        var signatures = MethodSignature.Of(methods);

        signatures.Count.ShouldBe(1);
        return signatures[0].ParameterKey;
    }

    private static SqlSegment Token(string name) => new(SqlSegmentKind.Token, name);
}
