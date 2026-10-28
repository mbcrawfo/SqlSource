using System.IO;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// SearchAccountsUnpooled of Queries/Shapes.sql is SearchAccounts again under no-array-pool, for the allocation tests,
// which send both through one hand-written SQL.  A query's hash is of its SQL alone, and the project does not build
// while an entry's hash is not its query's, so the committed entries of the two have one hash as long as the two
// queries have one SQL.  No server is needed.
public sealed class UnpooledCopyTests
{
    [Fact]
    public void SearchAccountsUnpooled_CommittedEntry_HasTheHashOfSearchAccounts()
    {
        var queries = JsonNode
            .Parse(File.ReadAllText(Path.Combine(RepositoryPaths.EndToEndQueries, "Shapes.sql.json")))
            .ShouldNotBeNull()["queries"]
            .ShouldNotBeNull();
        var hash = queries["SearchAccounts"].ShouldNotBeNull()["hash"].ShouldNotBeNull().GetValue<string>();

        queries["SearchAccountsUnpooled"].ShouldNotBeNull()["hash"].ShouldNotBeNull().GetValue<string>().ShouldBe(hash);
    }
}
