using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;

namespace SqlSource.Tests.Generation;

// Builds the generator's models by hand, for tests of the steps that take them.
internal static class TestModels
{
    public const string SourceFile = "/app/Repo/UserRepository.cs";

    // A level that turns these generator parameters on and says nothing else.
    public static SettingsLevel ParametersOn(GeneratorParameters parameters) =>
        new() { Parameters = TurnedOn(parameters) };

    // The generator parameters of a level that turns these on and names no other.
    public static GeneratorParameterChoices TurnedOn(GeneratorParameters parameters) =>
        GeneratorParameterChoices.None.With(parameters, GeneratorParameterState.On);

    // A level that turns these generator parameters off and says nothing else.
    public static SettingsLevel ParametersOff(GeneratorParameters parameters) =>
        new() { Parameters = TurnedOff(parameters) };

    // The generator parameters of a level that turns these off and names no other.
    public static GeneratorParameterChoices TurnedOff(GeneratorParameters parameters) =>
        GeneratorParameterChoices.None.With(parameters, GeneratorParameterState.Off);

    public static LocationInfo AttributeLocation { get; } =
        new(SourceFile, new TextSpan(20, 10), new LinePositionSpan(new LinePosition(2, 1), new LinePosition(2, 11)));

    public static TargetType Type(
        string? source = null,
        string filePath = SourceFile,
        string @namespace = "App",
        TypeDeclaration[]? types = null,
        DiagnosticInfo[]? diagnostics = null,
        bool isPublic = true,
        bool isReachableAsProtected = false,
        bool isStatic = false,
        bool isSealed = false,
        string[]? takenNames = null
    ) =>
        new(
            @namespace,
            TestSidecars.Of(
                types ?? [new TypeDeclaration("class", "UserRepository", "UserRepository", string.Empty, 0)]
            ),
            @namespace + "." + (types?[^1].ValueName ?? "UserRepository"),
            isPublic,
            isReachableAsProtected,
            isStatic,
            isSealed,
            source,
            null,
            null,
            filePath,
            AttributeLocation,
            TestSidecars.Of(takenNames ?? [types?[^1].ValueName ?? "UserRepository"]),
            TestSidecars.Of(diagnostics ?? [])
        );

    public static SqlSegment Literal(string text) => new(SqlSegmentKind.Literal, text);

    // A query whose SQL is one literal, with no token, parameter or model.  Its name is at the name location of
    // Users.sql unless the test gives another.
    public static SqlQuery Query(
        string name,
        string sql,
        string? summary = null,
        LocationInfo? nameLocation = null,
        string? keptSql = null,
        SettingsLevel? markers = null
    ) =>
        new(
            name,
            nameLocation ?? NameLocation("Users.sql"),
            summary,
            null,
            TestSidecars.Of(Literal(sql)),
            keptSql is null ? null : TestSidecars.Of(Literal(keptSql)),
            EquatableArray<SqlToken>.Empty,
            EquatableArray<SqlQueryParameter>.Empty,
            markers ?? SettingsLevel.None,
            null,
            null,
            EquatableArray<ColumnMarker>.Empty,
            EquatableArray<DiagnosticInfo>.Empty
        );

    // Where "-- name: GetUser" on the first line of the file puts its name.
    public static LocationInfo NameLocation(string file) =>
        new(
            "/app/Repo/" + file,
            new TextSpan(9, 7),
            new LinePositionSpan(new LinePosition(0, 9), new LinePosition(0, 16))
        );
}
