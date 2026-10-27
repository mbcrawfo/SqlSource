using Microsoft.CodeAnalysis;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// Why a session could not be opened or a query described.  The run reports it at the query.
/// </summary>
/// <param name="Descriptor">What is wrong.</param>
/// <param name="Arguments">The text the descriptor's message quotes.</param>
/// <param name="Step">The step that failed, or null for a session that could not be opened.</param>
/// <param name="ServerLines">
/// What the server said, a line each: its messages as it gave them, but for an error of <c>open</c> or of a
/// connection that was lost or timed out, whose line is the engine's code and the describer's fixed text
/// (<c>ConnectionErrors</c>).
/// </param>
/// <param name="Help">How to mend it, or null.</param>
internal sealed record DescribeFailure(
    DiagnosticDescriptor Descriptor,
    EquatableArray<string> Arguments,
    DescribeStep? Step,
    EquatableArray<string> ServerLines,
    string? Help
);
