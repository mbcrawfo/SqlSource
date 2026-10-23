using Shouldly;
using SqlSource.Settings.TypeOverrides;
using Xunit;

namespace SqlSource.Tests.Settings.TypeOverrides;

public sealed class TypeKeyNamesTests
{
    [Theory]
    [InlineData("citext")]
    [InlineData("hstore")]
    [InlineData("ltree")]
    [InlineData("lquery")]
    [InlineData("ltxtquery")]
    [InlineData("cube")]
    [InlineData("CItext")]
    [InlineData("HSTORE")]
    public void IsExtension_TypeOfAnExtension_IsOne(string internalName) =>
        TypeKeyNames.IsExtension(internalName).ShouldBeTrue();

    [Theory]
    [InlineData("text")]
    [InlineData("int4")]
    [InlineData("geometry")]
    public void IsExtension_AnyOtherType_IsNotOne(string internalName) =>
        TypeKeyNames.IsExtension(internalName).ShouldBeFalse();
}
