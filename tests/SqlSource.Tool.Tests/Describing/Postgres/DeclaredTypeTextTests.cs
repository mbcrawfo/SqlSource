using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The text of a -- param: type is put into SQL the tool sends, so it is read against the grammar of a type name
// first: a name, PostgreSQL's own type words, one modifier, and array marks.  Anything else never leaves the tool.
public class DeclaredTypeTextTests
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
    public void TryRead_TypeName_IsAccepted(string text)
    {
        DeclaredTypeText.TryRead(text, out var type).ShouldBeTrue(text);

        type.ShouldBe(text);
    }

    [Fact]
    public void TryRead_SpaceAroundTheName_IsLeftOut()
    {
        DeclaredTypeText.TryRead("  varchar(50) \t", out var type).ShouldBeTrue();

        type.ShouldBe("varchar(50)");
    }

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
    public void TryRead_AnythingElse_IsRefused(string text)
    {
        DeclaredTypeText.TryRead(text, out var type).ShouldBeFalse(text);

        type.ShouldBeNull();
    }
}
