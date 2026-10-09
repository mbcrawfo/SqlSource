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

    [Theory]
    [InlineData("sql", 0)]
    [InlineData("Models", 1)]
    [InlineData("codegen", 2)]
    [InlineData("code-gen", 2)]
    [InlineData("CodeGen", 2)]
    public void TryReadChoice_Output_IsRead(string value, int expected)
    {
        SettingValue.TryReadChoice<OutputKind>(value.AsSpan(), out var output).ShouldBeTrue();

        ((int)output).ShouldBe(expected);
    }

    [Theory]
    [InlineData("billing", true)]
    [InlineData("billing-v2", true)]
    [InlineData("app_1.read", true)]
    [InlineData("Größe", true)]
    [InlineData("", false)]
    [InlineData("two words", false)]
    [InlineData("a/b", false)]
    [InlineData("a=b", false)]
    public void IsDatabaseName_OneWordOfLettersDigitsAndThreeMarks_IsAName(string value, bool expected) =>
        SettingValue.IsDatabaseName(value.AsSpan()).ShouldBe(expected);
}
