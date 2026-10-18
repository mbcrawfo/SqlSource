using System;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Sidecars;
using SqlSource.Snapshot.Reading;
using Xunit;

namespace SqlSource.Tests.Generation.Sidecars;

public sealed class SidecarFileChecksTests
{
    // The kind is passed as its number, since the enum is internal and a test method is public.
    [Theory]
    [InlineData((int)SidecarErrorKind.InvalidJson, null, "it is not one JSON object")]
    [InlineData((int)SidecarErrorKind.MissingKey, "hash", "'hash' is missing")]
    [InlineData((int)SidecarErrorKind.WrongType, "hash", "'hash' has a value of the wrong type")]
    [InlineData((int)SidecarErrorKind.DuplicateKey, "GetUser", "'GetUser' is written twice")]
    [InlineData((int)SidecarErrorKind.OrdinalMismatch, "3", "the ordinal 3 is not the position of its element")]
    [InlineData((int)SidecarErrorKind.ColumnsWithoutRows, null, "it has columns for a query without rows")]
    [InlineData((int)SidecarErrorKind.UnknownResultKind, "some", "'some' is not a result kind")]
    public void ReasonOf_Error_SaysWhatIsWrong(int kind, string? argument, string reason) =>
        SidecarFileChecks.ReasonOf(new SidecarError((SidecarErrorKind)kind, default, argument)).ShouldBe(reason);

    // A kind added to the reader gets a reason of its own, and never reads as another kind.
    [Fact]
    public void ReasonOf_EveryKind_HasAReasonOfItsOwn()
    {
        var reasons = Enum.GetValues<SidecarErrorKind>()
            .Select(kind => SidecarFileChecks.ReasonOf(new SidecarError(kind, default, "x")))
            .ToArray();

        reasons.ShouldBeUnique();
    }

    [Fact]
    public void ReasonOf_KindThatIsNoMember_DoesNotThrow() =>
        SidecarFileChecks.ReasonOf(new SidecarError((SidecarErrorKind)99, default, null)).ShouldNotBeEmpty();
}
