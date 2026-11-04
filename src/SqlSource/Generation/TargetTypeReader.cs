using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;

namespace SqlSource.Generation;

/// <summary>
/// Reads a type marked with the generated attribute into a <see cref="TargetType" />.  It is one of the few steps that
/// look at symbols and syntax, beside the check of the target framework, <c>NpgsqlProbe</c>, <c>SqlClientProbe</c>,
/// <c>MappedTypeResolver</c>, <c>ConverterReader</c> and <c>ModelNameChecker</c>; every step after them works on data.
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

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var types = ReadDeclarations(declaration, diagnostics);
        return new TargetType(
            symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(NamespaceFormat),
            new EquatableArray<TypeDeclaration>(types),
            IsPublic(symbol),
            IsReachableAsProtected(symbol),
            symbol.IsStatic,
            symbol.IsSealed,
            ReadSource(attribute),
            reference.SyntaxTree.FilePath,
            LocationInfo.From(reference.GetSyntax(cancellationToken).GetLocation()),
            GetTakenNames(symbol),
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

    // A member's accessibility domain leaves the assembly unless the member or a type around it is internal, private or
    // private protected, which keep it inside.  A protected or protected internal level lets in a derived type of
    // another assembly, so a type whose levels are all public but for those is reached from there, as protected.
    private static bool IsReachableAsProtected(INamedTypeSymbol symbol)
    {
        var isPublic = true;
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            var accessibility = type.DeclaredAccessibility;
            if (accessibility is Accessibility.Protected or Accessibility.ProtectedOrInternal)
            {
                isPublic = false;
            }
            else if (accessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return !isPublic;
    }

    // A nested class, or a member on the type, cannot share its name with a member of the type, with the type itself
    // or with one of the type's parameters.  The names of the nested classes are not known until the files are parsed,
    // so every one of them is kept, sorted, for the placement of the queries and for the methods' check.  MemberNames
    // leaves out the nested types.
    private static EquatableArray<string> GetTakenNames(INamedTypeSymbol symbol) =>
        new(
            symbol
                .MemberNames.Concat(symbol.GetTypeMembers().Select(static type => type.Name))
                .Concat(symbol.TypeParameters.Select(static parameter => parameter.Name))
                .Append(symbol.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToImmutableArray()
        );

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

    private static string? ReadSource(AttributeData attribute) =>
        GetNamedArgument(attribute, AttributeSource.SourceProperty) is { Value: string { Length: > 0 } source }
            ? source
            : null;

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
