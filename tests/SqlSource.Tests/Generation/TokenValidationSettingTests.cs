using Shouldly;
using SqlSource.Generation;
using SqlSource.Tests.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

public class TokenValidationSettingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData(" true\t")]
    public void Parse_MissingEmptyOrTrue_Validates(string? value) =>
        TokenValidationSetting.Parse(value).ShouldBe(new TokenValidationSetting(true, null));

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("  false ")]
    public void Parse_False_DoesNotValidate(string value) =>
        TokenValidationSetting.Parse(value).ShouldBe(new TokenValidationSetting(false, null));

    [Theory]
    [InlineData("off")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("fals")]
    [InlineData("true;false")]
    [InlineData(" yes ")]
    public void Parse_AnythingElse_ValidatesAndKeepsTheValueAsWritten(string value) =>
        TokenValidationSetting.Parse(value).ShouldBe(new TokenValidationSetting(true, value));

    [Fact]
    public void Read_ProjectWithoutTheProperty_Validates() =>
        TokenValidationSetting
            .Read(new TestOptionsProvider(null).GlobalOptions)
            .ShouldBe(new TokenValidationSetting(true, null));

    [Fact]
    public void Read_ProjectWithTheProperty_ParsesItsValue()
    {
        TokenValidationSetting
            .Read(new TestOptionsProvider("false").GlobalOptions)
            .ShouldBe(new TokenValidationSetting(false, null));
        TokenValidationSetting
            .Read(new TestOptionsProvider("maybe").GlobalOptions)
            .ShouldBe(new TokenValidationSetting(true, "maybe"));
    }
}
