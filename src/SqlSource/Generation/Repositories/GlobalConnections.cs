using System;
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
/// <see cref="CandidateTier.Global" /> candidates.  A static member of a type marked <c>[SqlSourceGenerate]</c> and a
/// parameter of such a type's primary constructor are the type's own, which <see cref="ConnectionCandidateReader" />
/// reads, and both methods pass over them.
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
    /// <c>SQLSRC032</c> at each broken mark, in the order of the marks: a parameter, but of a repository's primary
    /// constructor; a member that cannot give a connection; a member of a generic type, or of a type in one, for which
    /// no type arguments can be chosen; and a <c>static abstract</c> or <c>static virtual</c> interface member.
    /// </summary>
    /// <param name="marks">The project's marks, from <see cref="GlobalMarkReader" />.</param>
    /// <param name="compilation">The compilation the marks are in.</param>
    /// <param name="token">Cancels the check.</param>
    public static EquatableArray<DiagnosticInfo> Check(
        EquatableArray<GlobalMark> marks,
        Compilation compilation,
        CancellationToken token
    )
    {
        if (marks.Count == 0)
        {
            return EquatableArray<DiagnosticInfo>.Empty;
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        foreach (var mark in marks)
        {
            token.ThrowIfCancellationRequested();
            var symbol = Resolve(mark, compilation, token);
            var name = symbol switch
            {
                IParameterSymbol parameter => IsRepositoryParameter(parameter, token) ? null : parameter.Name,
                null => null,
                _ => IsOwnMember(symbol) ? null : BrokenNameOf(symbol, mark.Location),
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
    /// The valid globals that the repository can reach, in the order of the marks, each with its
    /// <see cref="ConnectionCandidate.GlobalTypeText" />.  A member of a file-local type is reached from its own file
    /// alone, and the generated code is in another, so it is no repository's.
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
        foreach (var mark in marks)
        {
            if (
                mark is not { ParameterOrdinal: null, DeclarationId.Length: > 0 }
                || Resolve(mark, compilation, CancellationToken.None) is not { } member
                || IsOwnMember(member)
                || IsInFileLocalType(member)
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
        read is null || read.Type == CandidateType.Unsupported || IsInGenericType(member) || IsOverridable(member)
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
        return GlobalOf(member, read) is null ? read?.Name ?? member.Name : null;
    }

    // The member or parameter of the mark: a member by its id in the project's own assembly, a parameter by its place
    // in that member, and a local function or a parameter of one by its location.
    private static ISymbol? Resolve(GlobalMark mark, Compilation compilation, CancellationToken token)
    {
        if (mark.DeclarationId.Length == 0)
        {
            return AtLocation(mark.Location, compilation, token);
        }

        var owner = DocumentationCommentId
            .GetSymbolsForDeclarationId(mark.DeclarationId, compilation)
            .FirstOrDefault(symbol =>
                SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly)
            );
        if (mark.ParameterOrdinal is not { } ordinal)
        {
            return owner;
        }

        var parameters = owner switch
        {
            IMethodSymbol method => method.Parameters,
            IPropertySymbol property => property.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty,
        };
        return ordinal < parameters.Length ? parameters[ordinal] : null;
    }

    // The local function, or the parameter, declared at the location.
    private static ISymbol? AtLocation(LocationInfo location, Compilation compilation, CancellationToken token)
    {
        var tree = compilation.SyntaxTrees.FirstOrDefault(tree =>
            string.Equals(tree.FilePath, location.Path, StringComparison.Ordinal)
        );
        var root = tree?.GetRoot(token);
        if (tree is null || root is null || !root.FullSpan.Contains(location.Span))
        {
            return null;
        }

        var declaration = root.FindNode(location.Span, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .FirstOrDefault(static node => node is ParameterSyntax or LocalFunctionStatementSyntax);
        return declaration is null ? null : compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration, token);
    }

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

    private static bool IsInGenericType(ISymbol member)
    {
        for (var type = member.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.TypeParameters.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInFileLocalType(ISymbol member)
    {
        for (var type = member.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.IsFileLocal)
            {
                return true;
            }
        }

        return false;
    }

    // A static abstract or static virtual interface member, which only a type argument names.
    private static bool IsOverridable(ISymbol member) => member.IsAbstract || member.IsVirtual;
}
