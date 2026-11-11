using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// A copy of a query in its file, such as SearchAccountsUnpooled of Queries/Shapes.sql, is the query again under one
// setting, for a test that needs the two apart: the allocation tests of tests/DatabaseTests/QueryMethodAllocation send
// both through one hand-written SQL, the Dapper tests run the constants of the ForDapper copies, and the method tests
// call the copies of a collection type, a suffix or a location.  A query's hash is of its SQL, its declared parameters
// and its tokens' defaults, and the project does not build while an entry's hash is not its query's, so the committed
// entries of the two have one hash as long as the two queries have one SQL and one declaration of their parameters.
// No server is needed.
public sealed class VariantCopyTests
{
    [Theory]
    [InlineData("Shapes.sql", "SearchAccounts", "SearchAccountsUnpooled")]
    [InlineData("AllTypes.sql", "ListAllTypes", "ListAllTypesForDapper")]
    [InlineData("AllTypes.sql", "InsertAllTypes", "InsertAllTypesForDapper")]
    [InlineData("NewTypes.sql", "ListNewTypes", "ListNewTypesForDapper")]
    [InlineData("Writes.sql", "AddNote", "AddNoteForDapper")]
    [InlineData("Notes.sql", "ListMarkedNames", "ListMarkedNamesForDapper")]
    [InlineData("Locations.sql", "ListAccountNames", "ListAccountNamesPublic")]
    [InlineData("Locations.sql", "ListAccountNames", "ListAccountNamesInternal")]
    [InlineData("Locations.sql", "ListAccountNames", "ListAccountNamesPrivate")]
    [InlineData("Locations.sql", "ListAccountNames", "ListAccountNamesNested")]
    [InlineData("Locations.sql", "ListAccountNames", "ListAccountNamesWithSuffix")]
    [InlineData("Collections/Claimed.sql", "ListCollectedAccounts", "ListCollectedAccountsAsIEnumerable")]
    [InlineData("Collections/Claimed.sql", "ListCollectedAccounts", "ListCollectedAccountsAsICollection")]
    [InlineData("Collections/Claimed.sql", "ListCollectedAccounts", "ListCollectedAccountsAsIReadOnlyCollection")]
    [InlineData("Collections/Claimed.sql", "ListCollectedAccounts", "ListCollectedAccountsAsList")]
    [InlineData("Collections/Claimed.sql", "ListCollectedAccounts", "ListCollectedAccountsAsImmutableList")]
    [InlineData("Lists.sql", "ListAccountsByIds", "StreamAccountsByIds")]
    [InlineData("Lists.sql", "ListAccountsByIds", "ListAccountsByIdsNoPadding")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteNoTransaction")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteInRepository")]
    [InlineData("Batches.sql", "InsertNoteRowCount", "InsertNoteQuietly")]
    [InlineData("Batches.sql", "FindAccountThenEmpty", "GetAccountThenEmpty")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteReadUncommitted")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteReadCommitted")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteRepeatableRead")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteSnapshot")]
    public void Copy_CommittedEntry_HasTheHashOfItsOriginal(string file, string original, string copy) =>
        VariantCopies.ShouldShareHash(RepositoryPaths.EndToEndQueries, file, original, copy);
}
