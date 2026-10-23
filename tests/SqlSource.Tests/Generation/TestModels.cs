using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Tests.Generation;

// Builds the generator's models by hand, for tests of the steps that take them.
internal static class TestModels
{
    public const string SourceFile = "/app/Repo/UserRepository.cs";

    public static LocationInfo AttributeLocation { get; } =
        new(SourceFile, new TextSpan(20, 10), new LinePositionSpan(new LinePosition(2, 1), new LinePosition(2, 11)));

    public static EquatableArray<T> Array<T>(params T[] items)
        where T : IEquatable<T> => new(ImmutableArray.Create(items));

    public static TargetType Type(
        string? path = null,
        MemberPlacement placement = MemberPlacement.Nested,
        string filePath = SourceFile,
        string @namespace = "App",
        TypeDeclaration[]? types = null,
        DiagnosticInfo[]? diagnostics = null,
        SettingsLevel? settings = null,
        bool isPublic = true
    ) =>
        new(
            @namespace,
            Array(types ?? [new TypeDeclaration("class", "UserRepository", "UserRepository", string.Empty, 0)]),
            placement,
            MethodPlacement.ExtensionClass,
            settings ?? SettingsLevel.None,
            isPublic,
            path,
            filePath,
            AttributeLocation,
            Array(diagnostics ?? [])
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
            Array(Literal(sql)),
            keptSql is null ? null : Array(Literal(keptSql)),
            EquatableArray<SqlToken>.Empty,
            EquatableArray<SqlQueryParameter>.Empty,
            markers ?? SettingsLevel.None,
            null,
            null,
            EquatableArray<ColumnMarker>.Empty
        );

    // Where "-- name: GetUser" on the first line of the file puts its name.
    public static LocationInfo NameLocation(string file) =>
        new(
            "/app/Repo/" + file,
            new TextSpan(9, 7),
            new LinePositionSpan(new LinePosition(0, 9), new LinePosition(0, 16))
        );
}
