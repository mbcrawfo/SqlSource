using System.IO;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.TestHarness;

// The folder and schema of each engine's describer fixtures, read from the repository.
public sealed class DescriberFixturesTests
{
    [Theory]
    [InlineData("postgres")]
    [InlineData("mssql")]
    public void Folder_OfEachEngine_IsTheEnginesFolderUnderDescribers(string engine)
    {
        var fixtures = engine == "postgres" ? DescriberFixtures.Postgres : DescriberFixtures.SqlServer;

        fixtures.Folder.ShouldBe(
            RepositoryRoot.Path("tests", "SqlSource.Tool.Tests", "Fixtures", "Describers", engine)
        );
        fixtures.Schema.ShouldBe(Path.Combine(fixtures.Folder, "schema.sql"));
        File.Exists(fixtures.Schema).ShouldBeTrue();
    }
}
