using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// A copy of a query in its file, such as GetAccountNameUnpooled of Queries/Accounts.sql, is the query again under one
// setting, for a test that needs the two apart: the allocation tests of tests/DatabaseTests/QueryMethodAllocation send
// both through one hand-written SQL, the Dapper tests run the constants of the ForDapper copies, such as
// ListAllTypesForDapper, which are at the output models, since a methods query has a method and no constant, and
// SynchronousMethodTests runs the synchronous methods of the copies under method-type, such as
// GetAccountInRangeSync.  A query's hash is of its SQL, its declared parameters and its tokens' defaults, and the
// project does not build while an entry's hash is not its query's, so the committed entries of the two have one hash
// as long as the two queries have one SQL and one declaration of their parameters.  No server is needed.
public sealed class VariantCopyTests
{
    [Theory]
    [InlineData("Accounts.sql", "GetAccountName", "GetAccountNameUnpooled")]
    [InlineData("Accounts.sql", "ListAccounts", "ListAccountsUnpooled")]
    [InlineData("Accounts.sql", "ListClosedBefore", "ListClosedBeforeUnpooled")]
    [InlineData("AllTypes.sql", "ListAllTypes", "ListAllTypesForDapper")]
    [InlineData("AllTypes.sql", "InsertAllTypes", "InsertAllTypesForDapper")]
    [InlineData("Notes.sql", "ListNoteKinds", "ListNoteKindsForDapper")]
    [InlineData("Locations.sql", "GetLocatedName", "GetLocatedNameInternal")]
    [InlineData("Locations.sql", "GetLocatedName", "GetLocatedNamePrivate")]
    [InlineData("Locations.sql", "GetLocatedName", "GetLocatedNameNested")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteNoTransaction")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteInRepository")]
    [InlineData("Batches.sql", "InsertNoteRowCount", "InsertNoteQuietly")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteReadCommitted")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteRepeatableRead")]
    [InlineData("Lists.sql", "ListAccountsByIds", "StreamAccountsByIds")]
    [InlineData("Lists.sql", "ListAccountsByIds", "ListAccountsByIdsNoPadding")]
    [InlineData("Lists.sql", "ListAccountsByIds", "ListAccountsByIdsSync")]
    [InlineData("Methods.sql", "TouchAccount", "TouchAccountSync")]
    [InlineData("Methods.sql", "TouchAccountQuietly", "TouchAccountQuietlySync")]
    [InlineData("Methods.sql", "GetAccountInRange", "GetAccountInRangeSync")]
    [InlineData("Methods.sql", "GetAccountInRange", "GetAccountInRangeBoth")]
    [InlineData("Methods.sql", "FindAccountInRange", "FindAccountInRangeSync")]
    [InlineData("Methods.sql", "ListAccountsWhere", "ListAccountsWhereSync")]
    [InlineData("Methods.sql", "GetThenRaise", "GetThenRaiseSync")]
    [InlineData("Notes.sql", "GetAccountNoteCount", "GetAccountNoteCountSync")]
    [InlineData("Streaming.sql", "StreamAccounts", "StreamAccountsBoth")]
    public void Copy_CommittedEntry_HasTheHashOfItsOriginal(string file, string original, string copy) =>
        VariantCopies.ShouldShareHash(RepositoryPaths.EndToEndQueries, file, original, copy);
}
