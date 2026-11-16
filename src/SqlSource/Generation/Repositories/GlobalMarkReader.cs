using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Reads a <c>[SqlSourceConnection]</c> of the project's source into a <see cref="GlobalMark" />, the transform of the
/// global stage's <c>ForAttributeWithMetadataName</c>, which runs again only for a changed file.  It reads the marked
/// symbol alone: whether it is in a repository, what its type derives from and who can reach it may change in another
/// file, and <see cref="GlobalConnections" /> decides them over the compilation.
/// </summary>
internal static class GlobalMarkReader
{
    /// <summary>
    /// Whether a node that carries the attribute is read: anything the attribute's targets allow, a field's variable,
    /// an enum member, a property, a method, an operator, an accessor, a local function, a lambda or a parameter.
    /// </summary>
    /// <param name="node">The node that carries the attribute.</param>
    public static bool IsCandidate(SyntaxNode node) =>
        node
            is VariableDeclaratorSyntax
                or EnumMemberDeclarationSyntax
                or PropertyDeclarationSyntax
                or MethodDeclarationSyntax
                or OperatorDeclarationSyntax
                or ConversionOperatorDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or LambdaExpressionSyntax
                or ParameterSyntax;

    /// <summary>
    /// The mark of a static member, of a local function, of a lambda or of a parameter, or null for an instance member,
    /// which is a factory's, read through the holders that hold it.
    /// </summary>
    /// <param name="context">The marked node and its symbol.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static GlobalMark? Read(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbol = context.TargetSymbol;
        var location = LocationOf(symbol, context.TargetNode);
        if (symbol is IParameterSymbol parameter)
        {
            return new GlobalMark(IdOf(parameter.ContainingSymbol), parameter.Ordinal, location);
        }

        return IsMember(symbol) && !symbol.IsStatic ? null : new GlobalMark(IdOf(symbol), null, location);
    }

    /// <summary>
    /// The marks in the order of their ids, their parameters' places and their locations, the same for every order of
    /// the files.
    /// </summary>
    /// <param name="marks">The marks the stage found.</param>
    public static EquatableArray<GlobalMark> Sort(ImmutableArray<GlobalMark> marks) =>
        marks.IsEmpty
            ? EquatableArray<GlobalMark>.Empty
            : new(
                marks
                    .OrderBy(static mark => mark.DeclarationId, StringComparer.Ordinal)
                    .ThenBy(static mark => mark.ParameterOrdinal ?? -1)
                    .ThenBy(static mark => mark.Location.Path, StringComparer.Ordinal)
                    .ThenBy(static mark => mark.Location.Span.Start)
                    .ToImmutableArray()
            );

    /// <summary>
    /// Whether the symbol is a member of a type, which its documentation comment id finds again: not a local function,
    /// whose id may name a method of the type.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    internal static bool IsMember(ISymbol symbol) => symbol.ContainingSymbol is INamedTypeSymbol;

    // The member's id, or empty for a symbol that is no member.
    private static string IdOf(ISymbol symbol) =>
        IsMember(symbol) ? symbol.GetDocumentationCommentId() ?? string.Empty : string.Empty;

    // Where the symbol is declared in the node's own file, which is its name, or the node.
    private static LocationInfo LocationOf(ISymbol symbol, SyntaxNode node) =>
        LocationInfo.From(
            symbol.Locations.FirstOrDefault(location =>
                location.SourceTree == node.SyntaxTree && node.Span.Contains(location.SourceSpan)
            ) ?? node.GetLocation()
        );
}
