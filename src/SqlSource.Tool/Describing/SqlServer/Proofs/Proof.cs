using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// Why an expression cannot be <c>NULL</c>: from its text alone, or from the server's answers about the operands the
/// text leaves to ask.
/// </summary>
/// <remarks>
/// A proof has no negation, so an operand answered not null never makes one fail that held without it.  An operand
/// the server was not asked about, or did not answer, is nullable.
/// </remarks>
internal abstract record Proof
{
    /// <summary>Proved from the text alone.</summary>
    public static Proof Yes { get; } = new Always();

    /// <summary>Not proved, whatever the server answers.</summary>
    public static Proof No { get; } = new Never();

    /// <summary>
    /// True when the proof holds only through the server's answers: it does not hold without them, and it does when
    /// every operand is answered not null.
    /// </summary>
    public bool NeedsOperands => !Evaluate(static _ => false) && Evaluate(static _ => true);

    /// <summary>True when the expression is proved not null.</summary>
    /// <param name="operandNotNull">
    /// The server's answer for each operand, by its index, true for not null; null when the server was not asked or
    /// its answer proves nothing.
    /// </param>
    public bool Holds(IReadOnlyList<bool>? operandNotNull) =>
        Evaluate(index => operandNotNull is not null && index < operandNotNull.Count && operandNotNull[index]);

    private protected abstract bool Evaluate(Func<int, bool> operandNotNull);

    /// <summary>Holds from the text alone.</summary>
    internal sealed record Always : Proof
    {
        private protected override bool Evaluate(Func<int, bool> operandNotNull) => true;
    }

    /// <summary>Never holds.</summary>
    internal sealed record Never : Proof
    {
        private protected override bool Evaluate(Func<int, bool> operandNotNull) => false;
    }

    /// <summary>Holds when the server answers the operand not null.</summary>
    /// <param name="Index">The operand's index in <see cref="ExpressionProofs.Operands" />.</param>
    internal sealed record Operand(int Index) : Proof
    {
        private protected override bool Evaluate(Func<int, bool> operandNotNull) => operandNotNull(Index);
    }

    /// <summary>Holds when one of its parts holds.</summary>
    internal sealed record AnyOf(IReadOnlyList<Proof> Parts) : Proof
    {
        private protected override bool Evaluate(Func<int, bool> operandNotNull) =>
            Parts.Any(part => part.Evaluate(operandNotNull));
    }

    /// <summary>Holds when every one of its parts holds, and it has one.</summary>
    internal sealed record AllOf(IReadOnlyList<Proof> Parts) : Proof
    {
        private protected override bool Evaluate(Func<int, bool> operandNotNull) =>
            Parts.Count > 0 && Parts.All(part => part.Evaluate(operandNotNull));
    }
}
