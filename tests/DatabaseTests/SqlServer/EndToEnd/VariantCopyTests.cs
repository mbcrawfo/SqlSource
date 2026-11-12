using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// A copy of a query in its file, such as SearchAccountsUnpooled of Queries/Shapes.sql, is the query again under one
// setting, for a test that needs the two apart: the allocation tests of tests/DatabaseTests/QueryMethodAllocation send
// both through one hand-written SQL, the Dapper tests run the constants of the ForDapper copies, the method tests call
// the copies of a collection type, a suffix or a location, and SynchronousMethodTests, SynchronousBatchTests and
// SynchronousRepositoryTests run the synchronous methods of the copies under method-type, such as
// GetAccountInRangeSync, InsertNoteSync and GetNoteSync.  A query's hash is of its SQL, its declared parameters and
// its tokens' defaults, and the project does not build while an entry's hash is not its query's, so the committed
// entries of the two have one hash as long as the two queries have one SQL and one declaration of their parameters.
// No server is needed.
public sealed class VariantCopyTests
{
    [Theory]
    [InlineData("Shapes.sql", "SearchAccounts", "SearchAccountsUnpooled")]
    [InlineData("Shapes.sql", "SearchAccounts", "SearchAccountsSync")]
    [InlineData("Shapes.sql", "SearchAccountsUnpooled", "SearchAccountsUnpooledSync")]
    [InlineData("RowBuffers.sql", "NumbersAsArray", "NumbersAsArraySync")]
    [InlineData("RowBuffers.sql", "NumbersAsList", "NumbersAsListSync")]
    [InlineData("RowBuffers.sql", "NumbersAsIList", "NumbersAsIListSync")]
    [InlineData("RowBuffers.sql", "NumbersAsICollection", "NumbersAsICollectionSync")]
    [InlineData("RowBuffers.sql", "NumbersAsImmutableArray", "NumbersAsImmutableArraySync")]
    [InlineData("RowBuffers.sql", "NumbersNoPoolAsList", "NumbersNoPoolAsListSync")]
    [InlineData("RowBuffers.sql", "NumbersNoPoolAsIList", "NumbersNoPoolAsIListSync")]
    [InlineData("RowBuffers.sql", "NumbersNoPoolAsICollection", "NumbersNoPoolAsICollectionSync")]
    [InlineData("RowBuffers.sql", "NumbersNoPoolAsImmutableArray", "NumbersNoPoolAsImmutableArraySync")]
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
    [InlineData("Lists.sql", "ListAccountsByIds", "ListAccountsByIdsSync")]
    [InlineData("Methods.sql", "TouchAccount", "TouchAccountSync")]
    [InlineData("Methods.sql", "SetAccountStatus", "SetAccountStatusSync")]
    [InlineData("Methods.sql", "GetAccountInRange", "GetAccountInRangeSync")]
    [InlineData("Methods.sql", "GetAccountInRange", "GetAccountInRangeBoth")]
    [InlineData("Methods.sql", "FindAccountInRange", "FindAccountInRangeSync")]
    [InlineData("Methods.sql", "ListAccountsWhere", "ListAccountsWhereSync")]
    [InlineData("Methods.sql", "GetTwoThenFailConversion", "GetTwoThenFailConversionSync")]
    [InlineData("Methods.sql", "ListFailingAtSecondThenFailConversion", "ListFailingAtSecondThenFailConversionSync")]
    [InlineData("Methods.sql", "RunThenRaise", "RunThenRaiseSync")]
    [InlineData("Methods.sql", "GetThenRaise", "GetThenRaiseSync")]
    [InlineData("Notes.sql", "GetAccountNoteCount", "GetAccountNoteCountSync")]
    [InlineData("Streaming.sql", "StreamAccounts", "StreamAccountsBoth")]
    [InlineData("Repository.sql", "GetNote", "GetNoteSync")]
    [InlineData("Repository.sql", "GetNote", "GetNoteBoth")]
    [InlineData("Repository.sql", "ListNotes", "ListNotesSync")]
    [InlineData("Repository.sql", "TouchNote", "TouchNoteSync")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteNoTransaction")]
    [InlineData("Batches.sql", "InsertConvertedNote", "InsertConvertedNoteNoTransaction")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteInRepository")]
    [InlineData("Batches.sql", "InsertNoteRowCount", "InsertNoteQuietly")]
    [InlineData("Batches.sql", "FindAccountThenEmpty", "GetAccountThenEmpty")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteReadUncommitted")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteReadCommitted")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteRepeatableRead")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteSnapshot")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteSerializableInRepository")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteDefaultLevel")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteDefaultLevelInRepository")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteSync")]
    [InlineData("Batches.sql", "InsertNote", "InsertNoteBoth")]
    [InlineData("Batches.sql", "InsertNoteRowCount", "InsertNoteRowCountSync")]
    [InlineData("Batches.sql", "InsertNoteQuietly", "InsertNoteQuietlySync")]
    [InlineData("Batches.sql", "InsertConvertedNote", "InsertConvertedNoteSync")]
    [InlineData("Batches.sql", "FindNotes", "FindNotesSync")]
    [InlineData("Batches.sql", "FindNotes", "FindNotesStreamBoth")]
    [InlineData("Batches.sql", "FindNote", "FindNoteSync")]
    [InlineData("Batches.sql", "FindAccountWhenPositive", "FindAccountWhenPositiveSync")]
    [InlineData("Batches.sql", "FindAccountThenEmpty", "FindAccountThenEmptySync")]
    [InlineData("Batches.sql", "GetAccountThenEmpty", "GetAccountThenEmptySync")]
    [InlineData("Batches.sql", "FindAccountThenText", "FindAccountThenTextSync")]
    [InlineData("Batches.sql", "InsertNoteThenOther", "InsertNoteThenOtherSync")]
    [InlineData("Batches.sql", "InsertNoteSerializable", "InsertNoteSerializableSync")]
    [InlineData("Batches.sql", "InsertNoteReadUncommitted", "InsertNoteReadUncommittedSync")]
    [InlineData("Batches.sql", "InsertNoteReadCommitted", "InsertNoteReadCommittedSync")]
    [InlineData("Batches.sql", "InsertNoteRepeatableRead", "InsertNoteRepeatableReadSync")]
    [InlineData("Batches.sql", "InsertNoteSnapshot", "InsertNoteSnapshotSync")]
    [InlineData("Batches.sql", "InsertNoteNoTransaction", "InsertNoteNoTransactionSync")]
    [InlineData("Batches.sql", "InsertNoteDefaultLevel", "InsertNoteDefaultLevelSync")]
    public void Copy_CommittedEntry_HasTheHashOfItsOriginal(string file, string original, string copy) =>
        VariantCopies.ShouldShareHash(RepositoryPaths.EndToEndQueries, file, original, copy);

    // The copies of NodaTime/Queries/, whose folder turns NodaTime on.
    [Theory]
    [InlineData("NodaTime.sql", "InsertNodaValues", "InsertNodaValuesSync")]
    [InlineData("NodaTime.sql", "GetNodaValues", "GetNodaValuesSync")]
    public void Copy_CommittedEntryUnderNodaTime_HasTheHashOfItsOriginal(string file, string original, string copy) =>
        VariantCopies.ShouldShareHash(RepositoryPaths.EndToEndNodaTimeQueries, file, original, copy);
}
