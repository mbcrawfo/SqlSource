using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Generation.Emitting;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Reads each marked type's <see cref="RepositoryShape" /> from the compilation.  It runs on every compilation, since a
/// type's base types and the name of its interface may be declared in files that no edit of the type's own file
/// touches, and it gives an equal value until a candidate or the interface's settings change.
/// </summary>
internal static class RepositoryShapeReader
{
    /// <summary>
    /// The shape of each of the types, sorted ordinally by <see cref="RepositoryShape.TypeKey" />.
    /// </summary>
    /// <param name="types">Every type marked with the generated attribute.</param>
    /// <param name="compilation">The compilation the types are in.</param>
    /// <param name="interfacesOn">The project's <c>SqlSourceRepositoryInterfaces</c>.</param>
    /// <param name="token">Cancels the read.</param>
    public static EquatableArray<RepositoryShape> Read(
        ImmutableArray<TargetType> types,
        Compilation compilation,
        bool interfacesOn,
        CancellationToken token
    )
    {
        var shapes = ImmutableArray.CreateBuilder<RepositoryShape>(types.Length);
        foreach (var type in types)
        {
            token.ThrowIfCancellationRequested();
            var symbol = compilation.GetTypeByMetadataName(type.MetadataName);
            var interfaceOn = type.GenerateInterface ?? interfacesOn;
            var interfaceName = type.InterfaceName ?? ("I" + type.Name);
            shapes.Add(
                new RepositoryShape(
                    HintName.NameOf(type),
                    symbol is null || type.IsStatic
                        ? EquatableArray<ConnectionCandidate>.Empty
                        : ConnectionCandidateReader.Read(symbol, compilation, type.AttributeLocation, token),
                    interfaceOn,
                    interfaceName,
                    symbol is not null && interfaceOn ? FindTaken(symbol, interfaceName, token) : null
                )
            );
        }

        return new EquatableArray<RepositoryShape>(
            shapes.OrderBy(static shape => shape.TypeKey, StringComparer.Ordinal).ToImmutableArray()
        );
    }

    // What the project's own assembly declares where the interface goes, the type's namespace or the type it is nested
    // in, with the interface's name and the type's arity, unless it is an interface that every declaration of makes
    // partial, which the generated part merges with.  A type of a referenced assembly does not stop the interface
    // from compiling.
    private static string? FindTaken(INamedTypeSymbol type, string name, CancellationToken token)
    {
        var siblings = type.ContainingType is { } container
            ? container.GetTypeMembers(name, type.Arity)
            : type.ContainingNamespace.GetTypeMembers(name, type.Arity);
        var taken = siblings.FirstOrDefault(sibling =>
            SymbolEqualityComparer.Default.Equals(sibling.ContainingAssembly, type.ContainingAssembly)
            && !IsPartialInterface(sibling, token)
        );
        return taken is null ? null : "the " + KindOf(taken) + " " + taken.ToDisplayString();
    }

    private static bool IsPartialInterface(INamedTypeSymbol type, CancellationToken token) =>
        type.TypeKind == TypeKind.Interface
        && type.DeclaringSyntaxReferences.All(reference =>
            reference.GetSyntax(token) is InterfaceDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        );

    // The keyword that declares the type: a type that a namespace or a type declares is one of these.
    private static string KindOf(INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
        }

        if (type.TypeKind == TypeKind.Struct)
        {
            return "struct";
        }

        if (type.TypeKind == TypeKind.Interface)
        {
            return "interface";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return "enum";
        }

        return type.TypeKind == TypeKind.Delegate ? "delegate" : "class";
    }
}
