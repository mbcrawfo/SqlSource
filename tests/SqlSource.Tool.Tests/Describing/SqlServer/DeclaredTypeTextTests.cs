using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The text of a -- param: type is put into a declaration the tool sends, so it is read against T-SQL's grammar of a
// type name first: one or two parts, then (max) or one or two numbers in parentheses, and nothing else.
public sealed class DeclaredTypeTextTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("nvarchar(50)")]
    [InlineData("varchar(max)")]
    [InlineData("VARCHAR(MAX)")]
    [InlineData("decimal(18,2)")]
    [InlineData("decimal(18, 2)")]
    [InlineData("decimal ( 18 , 2 )")]
    [InlineData("dbo.Email")]
    [InlineData("[billing].[Amount]")]
    [InlineData("\"dbo\".\"Email\"")]
    [InlineData("[a]]b]")]
    [InlineData("\"a\"\"b\"")]
    [InlineData("sys.sysname")]
    [InlineData("[My Schema].[Odd; Name]")]
    [InlineData("#t")]
    [InlineData("_t$1")]
    [InlineData("dbo.Größe")]
    [InlineData("Größe")]
    [InlineData("dbo.名前")]
    [InlineData("dbo.x٣")]
    [InlineData("dbo.Ａbc")]
    public void Read_TypeName_IsTheText(string text) => DeclaredTypeText.Read(text).Value.ShouldBe(text);

    // A delimited name holds anything but its own closing delimiter alone, so what looks like a second statement
    // inside one is part of the one name, and the server reads it as such.
    [Theory]
    [InlineData("[a]]; DROP]")]
    [InlineData("\"a\"\";\"")]
    [InlineData("[a]]--b]")]
    [InlineData("[x, @y int]")]
    public void Read_DelimitedNameThatHoldsSyntax_IsOneName(string text) =>
        DeclaredTypeText.Read(text).Value.ShouldBe(text);

    // Space and tab may stand between the name and its parentheses, and inside them.
    [Theory]
    [InlineData("nvarchar\t(50)")]
    [InlineData("nvarchar ( 50 )")]
    [InlineData("nvarchar(\tmax\t)")]
    public void Read_SpaceBeforeOrInsideTheParentheses_IsAccepted(string text) =>
        DeclaredTypeText.Read(text).Value.ShouldBe(text);

    [Fact]
    public void Read_SpaceAroundTheName_IsLeftOut() =>
        DeclaredTypeText.Read("  nvarchar(50) \t").Value.ShouldBe("nvarchar(50)");

    [Theory]
    [InlineData("int;", DeclaredTypeText.Semicolon)]
    [InlineData("int; DROP TABLE x", DeclaredTypeText.Semicolon)]
    [InlineData("int -- x", DeclaredTypeText.Comment)]
    [InlineData("int /* x */", DeclaredTypeText.Comment)]
    [InlineData("nvarchar(/* x */50)", DeclaredTypeText.Comment)]
    [InlineData("[dbo", DeclaredTypeText.NotClosed)]
    [InlineData("\"dbo", DeclaredTypeText.NotClosed)]
    [InlineData("[a]]", DeclaredTypeText.NotClosed)]
    [InlineData("int) --", DeclaredTypeText.Shape)]
    [InlineData("int foo", DeclaredTypeText.Shape)]
    [InlineData("nvarchar(", DeclaredTypeText.Shape)]
    [InlineData("nvarchar(max, 2)", DeclaredTypeText.Shape)]
    [InlineData("decimal(18,2,1)", DeclaredTypeText.Shape)]
    [InlineData("nvarchar(-1)", DeclaredTypeText.Shape)]
    [InlineData("nvarchar()", DeclaredTypeText.Shape)]
    [InlineData("decimal(18,2)(3)", DeclaredTypeText.Shape)]
    [InlineData("int(1)(2)", DeclaredTypeText.Shape)]
    [InlineData("int\tfoo", DeclaredTypeText.Shape)]
    // A second declaration never passes: a comma stands outside the grammar.
    [InlineData("int, @x int", DeclaredTypeText.Shape)]
    [InlineData("int,@x int", DeclaredTypeText.Shape)]
    [InlineData("nvarchar(50), @x int", DeclaredTypeText.Shape)]
    [InlineData("[a]], @x int", DeclaredTypeText.NotClosed)]
    [InlineData("''", DeclaredTypeText.Shape)]
    [InlineData("", DeclaredTypeText.Shape)]
    [InlineData(" ", DeclaredTypeText.Shape)]
    [InlineData("[]", DeclaredTypeText.Shape)]
    [InlineData("1int", DeclaredTypeText.Shape)]
    [InlineData("€type", DeclaredTypeText.Shape)]
    [InlineData("٣x", DeclaredTypeText.Shape)]
    [InlineData("x²", DeclaredTypeText.Shape)]
    [InlineData("a.b.c", DeclaredTypeText.Parts)]
    [InlineData("dbo .Email", DeclaredTypeText.Parts)]
    [InlineData("dbo. Email", DeclaredTypeText.Parts)]
    [InlineData(".t", DeclaredTypeText.Parts)]
    [InlineData("t.", DeclaredTypeText.Parts)]
    [InlineData("int\nfoo", DeclaredTypeText.OneLine)]
    [InlineData("int\r\n", DeclaredTypeText.OneLine)]
    [InlineData("[a\nb]", DeclaredTypeText.OneLine)]
    [InlineData("\"a\rb\"", DeclaredTypeText.OneLine)]
    [InlineData("int\u0085foo", DeclaredTypeText.OneLine)]
    [InlineData("int\u2028", DeclaredTypeText.OneLine)]
    [InlineData("int\u2029foo", DeclaredTypeText.OneLine)]
    [InlineData("[a\u0085b]", DeclaredTypeText.OneLine)]
    [InlineData("[a\u2028b]", DeclaredTypeText.OneLine)]
    [InlineData("\"a\u2029b\"", DeclaredTypeText.OneLine)]
    public void Read_AnythingElse_IsRefusedWithWhatWasFound(string text, string found) =>
        DeclaredTypeText.Read(text).Failure.ShouldBe(DeclaredTypeText.NotATypeName + ": " + found, text);

    [Fact]
    public void Read_Semicolon_SaysItIsNotWrittenAsATypeName() =>
        DeclaredTypeText
            .Read("int; DROP TABLE x")
            .Failure.ShouldBe("it is not written as a type name: a ; is not part of a type name");
}
