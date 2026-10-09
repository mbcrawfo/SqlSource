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
using SqlSource.Settings;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Generation;

/// <summary>
/// Reads a type marked with the generated attribute into a <see cref="TargetType" />.  This is the only place the
/// generator looks at symbols and syntax; everything after it works on data.
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
        var types = ReadDeclarations(declaration, diagnostics);
        var placement = ReadPlacement(attribute, attributeLocation, diagnostics);
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
            ReadSettings(attribute, attributeLocation, diagnostics),
            ReadPath(attribute),
            reference.SyntaxTree.FilePath,
            attributeLocation,
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
        );
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

    private static MemberPlacement ReadPlacement(
        AttributeData attribute,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        // An argument that is not a constant has no value here.  The compiler reports it.
        if (GetNamedArgument(attribute, AttributeSource.LocationProperty) is not { Value: int value })
        {
            return MemberPlacement.Nested;
        }

        switch (value)
        {
            case 0:
                return MemberPlacement.Nested;
            case 1:
                return MemberPlacement.Direct;
            default:
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.InvalidAttributeValue,
                        attributeLocation,
                        value.ToString(CultureInfo.InvariantCulture),
                        AttributeSource.LocationProperty
                    )
                );
                return MemberPlacement.Nested;
        }
    }

    // A word that is no generator parameter is reported, and the other words apply.  A value without text is a
    // property that is not set.
    private static SettingsLevel ReadSettings(
        AttributeData attribute,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var level = SettingsLevel.None;
        if (GetNamedArgument(attribute, AttributeSource.ParametersProperty) is { Value: string list })
        {
            var words = new List<string>();
            if (GeneratorParameterList.Parse(list, words) is { } parameters)
            {
                level = level with { Parameters = parameters };
            }

            foreach (var word in words)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.InvalidAttributeValue,
                        attributeLocation,
                        word,
                        AttributeSource.ParametersProperty
                    )
                );
            }
        }

        if (
            ReadChoice<OutputKind>(attribute, AttributeSource.OutputProperty, attributeLocation, diagnostics) is
            { } output
        )
        {
            level = level with { Output = output };
        }

        return level;
    }

    // An enum argument arrives as its number.  A number that is no member of the generator's own form of the enum
    // was written with a cast, and is reported.  An argument that is not a constant has no value here; the compiler
    // reports it.
    private static T? ReadChoice<T>(
        AttributeData attribute,
        string property,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
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

        diagnostics.Add(
            DiagnosticInfo.Create(
                SqlDiagnostics.InvalidAttributeValue,
                attributeLocation,
                value.ToString(CultureInfo.InvariantCulture),
                property
            )
        );
        return null;
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
