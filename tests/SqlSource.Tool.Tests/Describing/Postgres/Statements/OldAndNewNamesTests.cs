using Shouldly;
using SqlSource.Tool.Describing.Postgres.Statements;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Statements;

// The names under which PostgreSQL 18 may print the old and the new row of a RETURNING list, as 18.6 reads them:
// old and new, or the names a WITH gives, each folded, unquoted or decoded as the server keeps a name.
public sealed class OldAndNewNamesTests
{
    [Theory]
    [InlineData("UPDATE t SET v = 1 RETURNING old.v", 17)]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS o) o.v", 14)]
    [InlineData("UPDATE t SET v = 1 RETURNING v", 18)]
    [InlineData("UPDATE t SET v = 1 RETURNING \"Old\".v", 18)]
    [InlineData("SELECT 'old', $$new$$ -- old", 18)]
    public void Of_StatementBefore18OrThatNamesNeitherRow_IsNone(string sql, int major) =>
        OldAndNewNames.Of(sql, major).MayBeNamed.ShouldBeFalse();

    [Theory]
    [InlineData("UPDATE t SET v = 1 RETURNING old.v", "old", "new")]
    [InlineData("UPDATE t SET v = 1 RETURNING \"new\".v", "old", "new")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS o, NEW AS n) o.v, n.v", "old", "new", "o", "n")]
    [InlineData("UPDATE t SET v = 1 returning with ( old as Prev ) prev.v", "old", "new", "prev")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS \"Prev\") \"Prev\".v", "old", "new", "Prev")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS \"a\"\"b\") 1", "old", "new", "a\"b")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS U&\"\\0061b\") ab.v", "old", "new", "ab")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS U&\"!0061c\" UESCAPE '!') ac.v", "old", "new", "ac")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD /* c */ AS -- c\n p) p.v", "old", "new", "p")]
    // Every list's names count together.
    [InlineData(
        "WITH x AS (UPDATE t SET v = 1 RETURNING WITH (OLD AS a) a.v) INSERT INTO s SELECT 1 FROM x "
            + "RETURNING WITH (NEW AS b) b.w",
        "old",
        "new",
        "a",
        "b"
    )]
    public void Of_StatementOf18_NamesOldNewAndEachNameAWithGives(string sql, params string[] names)
    {
        var read = OldAndNewNames.Of(sql, 18);

        read.MayBeNamed.ShouldBeTrue();
        foreach (var name in names)
        {
            read.MayName(name).ShouldBeTrue(name);
        }

        read.MayName("t").ShouldBeFalse();
        read.MayName("OLD").ShouldBeFalse();
        read.MayName("x").ShouldBeFalse();
    }

    // A version that names no major is read as 18 or later.
    [Fact]
    public void Of_UnknownMajor_IsReadAs18()
    {
        var read = OldAndNewNames.Of("UPDATE t SET v = 1 RETURNING NEW.v", null);

        read.MayName("new").ShouldBeTrue();
        read.MayName("t").ShouldBeFalse();
    }

    // The server cuts a name at 63 bytes, and the plan prints it cut.
    [Fact]
    public void Of_LongName_IsCutAsTheServerCutsIt()
    {
        var read = OldAndNewNames.Of($"UPDATE t SET v = 1 RETURNING WITH (OLD AS {new string('o', 70)}) 1", 18);

        read.MayName(new string('o', 63)).ShouldBeTrue();
        read.MayName(new string('o', 70)).ShouldBeFalse();
    }

    // A WITH this reader cannot follow, or a name it cannot decode, may name the rows anything.
    [Theory]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS U&\"\\zz\") 1")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD o) o.v")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH OLD AS o")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS o NEW AS n) 1")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS 1) 1")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS o")]
    // A name outside ASCII: a server of a single-byte encoding folds it by its locale and cuts it by its own bytes.
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS Ö) ö.v")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (NEW AS \"é\") 1")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS U&\"\\00e9\") 1")]
    public void Of_WithThatCannotBeRead_IsUnknown(string sql)
    {
        var read = OldAndNewNames.Of(sql, 18);

        read.MayBeNamed.ShouldBeTrue();
        read.MayName("t").ShouldBeTrue();
    }

    [Fact]
    public void NoneAndUnknown_MayNameNothingAndAnything()
    {
        OldAndNewNames.None.MayBeNamed.ShouldBeFalse();
        OldAndNewNames.None.MayName("old").ShouldBeFalse();
        OldAndNewNames.Unknown.MayName("t").ShouldBeTrue();
    }
}
