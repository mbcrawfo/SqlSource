using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// Reads a type marked with the generated attribute into a <see cref="TargetType" />.  This and the step that checks
/// the target framework are the only places the generator looks at symbols and syntax; everything after them works on
/// data.
/// </summary>
internal static class TargetTypeReader
{
    private static readonly SymbolDisplayFormat NamespaceFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    /// <summary>
    /// Whether a node that carries the attribute is a declaration the generator can extend.
    /// </summary>
    public static bool IsCandidate(SyntaxNode node) =>
        node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax;

    /// <summary>
    /// Reads the type.  Returns null for every declaration but the one that carries the type's first attribute, so
    /// that a type with the attribute on two partial declarations, which the compiler rejects, gets one file.
    /// </summary>
    public static TargetType? Read(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var attribute = context.Attributes[0];
        if (!IsFirstAttributeOfType(symbol, attribute) || attribute.ApplicationSyntaxReference is not { } reference)
        {
            return null;
        }

        var attributeLocation = LocationInfo.From(reference.GetSyntax(cancellationToken).GetLocation());
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var source = new AttributeSettings(attribute, attributeLocation, diagnostics);
        var types = ReadDeclarations(declaration, diagnostics);
        var placement = source.Choice<MemberPlacement>(AttributeSource.LocationProperty) ?? MemberPlacement.Nested;
        if (placement == MemberPlacement.Nested && IsNestedClassNameTaken(symbol))
        {
            diagnostics.Add(DiagnosticInfo.Create(SqlDiagnostics.SqlMemberExists, attributeLocation, symbol.Name));
        }

        return new TargetType(
            symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(NamespaceFormat),
            new EquatableArray<TypeDeclaration>(types),
            placement,
            source.Choice<MethodPlacement>(AttributeSource.MethodLocationProperty) ?? MethodPlacement.ExtensionClass,
            SettingsLevelReader.Read(ref source),
            IsPublic(symbol),
            ReadPath(attribute),
            reference.SyntaxTree.FilePath,
            attributeLocation,
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
        );
    }

    // A model is public or internal, so the declared accessibility of the type and of each type it is nested in is
    // all that decides it: anything but public makes the type, and so its models, internal at most.
    private static bool IsPublic(INamedTypeSymbol symbol)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    // A nested class cannot share its name with a member of the type, with the type itself or with one of the
    // type's parameters.  Each is a compiler error in generated code unless it is reported here first.
    private static bool IsNestedClassNameTaken(INamedTypeSymbol symbol) =>
        symbol.Name == TypeEmitter.NestedClassName
        || symbol.TypeParameters.Any(static parameter => parameter.Name == TypeEmitter.NestedClassName)
        || !symbol.GetMembers(TypeEmitter.NestedClassName).IsEmpty;

    private static bool IsFirstAttributeOfType(INamedTypeSymbol symbol, AttributeData attribute)
    {
        var first = symbol
            .GetAttributes()
            .FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attribute.AttributeClass)
            );
        return first?.ApplicationSyntaxReference is { } firstReference
            && attribute.ApplicationSyntaxReference is { } reference
            && firstReference.SyntaxTree == reference.SyntaxTree
            && firstReference.Span == reference.Span;
    }

    // The type and the types it is nested in, outermost first, with a diagnostic for each one that generated code
    // cannot add to.
    private static ImmutableArray<TypeDeclaration> ReadDeclarations(
        TypeDeclarationSyntax declaration,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var chain = new Stack<TypeDeclarationSyntax>();
        for (SyntaxNode? node = declaration; node is TypeDeclarationSyntax type; node = node.Parent)
        {
            chain.Push(type);
        }

        var types = ImmutableArray.CreateBuilder<TypeDeclaration>(chain.Count);
        foreach (var type in chain)
        {
            var location = LocationInfo.From(type.Identifier.GetLocation());
            if (!type.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(SqlDiagnostics.TypeNotPartial, location, type.Identifier.ValueText)
                );
            }

            if (type.Modifiers.Any(SyntaxKind.FileKeyword))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(SqlDiagnostics.TypeIsFileLocal, location, type.Identifier.ValueText)
                );
            }

            types.Add(
                new TypeDeclaration(
                    GetKeyword(type),
                    type.Identifier.Text,
                    type.Identifier.ValueText,
                    GetTypeParameters(type.TypeParameterList),
                    type.TypeParameterList?.Parameters.Count ?? 0
                )
            );
        }

        return types.MoveToImmutable();
    }

    // "record" and not "record class": the short form compiles on every language version that has records.
    private static string GetKeyword(TypeDeclarationSyntax type) =>
        type is RecordDeclarationSyntax record && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
            ? "record struct"
            : type.Keyword.Text;

    // Partial declarations must agree on the names of their type parameters, and on nothing else.  Variance never
    // appears: a variant interface cannot contain a class, a struct or a record.
    private static string GetTypeParameters(TypeParameterListSyntax? list) =>
        list is null
            ? string.Empty
            : "<" + string.Join(", ", list.Parameters.Select(static parameter => parameter.Identifier.Text)) + ">";

    private static string? ReadPath(AttributeData attribute) =>
        GetNamedArgument(attribute, AttributeSource.PathProperty) is { Value: string { Length: > 0 } path }
            ? path
            : null;

    // The attribute as a source of settings.  A word that is no generator parameter is reported, and a type with a
    // diagnostic gets no members, so nothing of this level applies then; every property is still read, so that all
    // the problems of the attribute are reported together.
    private readonly struct AttributeSettings(
        AttributeData attribute,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    ) : ISettingSource
    {
        public SettingPlace Place => SettingPlace.Attribute;

        // A string that is empty or white space is not set, as Path is: the reader of the level says so.
        public string? Text(SettingId setting) =>
            PropertyOf(setting) is { } property && GetNamedArgument(attribute, property) is { Value: string written }
                ? written
                : null;

        public T? Choice<T>(SettingId setting)
            where T : struct, Enum => PropertyOf(setting) is { } property ? Choice<T>(property) : null;

        // An enum argument arrives as its number.  A number that is no member of the generator's own form of the
        // enum was written with a cast, and is reported.  An argument that is not a constant has no value here; the
        // compiler reports it.
        public T? Choice<T>(string property)
            where T : struct, Enum
        {
            if (GetNamedArgument(attribute, property) is not { Value: int value })
            {
                return null;
            }

            if (Enum.IsDefined(typeof(T), value))
            {
                return (T)Enum.ToObject(typeof(T), value);
            }

            Report(property, value.ToString(CultureInfo.InvariantCulture));
            return null;
        }

        public void Invalid(SettingId setting, string value)
        {
            if (PropertyOf(setting) is { } property)
            {
                Report(property, value);
            }
        }

        private void Report(string property, string value) =>
            diagnostics.Add(
                DiagnosticInfo.Create(SqlDiagnostics.InvalidAttributeValue, attributeLocation, value, property)
            );

        private static string? PropertyOf(SettingId setting) =>
            setting switch
            {
                SettingId.Parameters => AttributeSource.ParametersProperty,
                SettingId.Output => AttributeSource.OutputProperty,
                // The attribute has no property for the database.
                SettingId.Database => null,
                SettingId.InputModelSuffix => AttributeSource.InputModelSuffixProperty,
                SettingId.OutputModelSuffix => AttributeSource.OutputModelSuffixProperty,
                SettingId.ModelNamespace => AttributeSource.ModelNamespaceProperty,
                SettingId.InputModelType => AttributeSource.InputModelTypeProperty,
                SettingId.OutputModelType => AttributeSource.OutputModelTypeProperty,
                SettingId.CollectionType => AttributeSource.CollectionTypeProperty,
                // The type override has no attribute level.
                SettingId.TypeOverrides => null,
                _ => null,
            };
    }

    private static TypedConstant? GetNamedArgument(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Kind != TypedConstantKind.Error)
            {
                return argument.Value;
            }
        }

        return null;
    }
}
