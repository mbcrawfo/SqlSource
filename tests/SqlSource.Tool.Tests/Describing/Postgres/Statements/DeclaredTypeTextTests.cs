using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres.Statements;
using Xunit;

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
    // A name with "$", or with a character above U+007F that is no letter, is accepted quoted.
    [InlineData("\"my$type\"")]
    [InlineData("\"€type\"")]
    public void Read_TypeName_IsTheText(string text) => DeclaredTypeText.Read(text).Value.ShouldBe(text);

    [Fact]
    public void Read_SpaceAroundTheName_IsLeftOut() =>
        DeclaredTypeText.Read("  varchar(50) \t").Value.ShouldBe("varchar(50)");

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(";")]
    [InlineData("int;")]
    [InlineData("'")]
    [InlineData("int'")]
    [InlineData("int /* x */")]
    [InlineData("int -- x")]
    [InlineData("int) AS SELECT 1")]
    [InlineData("int) AS SELECT 1; DROP TABLE users; --")]
    [InlineData("int AS SELECT foo")]
    [InlineData("int foo")]
    [InlineData("int(")]
    [InlineData("int(1)(2)")]
    [InlineData("int(a)")]
    [InlineData("int(--)")]
    [InlineData("int(1-)")]
    [InlineData("(1)")]
    [InlineData("int[")]
    [InlineData("int[3]")]
    [InlineData("[]")]
    [InlineData("int[] foo")]
    [InlineData("\"open")]
    [InlineData("\"a\nb\"")]
    [InlineData("int\"x\"")]
    [InlineData("a.b.c")]
    [InlineData(".t")]
    [InlineData("t.")]
    [InlineData("int\nfoo")]
    [InlineData("int\n")]
    [InlineData("\"a\rb\"")]
    [InlineData("\"a\r\nb\"")]
    [InlineData("\"a\0b\"")]
    // Stricter than PostgreSQL's rule for a word on purpose: a "$" would let "1$$" open a dollar quote in the
    // PREPARE, and "1$1" be a parameter there.
    [InlineData("$type")]
    [InlineData("my$type")]
    [InlineData("1$$")]
    [InlineData("1$1")]
    [InlineData("€type")]
    public void Read_AnythingElse_IsNotATypeName(string text) =>
        DeclaredTypeText.Read(text).Failure.ShouldBe(DescribeFailures.NotATypeName, text);
}
