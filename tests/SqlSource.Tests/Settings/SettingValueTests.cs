using System;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public class SettingValueTests
{
    [Theory]
    [InlineData("many", "Many")]
    [InlineData("ONE", "One")]
    [InlineData("one-optional", "OneOptional")]
    [InlineData("OneOptional", "OneOptional")]
    [InlineData("one optional", "OneOptional")]
    [InlineData("row-count", "RowCount")]
    [InlineData("rowcount", "RowCount")]
    [InlineData("none", "None")]
    public void TryReadChoice_MemberNameInAnySpelling_IsRead(string value, string expected)
    {
        SettingValue.TryReadChoice<ResultShape>(value.AsSpan(), out var shape).ShouldBeTrue();

        shape.ShouldBe(Enum.Parse<ResultShape>(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("on")]
    [InlineData("ones")]
    [InlineData("one_optional")]
    [InlineData("1")]
    [InlineData("one, many")]
    public void TryReadChoice_AnythingElse_IsNotRead(string value) =>
        SettingValue.TryReadChoice<ResultShape>(value.AsSpan(), out _).ShouldBeFalse();
}
