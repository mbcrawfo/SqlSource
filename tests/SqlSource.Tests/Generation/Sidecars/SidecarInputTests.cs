using Shouldly;
using SqlSource.Generation.Sidecars;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Sidecars;

public sealed class SidecarInputTests
{
    private static readonly EquatableArray<string> Claimed = TestSidecars.Of("app/Orders.sql", "app/Users.sql");

    [Theory]
    [InlineData("/app/Orders.sql.json", "app/Orders.sql")]
    [InlineData("/app/Users.sql.json", "app/Users.sql")]
    [InlineData("/app/users.SQL.json", "app/Users.sql")]
    public void Create_SidecarOfAClaimedFile_HoldsTheFileAndThePathOfItsSqlFileAsClaimed(string path, string sqlPath)
    {
        var file = new InMemoryAdditionalText(path, "{}");

        var input = SidecarInput.Create(file, Claimed).ShouldNotBeNull();

        input.File.ShouldBeSameAs(file);
        input.SqlPath.ShouldBe(sqlPath);
    }

    [Theory]
    [InlineData("/app/Migrations/001_init.sql.json")]
    [InlineData("/../app/Users.sql.json")]
    public void Create_SidecarOfAFileNoTypeClaims_IsNull(string path) =>
        SidecarInput.Create(new InMemoryAdditionalText(path, "{}"), Claimed).ShouldBeNull();
}
