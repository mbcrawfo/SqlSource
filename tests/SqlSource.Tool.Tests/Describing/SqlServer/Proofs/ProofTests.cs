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
    public void Holds_StoredOfAStoredColumn_IsTrueWhateverItsNullability() =>
        new Proof.Stored(0).Holds([new OperandFacts(NotNull: false, NotClr: false, Stored: true)]).ShouldBeTrue();

    [Fact]
    public void Holds_StoredOfAnOperandNotNullAndNotStored_IsFalse() =>
        new Proof.Stored(0).Holds([new OperandFacts(NotNull: true, NotClr: true)]).ShouldBeFalse();

    [Fact]
    public void Holds_StoredWithoutAnswers_DoesNotHold() => new Proof.Stored(0).Holds(null).ShouldBeFalse();

    [Fact]
    public void Holds_ClrOfAnOperandOfAClrType_IsTrueWhateverItsNullability() =>
        new Proof.Clr(0).Holds([new OperandFacts(NotNull: false, NotClr: false, Clr: true)]).ShouldBeTrue();

    [Fact]
    public void Holds_ClrOfAnOperandOfNoClrType_IsFalse() =>
        new Proof.Clr(0).Holds([new OperandFacts(NotNull: true, NotClr: true, Stored: true)]).ShouldBeFalse();

    // An operand not answered is never shown of a CLR type, though it may be one.
    [Fact]
    public void Holds_ClrWithoutAnswers_DoesNotHold() => new Proof.Clr(0).Holds(null).ShouldBeFalse();

    [Fact]
    public void Shifted_Clr_MovesItsIndex() => new Proof.Clr(1).Shifted(2).ShouldBe(new Proof.Clr(3));

    // A CTE's column and the proofs of the text alone have no operand to move.
    [Fact]
    public void Renumbered_MovesEveryOperandOfEveryKind()
    {
        var proof = new Proof.AllOf([
            new Proof.Operand(0),
            new Proof.NotClr(1),
            new Proof.AnyOf([new Proof.Stored(2), new Proof.Clr(3)]),
            new Proof.CteColumn(0, 1),
            Proof.Yes,
            Proof.No,
        ]);

        var renumbered = proof.Renumbered(static index => (index * 10) + 1).ShouldBeOfType<Proof.AllOf>();

        renumbered.Parts.Count.ShouldBe(6);
        renumbered.Parts[0].ShouldBe(new Proof.Operand(1));
        renumbered.Parts[1].ShouldBe(new Proof.NotClr(11));
        renumbered.Parts[2].ShouldBeOfType<Proof.AnyOf>().Parts.ShouldBe([new Proof.Stored(21), new Proof.Clr(31)]);
        renumbered.Parts[3].ShouldBe(new Proof.CteColumn(0, 1));
        renumbered.Parts[4].ShouldBeSameAs(Proof.Yes);
        renumbered.Parts[5].ShouldBeSameAs(Proof.No);
    }

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
