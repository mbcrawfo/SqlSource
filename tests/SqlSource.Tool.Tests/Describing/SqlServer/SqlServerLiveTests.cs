using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The texts the live functions make themselves.  What they do with a server is in tests/DatabaseTests/SqlServer.
public sealed class SqlServerLiveTests
{
    [Fact]
    public void TableSelect_SchemaAndTable_AreEachInBrackets() =>
        SqlServerLive.TableSelect("dbo", "users").ShouldBe("SELECT * FROM [dbo].[users]");

    // A name the catalog gives may hold any character: a ] is written twice inside the brackets, and nothing else is
    // changed.
    [Fact]
    public void TableSelect_NameWithBracket_DoublesIt() =>
        SqlServerLive.TableSelect("od]d", "a]]b [c].d").ShouldBe("SELECT * FROM [od]]d].[a]]]]b [c]].d]");
}
