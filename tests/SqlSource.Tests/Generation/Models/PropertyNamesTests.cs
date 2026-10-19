using Shouldly;
using SqlSource.Generation.Models;
using Xunit;

namespace SqlSource.Tests.Generation.Models;

public sealed class PropertyNamesTests
{
    [Theory]
    [InlineData("created_at", "CreatedAt")]
    [InlineData("userId", "UserId")]
    [InlineData("ID", "ID")]
    [InlineData("api_v2_url", "ApiV2Url")]
    [InlineData("café", "Café")]
    [InlineData("名前", "名前")]
    [InlineData("_a__b_", "AB")]
    [InlineData("2fa", "2fa")]
    [InlineData("___", "")]
    [InlineData("a!", "A")]
    [InlineData("b?", "B")]
    [InlineData("c!?", "C!")]
    public void PascalCase_Name_IsPascalCase(string name, string expected) =>
        PropertyNames.PascalCase(name).ShouldBe(expected);

    // A name that is already PascalCase is given back as it is, with no new string.
    [Fact]
    public void PascalCase_NameAlreadyPascalCase_IsTheSameString()
    {
        const string Name = "CreatedAt";

        PropertyNames.PascalCase(Name).ShouldBeSameAs(Name);
    }

    [Theory]
    [InlineData("2fa", false)]
    [InlineData("", false)]
    [InlineData("Api-Url", false)]
    [InlineData("Café", true)]
    [InlineData("Id", true)]
    public void IsIdentifier_Name_IsWhetherItIsACSharpIdentifier(string name, bool expected) =>
        PropertyNames.IsIdentifier(name).ShouldBe(expected);

    [Theory]
    [InlineData("EqualityContract")]
    [InlineData("Equals")]
    [InlineData("GetHashCode")]
    [InlineData("ToString")]
    [InlineData("GetType")]
    [InlineData("MemberwiseClone")]
    [InlineData("Finalize")]
    [InlineData("ReferenceEquals")]
    [InlineData("PrintMembers")]
    [InlineData("Deconstruct")]
    [InlineData("Clone")]
    public void IsReserved_EachName_IsTrue(string name) => PropertyNames.IsReserved(name).ShouldBeTrue();

    [Theory]
    [InlineData("Id")]
    [InlineData("equals")]
    public void IsReserved_Name_IsFalse(string name) => PropertyNames.IsReserved(name).ShouldBeFalse();
}
