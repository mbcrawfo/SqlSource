using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Generation.Emitting;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Reads the <see cref="RepositoryShape" /> of each type that has a repository query, as <see cref="RepositoryTypes" />
/// says, from the compilation.  It runs on every compilation, since a type's base types and the name of its interface
/// may be declared in files that no edit of the type's own file touches, and it gives an equal value until a candidate
/// or the interface's settings change.  A type with no repository query costs it nothing.
/// </summary>
internal static class RepositoryShapeReader
{
    /// <summary>
    /// The shape of each of the types, sorted ordinally by <see cref="RepositoryShape.TypeKey" />.
    /// </summary>
    /// <param name="types">The types that have a repository query, from <see cref="RepositoryTypes" />.</param>
    /// <param name="compilation">The compilation the types are in.</param>
    /// <param name="interfacesOn">The project's <c>SqlSourceRepositoryInterfaces</c>.</param>
    /// <param name="globalMarks">
    /// The project's marks of global connections, from <see cref="GlobalMarkReader" />.
    /// </param>
    /// <param name="token">Cancels the read.</param>
    public static EquatableArray<RepositoryShape> Read(
        EquatableArray<TargetType> types,
        Compilation compilation,
        bool interfacesOn,
        EquatableArray<GlobalMark> globalMarks,
        CancellationToken token
    )
    {
        var shapes = ImmutableArray.CreateBuilder<RepositoryShape>(types.Count);
        foreach (var type in types)
        {
            token.ThrowIfCancellationRequested();
            var symbol = compilation.GetTypeByMetadataName(type.MetadataName);
            var interfaceOn = type.GenerateInterface ?? interfacesOn;
            var interfaceName = type.InterfaceName ?? ("I" + type.Name);
            var heldParameters = EquatableArray<string>.Empty;
            shapes.Add(
                new RepositoryShape(
                    HintName.NameOf(type),
                    symbol is null || type.IsStatic
                        ? EquatableArray<ConnectionCandidate>.Empty
                        : ConnectionCandidateReader.Read(
                            symbol,
                            compilation,
                            type.AttributeLocation,
                            out heldParameters,
                            token
                        ),
                    symbol is null || type.IsStatic
                        ? EquatableArray<ConnectionCandidate>.Empty
                        : HeldFactoryReader.Read(symbol, compilation, type.AttributeLocation, token),
                    symbol is null || type.IsStatic
                        ? EquatableArray<ConnectionCandidate>.Empty
                        : GlobalConnections.CandidatesFor(globalMarks, symbol, compilation),
                    heldParameters,
                    interfaceOn,
                    interfaceName,
                    symbol is not null && interfaceOn ? FindTaken(symbol, interfaceName, compilation, token) : null,
                    symbol is not null && type.IsNestedInAGenericType ? FindSimpleNameTaker(symbol, compilation) : null
                )
            );
        }

        return new EquatableArray<RepositoryShape>(
            shapes.OrderBy(static shape => shape.TypeKey, StringComparer.Ordinal).ToImmutableArray()
        );
    }

    // What the type's simple name means inside it, when that is not the type: the nearest members of the name and the
    // type's arity that the type can reach, its own or inherited, as C# looks up a simple name.  A field or property
    // whose type is the type itself is C#'s Color Color rule, through which the name still reaches its static members.
    private static string? FindSimpleNameTaker(INamedTypeSymbol type, Compilation compilation)
    {
        for (var each = type; each is not null; each = each.BaseType)
        {
            var members = each.GetMembers(type.Name)
                .Where(member => HasTheArity(member, type.Arity) && compilation.IsSymbolAccessibleWithin(member, type))
                .ToList();
            if (members.Count > 0)
            {
                return IsOfTheType(members[0], type) ? null : DescribeMember(members[0]);
            }
        }

        return null;
    }

    // Whether a simple name of the arity finds the member: a generic method answers a name with no type arguments too.
    private static bool HasTheArity(ISymbol member, int arity) =>
        member switch
        {
            INamedTypeSymbol nested => nested.Arity == arity,
            IMethodSymbol method => arity == 0 || method.Arity == arity,
            _ => arity == 0,
        };

    private static bool IsOfTheType(ISymbol member, INamedTypeSymbol type) =>
        member switch
        {
            IFieldSymbol field => SymbolEqualityComparer.Default.Equals(field.Type, type),
            IPropertySymbol property => SymbolEqualityComparer.Default.Equals(property.Type, type),
            _ => false,
        };

    private static string DescribeMember(ISymbol member) =>
        "the "
        + (member is INamedTypeSymbol type ? KindOf(type) : MemberKindOf(member))
        + " "
        + member.ToDisplayString();

    // What takes the interface's full name, in the type's namespace or the type it is nested in, with the type's arity:
    // a type of the project's own assembly, unless it is an interface that every declaration of makes partial, which
    // the generated part merges with; a type of a referenced assembly that the project sees, whose name the interface
    // would hide; or, in the containing type, a member of the name.
    private static string? FindTaken(
        INamedTypeSymbol type,
        string name,
        Compilation compilation,
        CancellationToken token
    )
    {
        var metadataName =
            PrefixOf(type) + name + (type.Arity > 0 ? "`" + type.Arity.ToString(CultureInfo.InvariantCulture) : "");
        var types = compilation.GetTypesByMetadataName(metadataName);
        var own = types.FirstOrDefault(each =>
            SymbolEqualityComparer.Default.Equals(each.ContainingAssembly, compilation.Assembly)
            && !IsPartialInterface(each, token)
        );
        if (own is not null)
        {
            return "the " + KindOf(own) + " " + own.ToDisplayString();
        }

        var referenced = types.FirstOrDefault(each =>
            !SymbolEqualityComparer.Default.Equals(each.ContainingAssembly, compilation.Assembly)
            && compilation.IsSymbolAccessibleWithin(each, compilation.Assembly)
        );
        if (referenced is not null)
        {
            return "the "
                + KindOf(referenced)
                + " "
                + referenced.ToDisplayString()
                + " of the assembly "
                + referenced.ContainingAssembly.Name;
        }

        var member = type.ContainingType?.GetMembers(name).FirstOrDefault(static each => each is not INamedTypeSymbol);
        return member is null ? null : "the " + MemberKindOf(member) + " " + member.ToDisplayString();
    }

    // What the metadata name of a type beside the type starts with: its container's and a +, or its namespace's and a
    // dot, or nothing in the global namespace.
    private static string PrefixOf(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } container)
        {
            return TargetTypeReader.MetadataNameOf(container) + "+";
        }

        return type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? TargetTypeReader.MetadataNameOf(ns) + "."
            : string.Empty;
    }

    private static string MemberKindOf(ISymbol member) =>
        member switch
        {
            IMethodSymbol => "method",
            IPropertySymbol => "property",
            IEventSymbol => "event",
            _ => "field",
        };

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
