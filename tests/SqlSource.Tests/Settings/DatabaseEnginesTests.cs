using System.Linq;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class DatabaseEnginesTests
{
    [Fact]
    public void All_OfTheEnum_IsEveryMemberInDeclarationOrder() =>
        DatabaseEngines.All.ShouldBe([DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer]);

    // PerEngine<T> stores a slot for each engine by its number.
    [Fact]
    public void All_OfTheEnum_RunsFromZeroWithNoGaps() =>
        DatabaseEngines.All.Select(engine => (int)engine).ShouldBe(Enumerable.Range(0, DatabaseEngines.All.Length));
}
