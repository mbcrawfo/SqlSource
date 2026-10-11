using System.Collections.Immutable;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class OutcomeTests
{
    [Fact]
    public void Of_Value_SucceededWithTheValueAndNoFailure()
    {
        var outcome = Outcome<string, string>.Of("value");

        outcome.Succeeded.ShouldBeTrue();
        outcome.Value.ShouldBe("value");
        outcome.Failure.ShouldBeNull();
    }

    [Fact]
    public void Failed_Reason_DidNotSucceedAndHasTheReasonAndNoValue()
    {
        var outcome = Outcome<string, string>.Failed("why");

        outcome.Succeeded.ShouldBeFalse();
        outcome.Failure.ShouldBe("why");
        outcome.Value.ShouldBeNull();
    }

    // A value that is a struct has no null: a caller reads Succeeded first.
    [Fact]
    public void Failed_OutcomeOfAStruct_HasTheDefaultOfTheStructAsItsValue()
    {
        var outcome = Outcome<ImmutableArray<string>, string>.Failed("why");

        outcome.Succeeded.ShouldBeFalse();
        outcome.Value.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void Equals_TwoOutcomes_AreEqualWhenTheyHoldTheSame()
    {
        Outcome<string, string>.Of("a").ShouldBe(Outcome<string, string>.Of("a"));
        Outcome<string, string>.Of("a").ShouldNotBe(Outcome<string, string>.Of("b"));
        Outcome<string, string>.Of("a").ShouldNotBe(Outcome<string, string>.Failed("a"));
    }
}
