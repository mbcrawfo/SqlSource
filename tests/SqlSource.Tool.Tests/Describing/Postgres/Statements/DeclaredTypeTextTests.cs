using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres.Statements;
using Xunit;
using TypeNameFound = SqlSource.Tool.Describing.Describers.DescribeFailures.TypeNameFound;

namespace SqlSource.Tool.Tests.Describing.Postgres.Statements;

// The text of a -- param: type is put into SQL the tool sends, so it is read against the grammar of a type name
// first: a name, PostgreSQL's own type words, one modifier, and array marks.  Anything else never leaves the tool.
public sealed class DeclaredTypeTextTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("varchar(50)")]
    [InlineData("numeric(2,-3)")]
    [InlineData("numeric(18, 2)")]
    [InlineData("character varying(50)")]
    [InlineData("national character varying(10)")]
    [InlineData("bit varying(5)")]
    [InlineData("double precision")]
    [InlineData("timestamp(3) without time zone")]
    [InlineData("timestamp with time zone")]
    [InlineData("time(3) with time zone")]
    [InlineData("interval day to second(3)")]
    [InlineData("interval year to month")]
    [InlineData("INTERVAL HOUR TO MINUTE")]
    [InlineData("public.user_status")]
    [InlineData("public . user_status")]
    [InlineData("\"My Schema\".\"Status\"[]")]
    [InlineData("\"a\"\"b\"")]
    [InlineData("text[]")]
    [InlineData("int[][]")]
    [InlineData("email_t")]
    // A modifier of words is the server's to judge: it refuses int(a), and the reader only keeps the text a type name.
    [InlineData("geometry(Point,4326)")]
    [InlineData("geography(PointZM, 4326)")]
    [InlineData("geography( PointZM , 4326 )")]
    [InlineData("geometry(Point)")]
    [InlineData("geometry(_point_1)")]
    [InlineData("public.geometry(Point,4326)[]")]
    [InlineData("int(a)")]
    // A name with "$", or with a character above U+007F that is no letter, is accepted quoted.
    [InlineData("\"my$type\"")]
    [InlineData("\"€type\"")]
    public void Read_TypeName_IsTheText(string text) => DeclaredTypeText.Read(text).Value.ShouldBe(text);

    [Fact]
    public void Read_SpaceAroundTheName_IsLeftOut() =>
        DeclaredTypeText.Read("  varchar(50) \t").Value.ShouldBe("varchar(50)");

    [Theory]
    [InlineData("", DeclaredTypeText.Shape)]
    [InlineData(" ", DeclaredTypeText.Shape)]
    [InlineData(";", TypeNameFound.Semicolon)]
    [InlineData("int;", TypeNameFound.Semicolon)]
    [InlineData("'", DeclaredTypeText.Shape)]
    [InlineData("int'", DeclaredTypeText.Shape)]
    [InlineData("int /* x */", TypeNameFound.Comment)]
    [InlineData("int -- x", TypeNameFound.Comment)]
    // The ")" comes before the ";".
    [InlineData("int) AS SELECT 1", DeclaredTypeText.Shape)]
    [InlineData("int) AS SELECT 1; DROP TABLE users; --", DeclaredTypeText.Shape)]
    [InlineData("int AS SELECT foo", DeclaredTypeText.Shape)]
    [InlineData("int foo", DeclaredTypeText.Shape)]
    [InlineData("int(", DeclaredTypeText.NotClosed)]
    [InlineData("int(1)(2)", DeclaredTypeText.Shape)]
    [InlineData("int(--)", TypeNameFound.Comment)]
    [InlineData("int(1-)", DeclaredTypeText.Modifier)]
    [InlineData("(1)", DeclaredTypeText.Shape)]
    [InlineData("int[", DeclaredTypeText.Shape)]
    [InlineData("int[3]", DeclaredTypeText.Shape)]
    [InlineData("int[;", TypeNameFound.Semicolon)]
    [InlineData("int[--", TypeNameFound.Comment)]
    [InlineData("int[\n", TypeNameFound.OneLine)]
    [InlineData("[]", DeclaredTypeText.Shape)]
    [InlineData("int[] foo", DeclaredTypeText.Shape)]
    [InlineData("\"open", DeclaredTypeText.NotClosed)]
    // A quoted identifier left open says what stands first: a line break, a NUL or the end.
    [InlineData("\"a\nb", TypeNameFound.OneLine)]
    [InlineData("\"a\0", DeclaredTypeText.Shape)]
    [InlineData("\"a\nb\"", TypeNameFound.OneLine)]
    [InlineData("int\"x\"", DeclaredTypeText.Shape)]
    [InlineData("a.b.c", TypeNameFound.Parts)]
    [InlineData(".t", TypeNameFound.Parts)]
    [InlineData("t.", TypeNameFound.Parts)]
    [InlineData("int\nfoo", TypeNameFound.OneLine)]
    [InlineData("int\n", TypeNameFound.OneLine)]
    [InlineData("\"a\rb\"", TypeNameFound.OneLine)]
    [InlineData("\"a\r\nb\"", TypeNameFound.OneLine)]
    [InlineData("\"a\0b\"", DeclaredTypeText.Shape)]
    // A modifier holds numbers and bare words alone.
    [InlineData("geometry('Point',4326)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(Point,'4326')", DeclaredTypeText.Modifier)]
    [InlineData("geometry(\"Point\",4326)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(Point;4326)", TypeNameFound.Semicolon)]
    [InlineData("geometry(Point--)", TypeNameFound.Comment)]
    [InlineData("geometry(Point -- x)", TypeNameFound.Comment)]
    [InlineData("geometry(Point/*x*/)", TypeNameFound.Comment)]
    [InlineData("geometry(Point\n4326)", TypeNameFound.OneLine)]
    [InlineData("geometry((Point))", DeclaredTypeText.Modifier)]
    [InlineData("geometry(Point(1))", DeclaredTypeText.Modifier)]
    [InlineData("geometry(4326Point)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(4326_point)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(-1_a)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(-Point)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(Point$1)", DeclaredTypeText.Modifier)]
    [InlineData("geometry($$Point$$)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(Pöint)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(Point.Z)", DeclaredTypeText.Modifier)]
    [InlineData("geometry(Point,4326", DeclaredTypeText.NotClosed)]
    [InlineData("geometry(Point)(4326)", DeclaredTypeText.Shape)]
    // Stricter than PostgreSQL's rule for a word on purpose: a "$" would let "1$$" open a dollar quote in the
    // PREPARE, and "1$1" be a parameter there.
    [InlineData("$type", DeclaredTypeText.Shape)]
    [InlineData("my$type", DeclaredTypeText.Shape)]
    [InlineData("1$$", DeclaredTypeText.Shape)]
    [InlineData("1$1", DeclaredTypeText.Shape)]
    [InlineData("€type", DeclaredTypeText.Shape)]
    public void Read_AnythingElse_IsRefusedWithWhatWasFound(string text, string found) =>
        DeclaredTypeText.Read(text).Failure.ShouldBe(DescribeFailures.NotATypeName + ": " + found, text);

    [Fact]
    public void Read_Semicolon_SaysItIsNotWrittenAsATypeName() =>
        DeclaredTypeText
            .Read("int; DROP TABLE x")
            .Failure.ShouldBe("it is not written as a type name: a ; is not part of a type name");
}
