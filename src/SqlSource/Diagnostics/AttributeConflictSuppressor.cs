using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Generation;

namespace SqlSource.Diagnostics;

/// <summary>
/// Turns off the compiler's warning that a type conflicts with an imported one (CS0436) where the type is one that
/// the generator adds to every project.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AttributeConflictSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Descriptor = new(
        id: "SQLSRC901",
        suppressedDiagnosticId: "CS0436",
        justification: "SqlSource adds this type to every project that uses it, and each project uses its own copy"
    );

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        ImmutableArray.Create(Descriptor);

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        // The project's own copies.  A type of the same name that the project declares elsewhere is not one of them.
        var assembly = context.Compilation.Assembly;
        var attribute = assembly.GetTypeByMetadataName(AttributeSource.AttributeMetadataName);
        var mode = assembly.GetTypeByMetadataName(AttributeSource.ModeMetadataName);

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (
                UsedType(context, diagnostic) is { } type
                && (
                    SymbolEqualityComparer.Default.Equals(type, attribute)
                    || SymbolEqualityComparer.Default.Equals(type, mode)
                )
            )
            {
                context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
            }
        }
    }

    // The warning is at a name that the compiler bound to the project's own type.  In an attribute that name binds to
    // a constructor.
    private static INamedTypeSymbol? UsedType(SuppressionAnalysisContext context, Diagnostic diagnostic)
    {
        if (diagnostic.Location.SourceTree is not { } tree)
        {
            return null;
        }

        // An attribute without arguments spans the same text as its name, and the name is the inner node.
        var node = tree.GetRoot(context.CancellationToken)
            .FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var symbol = context.GetSemanticModel(tree).GetSymbolInfo(node, context.CancellationToken).Symbol;
        return symbol as INamedTypeSymbol ?? (symbol as IMethodSymbol)?.ContainingType;
    }
}
