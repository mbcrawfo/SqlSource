using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer.Statements;
using Xunit;
using TypeNameFound = SqlSource.Tool.Describing.Describers.DescribeFailures.TypeNameFound;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Statements;

// The text of a -- param: type is put into a declaration the tool sends, so it is read against T-SQL's grammar of a
// type name first: one or two parts, then (max), one or two numbers or an XML schema collection in parentheses, and
// nothing else.
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
    [InlineData("xml(S)")]
    [InlineData("xml(dbo.S)")]
    [InlineData("xml(CONTENT dbo.S)")]
    [InlineData("xml(DOCUMENT S)")]
    [InlineData("xml(content dbo.S)")]
    [InlineData("xml([dbo].[S])")]
    [InlineData("xml(\"dbo\".\"S\")")]
    [InlineData("xml(CONTENT[dbo].[S])")]
    [InlineData("xml(CONTENT CONTENT)")]
    [InlineData("xml(CONTENT.S)")]
    [InlineData("xml(maxSchema)")]
    [InlineData("xml(CONTENTx)")]
    [InlineData("sys.xml(dbo.S)")]
    [InlineData("xml (S)")]
    [InlineData("xml( CONTENT dbo.S )")]
    [InlineData("dbo . Email")]
    [InlineData("dbo .Email")]
    [InlineData("dbo. Email")]
    [InlineData("dbo\t.\tEmail")]
    [InlineData("[dbo] . [Email]")]
    [InlineData("xml(dbo . S)")]
    [InlineData("xml(CONTENT dbo . S)")]
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
    [InlineData("int;", TypeNameFound.Semicolon)]
    [InlineData("int; DROP TABLE x", TypeNameFound.Semicolon)]
    [InlineData("int -- x", TypeNameFound.Comment)]
    [InlineData("int /* x */", TypeNameFound.Comment)]
    [InlineData("nvarchar(/* x */50)", TypeNameFound.Comment)]
    [InlineData("[dbo", DeclaredTypeText.NotClosed)]
    [InlineData("\"dbo", DeclaredTypeText.NotClosed)]
    [InlineData("[a]]", DeclaredTypeText.NotClosed)]
    [InlineData("int) --", DeclaredTypeText.Shape)]
    [InlineData("int foo", DeclaredTypeText.Shape)]
    [InlineData("nvarchar(", DeclaredTypeText.Shape)]
    [InlineData("nvarchar(max, 2)", DeclaredTypeText.Shape)]
    [InlineData("xml(CONTENT dbo.S, 1)", DeclaredTypeText.Shape)]
    [InlineData("xml(dbo.S;)", TypeNameFound.Semicolon)]
    [InlineData("xml(CONTENT -- x)", TypeNameFound.Comment)]
    [InlineData("xml([dbo)", DeclaredTypeText.NotClosed)]
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
    [InlineData("a.b.c", TypeNameFound.Parts)]
    [InlineData("a . b . c", TypeNameFound.Parts)]
    [InlineData("a. b .c", TypeNameFound.Parts)]
    [InlineData(".t", TypeNameFound.Parts)]
    [InlineData("t.", TypeNameFound.Parts)]
    [InlineData(". t", TypeNameFound.Parts)]
    [InlineData("t .", TypeNameFound.Parts)]
    // A schema collection of three parts makes the server end the session, so it never reaches the server.
    [InlineData("xml(a.b.c)", TypeNameFound.Parts)]
    [InlineData("xml(CONTENT a.b.c)", TypeNameFound.Parts)]
    [InlineData("xml(a . b . c)", TypeNameFound.Parts)]
    [InlineData("xml(.S)", TypeNameFound.Parts)]
    [InlineData("int\nfoo", TypeNameFound.OneLine)]
    [InlineData("int\r\n", TypeNameFound.OneLine)]
    [InlineData("[a\nb]", TypeNameFound.OneLine)]
    [InlineData("\"a\rb\"", TypeNameFound.OneLine)]
    [InlineData("int\u0085foo", TypeNameFound.OneLine)]
    [InlineData("int\u2028", TypeNameFound.OneLine)]
    [InlineData("int\u2029foo", TypeNameFound.OneLine)]
    [InlineData("[a\u0085b]", TypeNameFound.OneLine)]
    [InlineData("[a\u2028b]", TypeNameFound.OneLine)]
    [InlineData("\"a\u2029b\"", TypeNameFound.OneLine)]
    public void Read_AnythingElse_IsRefusedWithWhatWasFound(string text, string found) =>
        DeclaredTypeText.Read(text).Failure.ShouldBe(DescribeFailures.NotATypeName + ": " + found, text);

    [Fact]
    public void Read_Semicolon_SaysItIsNotWrittenAsATypeName() =>
        DeclaredTypeText
            .Read("int; DROP TABLE x")
            .Failure.ShouldBe("it is not written as a type name: a ; is not part of a type name");
}
