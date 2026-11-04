using System.IO;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// A copy of a query in its file, such as GetAccountNameUnpooled of Queries/Accounts.sql, is the query again under one
// setting, for a test that needs the two apart: the allocation tests of tests/DatabaseTests/QueryMethodAllocation send
// both through one hand-written SQL, and the Dapper tests run the constants of the ForDapper copies, such as
// ListAllTypesForDapper, which are at the output models, since a codegen query has a method and no constant.  A
// query's hash is of its SQL, its declared parameters and its tokens' defaults, and the project does not build while an
// entry's hash is not its query's, so the committed entries of the two have one hash as long as the two queries have
// one SQL and one declaration of their parameters.  No server is needed.
public sealed class VariantCopyTests
{
    [Theory]
    [InlineData("Accounts.sql", "GetAccountName", "GetAccountNameUnpooled")]
    [InlineData("Accounts.sql", "ListAccounts", "ListAccountsUnpooled")]
    [InlineData("Accounts.sql", "ListClosedBefore", "ListClosedBeforeUnpooled")]
    [InlineData("AllTypes.sql", "ListAllTypes", "ListAllTypesForDapper")]
    [InlineData("AllTypes.sql", "InsertAllTypes", "InsertAllTypesForDapper")]
    [InlineData("Notes.sql", "ListNoteKinds", "ListNoteKindsForDapper")]
    public void Copy_CommittedEntry_HasTheHashOfItsOriginal(string file, string original, string copy)
    {
        var queries = JsonNode
            .Parse(File.ReadAllText(Path.Combine(RepositoryPaths.EndToEndQueries, file + ".json")))
            .ShouldNotBeNull()["queries"]
            .ShouldNotBeNull();
        var hash = queries[original].ShouldNotBeNull()["hash"].ShouldNotBeNull().GetValue<string>();

        queries[copy].ShouldNotBeNull()["hash"].ShouldNotBeNull().GetValue<string>().ShouldBe(hash);
    }
}
