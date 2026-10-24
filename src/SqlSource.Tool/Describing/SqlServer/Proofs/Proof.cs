using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// Why an expression cannot be <c>NULL</c>: from its text alone, or from the server's answers about the operands the
/// text leaves to ask.
/// </summary>
/// <remarks>
/// A proof has no negation, so an operand answered not null, or of a type that is no CLR type, never makes one fail
/// that held without it.  An operand the server was not asked about, or did not answer, is nullable and may be of a CLR
/// type.
/// </remarks>
internal abstract record Proof
{
    /// <summary>Proved from the text alone.</summary>
    public static Proof Yes { get; } = new Always();

    /// <summary>Not proved, whatever the server answers.</summary>
    public static Proof No { get; } = new Never();

    /// <summary>True when the expression is proved not null.</summary>
    /// <param name="answers">
    /// The server's answer for each operand, by its index; null when the server was not asked or its answer proves
    /// nothing.
    /// </param>
    public bool Holds(IReadOnlyList<OperandFacts>? answers) =>
        Evaluate(index => answers is not null && index < answers.Count ? answers[index] : default);

    private protected abstract bool Evaluate(Func<int, OperandFacts> answerOf);

    /// <summary>Holds from the text alone.</summary>
    internal sealed record Always : Proof
    {
        private protected override bool Evaluate(Func<int, OperandFacts> answerOf) => true;
    }

    /// <summary>Never holds.</summary>
    internal sealed record Never : Proof
    {
        private protected override bool Evaluate(Func<int, OperandFacts> answerOf) => false;
    }

    /// <summary>Holds when the server answers the operand not null.</summary>
    /// <param name="Index">The operand's index in <see cref="ExpressionProofs.Operands" />.</param>
    internal sealed record Operand(int Index) : Proof
    {
        private protected override bool Evaluate(Func<int, OperandFacts> answerOf) => answerOf(Index).NotNull;
    }

    /// <summary>
    /// Holds when the server answers the operand of a type that is no CLR type, whatever its nullability: a string
    /// converted to a CLR type may be its Null.
    /// </summary>
    /// <param name="Index">The operand's index in <see cref="ExpressionProofs.Operands" />.</param>
    internal sealed record NotClr(int Index) : Proof
    {
        private protected override bool Evaluate(Func<int, OperandFacts> answerOf) => answerOf(Index).NotClr;
    }

    /// <summary>Holds when one of its parts holds.</summary>
    internal sealed record AnyOf(IReadOnlyList<Proof> Parts) : Proof
    {
        private protected override bool Evaluate(Func<int, OperandFacts> answerOf) =>
            Parts.Any(part => part.Evaluate(answerOf));
    }

    /// <summary>Holds when every one of its parts holds, and it has one.</summary>
    internal sealed record AllOf(IReadOnlyList<Proof> Parts) : Proof
    {
        private protected override bool Evaluate(Func<int, OperandFacts> answerOf) =>
            Parts.Count > 0 && Parts.All(part => part.Evaluate(answerOf));
    }
}
