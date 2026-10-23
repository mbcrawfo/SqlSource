using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// A proof tree and its evaluation over the server's answers about its operands.
public sealed class ProofTests
{
    [Fact]
    public void Holds_YesWithoutAnswers_IsTrue() => Proof.Yes.Holds(null).ShouldBeTrue();

    [Fact]
    public void Holds_NoWithOperandsNotNull_IsFalse() => Proof.No.Holds([true]).ShouldBeFalse();

    [Fact]
    public void Holds_OperandWithoutAnswers_DoesNotHold() => new Proof.Operand(0).Holds(null).ShouldBeFalse();

    [Fact]
    public void Holds_OperandBeyondTheAnswers_DoesNotHold() => new Proof.Operand(1).Holds([true]).ShouldBeFalse();

    [Fact]
    public void Holds_OperandAnsweredNotNull_IsTrue() => new Proof.Operand(1).Holds([false, true]).ShouldBeTrue();

    [Fact]
    public void Holds_AnyOf_IsTrueWhenOnePartHolds()
    {
        var proof = new Proof.AnyOf([new Proof.Operand(0), new Proof.Operand(1)]);

        proof.Holds([false, true]).ShouldBeTrue();
        proof.Holds([false, false]).ShouldBeFalse();
    }

    [Fact]
    public void Holds_AllOf_IsTrueWhenEveryPartHolds()
    {
        var proof = new Proof.AllOf([new Proof.Operand(0), new Proof.Operand(1)]);

        proof.Holds([true, true]).ShouldBeTrue();
        proof.Holds([false, true]).ShouldBeFalse();
    }

    [Fact]
    public void NeedsOperands_Operand_IsTrue() => new Proof.Operand(0).NeedsOperands.ShouldBeTrue();

    [Fact]
    public void NeedsOperands_YesAndNo_AreFalse()
    {
        Proof.Yes.NeedsOperands.ShouldBeFalse();
        Proof.No.NeedsOperands.ShouldBeFalse();
    }

    // An operand whose answer cannot change the proof is not needed.
    [Fact]
    public void NeedsOperands_OperandBesideADecidedPart_IsFalse()
    {
        new Proof.AnyOf([new Proof.Operand(0), Proof.Yes]).NeedsOperands.ShouldBeFalse();
        new Proof.AllOf([new Proof.Operand(0), Proof.No]).NeedsOperands.ShouldBeFalse();
    }
}
