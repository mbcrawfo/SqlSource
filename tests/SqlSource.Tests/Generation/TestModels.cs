using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;
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
        SettingsLevel? settings = null
    ) =>
        new(
            @namespace,
            Array(types ?? [new TypeDeclaration("class", "UserRepository", "UserRepository", string.Empty, 0)]),
            placement,
            MethodPlacement.ExtensionClass,
            settings ?? SettingsLevel.None,
            path,
            filePath,
            AttributeLocation,
            Array(diagnostics ?? [])
        );
}
