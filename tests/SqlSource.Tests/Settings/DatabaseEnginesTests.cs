using System.Linq;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class DatabaseEnginesTests
{
    [Fact]
    public void All_IsEveryMemberInDeclarationOrder() =>
        DatabaseEngines.All.ShouldBe([DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer]);

    // PerEngine<T> stores a slot for each engine by its number.
    [Fact]
    public void All_MembersRunFromZeroWithNoGaps() =>
        DatabaseEngines.All.Select(engine => (int)engine).ShouldBe(Enumerable.Range(0, DatabaseEngines.All.Length));
}
