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

    [Theory]
    [InlineData("record", 0)]
    [InlineData("sealed record", 1)]
    [InlineData("Sealed-Record", 1)]
    [InlineData("SealedRecord", 1)]
    [InlineData("class", 2)]
    [InlineData("sealed class", 3)]
    public void TryReadChoice_ModelType_IsRead(string value, int expected)
    {
        SettingValue.TryReadChoice<ModelKind>(value.AsSpan(), out var kind).ShouldBeTrue();

        ((int)kind).ShouldBe(expected);
    }

    [Theory]
    [InlineData("IEnumerable", 0)]
    [InlineData("ilist", 3)]
    [InlineData("list", 6)]
    [InlineData("Array", 5)]
    [InlineData("immutable-array", 7)]
    [InlineData("ImmutableList", 8)]
    [InlineData("IImmutableList", 9)]
    public void TryReadChoice_CollectionType_IsRead(string value, int expected)
    {
        SettingValue.TryReadChoice<CollectionKind>(value.AsSpan(), out var kind).ShouldBeTrue();

        ((int)kind).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Dto", true)]
    [InlineData("_Row2", true)]
    [InlineData("2", true)]
    [InlineData("", false)]
    [InlineData("A B", false)]
    [InlineData("A-B", false)]
    [InlineData("A.B", false)]
    [InlineData("A​B", false)]
    public void IsSuffix_CharactersThatCanFollowTheFirstOfAnIdentifier_IsASuffix(string value, bool expected) =>
        SettingValue.IsSuffix(value.AsSpan()).ShouldBe(expected);

    [Theory]
    [InlineData("App", true)]
    [InlineData("App.Data.Models", true)]
    [InlineData("", false)]
    [InlineData("App.", false)]
    [InlineData(".App", false)]
    [InlineData("App..Data", false)]
    [InlineData("App.class", false)]
    [InlineData("App.1st", false)]
    [InlineData("global::App", false)]
    public void IsNamespace_IdentifiersJoinedByPeriods_IsANamespace(string value, bool expected) =>
        SettingValue.IsNamespace(value.AsSpan()).ShouldBe(expected);

    [Theory]
    [InlineData("UserRow", true)]
    [InlineData("App.Models.UserRow", true)]
    [InlineData("class", false)]
    [InlineData("User Row", false)]
    [InlineData("UserRow<T>", false)]
    public void IsTypeName_AnIdentifierOrAFullName_IsATypeName(string value, bool expected) =>
        SettingValue.IsTypeName(value.AsSpan()).ShouldBe(expected);
}
