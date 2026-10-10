using Microsoft.CodeAnalysis;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Why a session could not be opened or a query described.  The run reports it at the query.
/// </summary>
/// <param name="Descriptor">What is wrong.</param>
/// <param name="Arguments">The text the descriptor's message quotes.</param>
/// <param name="Step">The step that failed, or null for a session that could not be opened.</param>
/// <param name="ServerLines">What the server said, verbatim, a line each.</param>
/// <param name="Help">How to mend it, or null.</param>
internal sealed record DescribeFailure(
    DiagnosticDescriptor Descriptor,
    EquatableArray<string> Arguments,
    DescribeStep? Step,
    EquatableArray<string> ServerLines,
    string? Help
);
