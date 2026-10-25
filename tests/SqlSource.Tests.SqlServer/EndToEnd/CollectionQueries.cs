namespace SqlSource.Tests.SqlServer.EndToEnd;

// The collection type of each level that sets it, each class with its methods on itself, so that the claims of one file
// do not give one extension method twice.  The files share the output model CollectedAccount.

// The project's property, IReadOnlyList: neither the attribute, the metadata nor a marker sets one.
[SqlSourceGenerate(Path = "Queries/Collections/Claimed.sql", MethodLocation = MethodLocation.Public)]
internal static partial class CollectionQueries;

// The attribute's, over the project's property.
[SqlSourceGenerate(
    Path = "Queries/Collections/Claimed.sql",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.IEnumerable
)]
internal static partial class EnumerableCollectionQueries;

[SqlSourceGenerate(
    Path = "Queries/Collections/Claimed.sql",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.ICollection
)]
internal static partial class CollectionInterfaceCollectionQueries;

[SqlSourceGenerate(
    Path = "Queries/Collections/Claimed.sql",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.IReadOnlyCollection
)]
internal static partial class ReadOnlyCollectionQueries;

[SqlSourceGenerate(
    Path = "Queries/Collections/Claimed.sql",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.List
)]
internal static partial class ListCollectionQueries;

[SqlSourceGenerate(
    Path = "Queries/Collections/Claimed.sql",
    MethodLocation = MethodLocation.Public,
    CollectionType = GeneratorCollectionType.ImmutableList
)]
internal static partial class ImmutableListCollectionQueries;

// The item metadata of the file, ImmutableArray, over the project's property.
[SqlSourceGenerate(Path = "Queries/Collections/ByMetadata.sql", MethodLocation = MethodLocation.Public)]
internal static partial class MetadataCollectionQueries;

// The markers of the file header and of the queries, over the project's property.
[SqlSourceGenerate(Path = "Queries/Collections/ByMarkers.sql", MethodLocation = MethodLocation.Public)]
internal static partial class MarkerCollectionQueries;
