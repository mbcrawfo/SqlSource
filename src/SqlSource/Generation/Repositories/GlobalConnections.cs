using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Decides what the project's <see cref="GlobalMark" />s are over the compilation, since a mark's repository, the type
/// its member gives and who can reach it may be declared in another file: each broken one, which
/// <see cref="Check" /> reports once, and the valid ones a repository can reach, its
/// <see cref="CandidateTier.Global" /> candidates.  A global is broken when its member cannot give a connection, when
/// no type arguments can be chosen for a generic type around it, when it is a <c>static abstract</c> or
/// <c>static virtual</c> interface member, or when a type around it is file-local, which the generated code, in
/// another file, can never reach.  A static member of a type marked <c>[SqlSourceGenerate]</c> and a parameter of such
/// a type's primary constructor are the type's own, which <see cref="ConnectionCandidateReader" /> reads, and both
/// methods pass over them.  So is a static member of its base type, which both pass over for the repositories that
/// derive from that type.
/// </summary>
internal static class GlobalConnections
{
    private const string GenerateAttributeName = "SqlSourceGenerateAttribute";

    // App.Outer<T>.Db, as a message names a type in full.
    private static readonly SymbolDisplayFormat TypeNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
    );

    /// <summary>
    /// <c>SQLSRC032</c> at each broken mark, in the order of the marks: a parameter, but of the primary constructor of
    /// a type marked <c>[SqlSourceGenerate]</c>, and a broken global.  A static member of a type marked
    /// <c>[SqlSourceGenerate]</c> or of one of its base types is passed over: the repository's own candidates read it,
    /// and report it once for each repository that derives from it and needs it.
    /// </summary>
    /// <param name="marks">The project's marks, from <see cref="GlobalMarkReader" />.</param>
    /// <param name="repositories">
    /// The metadata names of the types marked <c>[SqlSourceGenerate]</c>, whether they have a repository query or not.
    /// </param>
    /// <param name="compilation">The compilation the marks are in.</param>
    /// <param name="token">Cancels the check.</param>
    public static EquatableArray<DiagnosticInfo> Check(
        EquatableArray<GlobalMark> marks,
        EquatableArray<string> repositories,
        Compilation compilation,
        CancellationToken token
    )
    {
        if (marks.Count == 0)
        {
            return EquatableArray<DiagnosticInfo>.Empty;
        }

        var repositoryTypes = RepositoriesAndTheirBasesOf(repositories, compilation);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        foreach (var mark in marks)
        {
            token.ThrowIfCancellationRequested();
            var symbol = Resolve(mark, compilation, token);
            var name = symbol switch
            {
                IParameterSymbol parameter => IsRepositoryParameter(parameter, token) ? null : parameter.Name,
                null => null,
                _ when IsOwnMember(symbol) || IsInheritedByARepository(symbol, repositoryTypes) => null,
                _ => BrokenNameOf(symbol, mark.Location),
            };
            if (name is not null && symbol?.ContainingType is { } type)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ConnectionMemberNotSupported,
                        mark.Location,
                        type.ToDisplayString(TypeNameFormat),
                        name
                    )
                );
            }
        }

        return new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable());
    }

    /// <summary>
    /// Every valid global of the project that the repository can reach, in the order of the marks, each with its
    /// <see cref="ConnectionCandidate.GlobalTypeText" />, except the statics of types marked
    /// <c>[SqlSourceGenerate]</c> and of the repository's own base types.  A static of a base type is the repository's
    /// own member, which its own candidates read, or skip when a more derived member has its name, so it is never the
    /// repository's global; it is still a global of every other repository that can reach it.
    /// </summary>
    /// <param name="marks">The project's marks, from <see cref="GlobalMarkReader" />.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="compilation">The compilation the marks and the repository are in.</param>
    public static EquatableArray<ConnectionCandidate> CandidatesFor(
        EquatableArray<GlobalMark> marks,
        INamedTypeSymbol repository,
        Compilation compilation
    )
    {
        if (marks.Count == 0)
        {
            return EquatableArray<ConnectionCandidate>.Empty;
        }

        var candidates = ImmutableArray.CreateBuilder<ConnectionCandidate>();
        var ownTypes = TypeAndItsBasesOf(repository);
        foreach (var mark in marks)
        {
            if (
                mark is not { ParameterOrdinal: null, DeclarationId.Length: > 0 }
                || Resolve(mark, compilation, CancellationToken.None) is not { } member
                || IsOwnMember(member)
                || ownTypes.Contains(member.ContainingType.OriginalDefinition)
                || !ConnectionCandidateReader.IsReachable(member, repository, compilation)
            )
            {
                continue;
            }

            var read = ConnectionCandidateReader.ReadMarkedMember(member, isInherited: false, mark.Location);
            if (GlobalOf(member, read) is { } candidate)
            {
                candidates.Add(candidate);
            }
        }

        return new EquatableArray<ConnectionCandidate>(candidates.ToImmutable());
    }

    // The global that the member gives, of what ReadMarkedMember reads of it, or null when it is broken.
    private static ConnectionCandidate? GlobalOf(ISymbol member, ConnectionCandidate? read) =>
        read is null
        || read.Type == CandidateType.Unsupported
        || IsOverridable(member)
        || TypesAround(member).Any(static type => type.TypeParameters.Length > 0 || type.IsFileLocal)
            ? null
            : read with
            {
                Tier = CandidateTier.Global,
                GlobalTypeText = member.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            };

    // The name a message gives a broken member, the property's for an accessor, or null when it is a valid global.
    private static string? BrokenNameOf(ISymbol member, LocationInfo fallback)
    {
        var read = ConnectionCandidateReader.ReadMarkedMember(member, isInherited: false, fallback);
        if (GlobalOf(member, read) is not null)
        {
            return null;
        }

        var name = read?.Name ?? member.Name;
        return name.Length > 0 ? name : member.ToDisplayString();
    }

    // The member or parameter of the mark: a member by its id in the project's own assembly, a parameter by its place
    // in that member, and one that is no member's by its location.  Two types of one name, one of them file-local, give
    // one id, so the symbol declared at the mark is taken when there is one.
    private static ISymbol? Resolve(GlobalMark mark, Compilation compilation, CancellationToken token)
    {
        if (mark.DeclarationId.Length == 0)
        {
            return AtLocation(mark.Location, compilation, token);
        }

        ISymbol? first = null;
        foreach (var owner in DocumentationCommentId.GetSymbolsForDeclarationId(mark.DeclarationId, compilation))
        {
            if (!SymbolEqualityComparer.Default.Equals(owner.ContainingAssembly, compilation.Assembly))
            {
                continue;
            }

            var symbol = mark.ParameterOrdinal is { } ordinal ? ParameterOf(owner, ordinal) : owner;
            if (symbol is not null && IsDeclaredAt(symbol, mark.Location))
            {
                return symbol;
            }

            first ??= symbol;
        }

        return first;
    }

    private static IParameterSymbol? ParameterOf(ISymbol owner, int ordinal)
    {
        var parameters = owner switch
        {
            IMethodSymbol method => method.Parameters,
            IPropertySymbol property => property.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty,
        };
        return ordinal < parameters.Length ? parameters[ordinal] : null;
    }

    private static bool IsDeclaredAt(ISymbol symbol, LocationInfo location) =>
        symbol.Locations.Any(each =>
            each.IsInSource
            && each.SourceSpan == location.Span
            && string.Equals(each.SourceTree?.FilePath, location.Path, StringComparison.Ordinal)
        );

    // The local function, lambda or parameter declared at the location that carries the mark, in the first file of the
    // location's path that has one, so that two trees of one path, or of none, do not hide it.
    private static ISymbol? AtLocation(LocationInfo location, Compilation compilation, CancellationToken token)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (!string.Equals(tree.FilePath, location.Path, StringComparison.Ordinal))
            {
                continue;
            }

            var root = tree.GetRoot(token);
            if (!root.FullSpan.Contains(location.Span))
            {
                continue;
            }

            var declaration = root.FindNode(location.Span, getInnermostNodeForTie: true)
                .AncestorsAndSelf()
                .FirstOrDefault(static node =>
                    node is ParameterSyntax or LocalFunctionStatementSyntax or LambdaExpressionSyntax
                );
            if (declaration is null)
            {
                continue;
            }

            var model = compilation.GetSemanticModel(tree);
            var symbol = model.GetDeclaredSymbol(declaration, token) ?? model.GetSymbolInfo(declaration, token).Symbol;
            if (symbol is not null && ConnectionCandidateReader.FindMarking(symbol) is not null)
            {
                return symbol;
            }
        }

        return null;
    }

    // Each type marked [SqlSourceGenerate] and each of their base types, as declared, whose static members are the
    // repositories' own.
    private static HashSet<ISymbol> RepositoriesAndTheirBasesOf(
        EquatableArray<string> repositories,
        Compilation compilation
    )
    {
        var types = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var name in repositories)
        {
            if (compilation.GetTypeByMetadataName(name) is { } type)
            {
                types.UnionWith(TypeAndItsBasesOf(type));
            }
        }

        return types;
    }

    // The type and each of its base types, as declared, whose static members are the type's own.
    private static HashSet<ISymbol> TypeAndItsBasesOf(INamedTypeSymbol type)
    {
        var types = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = type; current is not null; current = current.BaseType)
        {
            _ = types.Add(current.OriginalDefinition);
        }

        return types;
    }

    // A member of a type among the repositories and their base types.
    private static bool IsInheritedByARepository(ISymbol symbol, HashSet<ISymbol> repositoryTypes) =>
        GlobalMarkReader.IsMember(symbol) && repositoryTypes.Contains(symbol.ContainingType.OriginalDefinition);

    // A member of a type marked [SqlSourceGenerate], which the type's own candidates read.
    private static bool IsOwnMember(ISymbol symbol) =>
        GlobalMarkReader.IsMember(symbol) && IsRepository(symbol.ContainingType);

    // A parameter of the primary constructor of a type marked [SqlSourceGenerate].
    private static bool IsRepositoryParameter(IParameterSymbol parameter, CancellationToken token) =>
        parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
        && IsRepository(constructor.ContainingType)
        && SymbolEqualityComparer.Default.Equals(
            ConnectionCandidateReader.PrimaryConstructorOf(constructor.ContainingType, token),
            constructor
        );

    // By the attribute's name first, so that the full name is made only for an attribute that can be it.
    private static bool IsRepository(INamedTypeSymbol? type) =>
        type is not null
        && type.GetAttributes()
            .Any(static attribute =>
                attribute.AttributeClass is { Name: GenerateAttributeName } attributeClass
                && attributeClass
                    .ToDisplayString()
                    .Equals(AttributeSource.AttributeMetadataName, StringComparison.Ordinal)
            );

    // The type that declares the member, and each type it is nested in.
    private static IEnumerable<INamedTypeSymbol> TypesAround(ISymbol member)
    {
        for (var type = member.ContainingType; type is not null; type = type.ContainingType)
        {
            yield return type;
        }
    }

    // A static abstract or static virtual interface member, which only a type argument names.
    private static bool IsOverridable(ISymbol member) => member.IsAbstract || member.IsVirtual;
}
