using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Placement;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using SqlSource.Tests.Generation.Emitting.QueryMethods;
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

    // The collection method is an overload of its query method, so a call can tell them apart.
    [Fact]
    public void Of_BatchMethod_DiffersFromItsQueryMethod()
    {
        var query = QueryMethodWriterTests.GetUser;
        var batch = query with { Batch = new BatchCall(TransactionMode.On) };

        KeyOf(batch).ShouldBe("global::System.Collections.Generic.IEnumerable<global::App.GetUserParams>,string");
        KeyOf(batch).ShouldNotBe(KeyOf(query));
    }

    private static string KeyOf(QueryMethod method)
    {
        var methods = new TypeMethods(
            "App.Sample",
            "App.Sample.Methods.g.cs",
            "App",
            "App",
            TestSidecars.Of(new TypeDeclaration("class", "Sample", "Sample", string.Empty, 0)),
            "SampleExtensions",
            true,
            null,
            false,
            EquatableArray<NestedClassPlacement>.Empty,
            false,
            TestModels.NameLocation("Sample.cs"),
            TestSidecars.Of(method),
            EquatableArray<DiagnosticInfo>.Empty,
            EquatableArray<SettingsFileLibraryUse>.Empty,
            MethodEngines.None,
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
