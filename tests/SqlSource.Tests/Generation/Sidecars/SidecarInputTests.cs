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
    [InlineData("/app/Users.sql.json")]
    [InlineData("/app/users.SQL.json")]
    public void Create_SidecarOfAClaimedFile_HoldsTheFileAndThePathOfItsSqlFileAsClaimed(string path)
    {
        var file = new InMemoryAdditionalText(path, "{}");

        var input = SidecarInput.Create(file, Claimed).ShouldNotBeNull();

        input.File.ShouldBeSameAs(file);
        input.SqlPath.ShouldBe("app/Users.sql");
    }

    [Theory]
    [InlineData("/app/Migrations/001_init.sql.json")]
    [InlineData("/../app/Users.sql.json")]
    public void Create_SidecarOfAFileNoTypeClaims_IsNull(string path) =>
        SidecarInput.Create(new InMemoryAdditionalText(path, "{}"), Claimed).ShouldBeNull();
}
