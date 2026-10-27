using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// A proof tree and its evaluation over the server's answers about its operands.
public sealed class ProofTests
{
    [Fact]
    public void Holds_YesWithoutAnswers_IsTrue() => Proof.Yes.Holds(null).ShouldBeTrue();

    [Fact]
    public void Holds_NoWithOperandsNotNull_IsFalse() => Proof.No.Holds(Answers(true)).ShouldBeFalse();

    [Fact]
    public void Holds_OperandWithoutAnswers_DoesNotHold() => new Proof.Operand(0).Holds(null).ShouldBeFalse();

    [Fact]
    public void Holds_OperandBeyondTheAnswers_DoesNotHold() =>
        new Proof.Operand(1).Holds(Answers(true)).ShouldBeFalse();

    [Fact]
    public void Holds_OperandAnsweredNotNull_IsTrue() =>
        new Proof.Operand(1).Holds(Answers(false, true)).ShouldBeTrue();

    // Whether an operand is not null does not depend on its type.
    [Fact]
    public void Holds_OperandNotNullOfAClrType_IsTrue() =>
        new Proof.Operand(0).Holds([new OperandFacts(NotNull: true, NotClr: false)]).ShouldBeTrue();

    [Fact]
    public void Holds_NotClrOfAnOperandOfNoClrType_IsTrueWhateverItsNullability() =>
        new Proof.NotClr(0).Holds([new OperandFacts(NotNull: false, NotClr: true)]).ShouldBeTrue();

    [Fact]
    public void Holds_NotClrOfAnOperandOfAClrType_IsFalse() =>
        new Proof.NotClr(0).Holds([new OperandFacts(NotNull: true, NotClr: false)]).ShouldBeFalse();

    [Fact]
    public void Holds_NotClrWithoutAnswers_DoesNotHold() => new Proof.NotClr(0).Holds(null).ShouldBeFalse();

    [Fact]
    public void Holds_NotClrBeyondTheAnswers_DoesNotHold() => new Proof.NotClr(1).Holds(Answers(true)).ShouldBeFalse();

    [Fact]
    public void Holds_AnyOf_IsTrueWhenOnePartHolds()
    {
        var proof = new Proof.AnyOf([new Proof.Operand(0), new Proof.Operand(1)]);

        proof.Holds(Answers(false, true)).ShouldBeTrue();
        proof.Holds(Answers(false, false)).ShouldBeFalse();
    }

    [Fact]
    public void Holds_AllOf_IsTrueWhenEveryPartHolds()
    {
        var proof = new Proof.AllOf([new Proof.Operand(0), new Proof.Operand(1)]);

        proof.Holds(Answers(true, true)).ShouldBeTrue();
        proof.Holds(Answers(false, true)).ShouldBeFalse();
    }
}
