using System;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class SettingValueTests
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
    [InlineData("methods", 2)]
    [InlineData("me-thods", 2)]
    [InlineData("Methods", 2)]
    public void TryReadChoice_Output_IsRead(string value, int expected)
    {
        SettingValue.TryReadChoice<OutputKind>(value.AsSpan(), out var output).ShouldBeTrue();

        ((int)output).ShouldBe(expected);
    }

    [Fact]
    public void TryReadChoice_Codegen_IsNotRead() =>
        SettingValue.TryReadChoice<OutputKind>("codegen".AsSpan(), out _).ShouldBeFalse();

    [Theory]
    [InlineData("class", 1)]
    [InlineData("Class", 1)]
    [InlineData("nested-class", 2)]
    [InlineData("NestedClass", 2)]
    [InlineData("nested class", 2)]
    [InlineData("extension class", 0)]
    [InlineData("Extension-Class", 0)]
    public void TryReadChoice_MemberLocation_IsRead(string value, int expected)
    {
        SettingValue.TryReadChoice<MemberLocation>(value.AsSpan(), out var location).ShouldBeTrue();

        ((int)location).ShouldBe(expected);
    }

    [Theory]
    [InlineData("public", 0)]
    [InlineData("Internal", 1)]
    [InlineData("PROTECTED", 2)]
    [InlineData("private", 3)]
    public void TryReadChoice_Visibility_IsRead(string value, int expected)
    {
        SettingValue.TryReadChoice<Visibility>(value.AsSpan(), out var visibility).ShouldBeTrue();

        ((int)visibility).ShouldBe(expected);
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
    [InlineData("IAsyncEnumerable", 10)]
    [InlineData("i-async enumerable", 10)]
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
    [InlineData("Sql", true)]
    [InlineData("Queries", true)]
    [InlineData("_queries2", true)]
    [InlineData("where", true)]
    [InlineData("", false)]
    [InlineData("class", false)]
    [InlineData("App.Sql", false)]
    [InlineData("My-Sql", false)]
    [InlineData("1Sql", false)]
    [InlineData("@Sql", false)]
    [InlineData("Sql\u200B", false)]
    // The contextual keywords that C# refuses as a type's name.
    [InlineData("file", false)]
    [InlineData("required", false)]
    [InlineData("scoped", false)]
    [InlineData("extension", false)]
    [InlineData("File", true)]
    [InlineData("record", true)]
    [InlineData("partial", true)]
    // The keyword of the locals of a query method, which a class of its name would stand for.
    [InlineData("var", false)]
    [InlineData("Var", true)]
    public void IsNestedClassName_OneIdentifierThatCanNameANestedClass_IsAName(string value, bool expected) =>
        SettingValue.IsNestedClassName(value.AsSpan()).ShouldBe(expected);

    // The rule of a namespace and of a type's name, which is one identifier or a full name.
    [Theory]
    [InlineData("App", true)]
    [InlineData("App.Data.Models", true)]
    [InlineData("UserRow", true)]
    [InlineData("App.Models.UserRow", true)]
    [InlineData("", false)]
    [InlineData("App.", false)]
    [InlineData(".App", false)]
    [InlineData("App..Data", false)]
    [InlineData("App.class", false)]
    [InlineData("class", false)]
    [InlineData("App.1st", false)]
    [InlineData("global::App", false)]
    [InlineData("User Row", false)]
    [InlineData("UserRow<T>", false)]
    // A contextual keyword that C# refuses as a type's name, as any part.
    [InlineData("file", false)]
    [InlineData("required.App", false)]
    [InlineData("App.scoped.UserRow", false)]
    [InlineData("App.extension", false)]
    [InlineData("App.where.UserRow", true)]
    [InlineData("App.var", true)]
    public void IsQualifiedName_IdentifiersJoinedByPeriods_IsAName(string value, bool expected) =>
        SettingValue.IsQualifiedName(value.AsSpan()).ShouldBe(expected);

    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the member's name.
    [InlineData("Sql", "sql")]
    [InlineData("Models", "models")]
    [InlineData("Methods", "methods")]
    public void NameOf_Member_IsItsNameInLowerCaseWhichTryReadChoiceReads(string member, string expected)
    {
        var output = Enum.Parse<OutputKind>(member);

        SettingValue.NameOf(output).ShouldBe(expected);
        SettingValue.TryReadChoice<OutputKind>(expected.AsSpan(), out var read).ShouldBeTrue();
        read.ShouldBe(output);
    }

    [Fact]
    public void NameOf_NumberThatIsNoMember_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => SettingValue.NameOf((OutputKind)99));
}
