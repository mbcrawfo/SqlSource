namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The collection type of each level that sets it: the project's settings file, the file headers and a query's marker.
// The attribute sets none, since a file gives every claim the same settings.  Each file is claimed once, with its
// methods on the type.  The files share the output model CollectedAccount.

// The project's settings file, IReadOnlyList: neither a header nor a marker sets one.  The copies of
// ListCollectedAccounts in the file, such as ListCollectedAccountsAsList, set theirs by a query's marker, and their
// methods are on the type too.
[SqlSourceGenerate(Source = "Queries/Collections/Claimed.sql", MethodLocation = MethodLocation.Public)]
internal static partial class CollectionQueries;

// The file header of ByMetadata.sql, ImmutableArray, over the project's settings file.
[SqlSourceGenerate(Source = "Queries/Collections/ByMetadata.sql", MethodLocation = MethodLocation.Public)]
internal static partial class HeaderCollectionQueries;

// The markers of the file header and of the queries, over the project's settings file.
[SqlSourceGenerate(Source = "Queries/Collections/ByMarkers.sql", MethodLocation = MethodLocation.Public)]
internal static partial class MarkerCollectionQueries;
