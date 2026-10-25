using Shouldly;
using SqlSource.Generation.Emitting;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class LocalNamesTests
{
    [Fact]
    public void Take_NameNoTokenHas_IsTheName() =>
        new LocalNames(["orderBy", "connection"]).Take("sql").ShouldBe("sql");

    [Fact]
    public void Take_NameATokenHas_GetsTheFirstFreeNumber() =>
        new LocalNames(["sql", "sql1", "sql3"]).Take("sql").ShouldBe("sql2");

    [Fact]
    public void Take_SameNameTwice_GivesTwoNames()
    {
        var names = new LocalNames([]);

        var first = names.Take("row");
        var second = names.Take("row");

        first.ShouldBe("row");
        second.ShouldBe("row1");
    }
}
