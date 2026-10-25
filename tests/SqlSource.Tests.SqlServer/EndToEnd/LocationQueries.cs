using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// Each MethodLocation of Queries/Locations.sql, and the suffix of the generator parameter async-method-suffix.

// The default, ExtensionClass: an extension of the connection in LocationQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Locations.sql")]
internal static partial class LocationQueries;

[SqlSourceGenerate(Path = "Queries/Locations.sql", MethodLocation = MethodLocation.Public)]
internal static partial class PublicLocationQueries;

[SqlSourceGenerate(Path = "Queries/Locations.sql", MethodLocation = MethodLocation.Internal)]
internal static partial class InternalLocationQueries;

// A private method, which only the type's own code can call: the tests call it through ListThroughWrapper.
[SqlSourceGenerate(Path = "Queries/Locations.sql", MethodLocation = MethodLocation.Private)]
internal static partial class PrivateLocationQueries
{
    public static Task<IReadOnlyList<ListAccountNamesDto>> ListThroughWrapper(
        DbConnection connection,
        CancellationToken cancellationToken
    ) => ListAccountNames(connection, cancellationToken: cancellationToken);
}

// The method's name ends in Async: ListAccountNamesAsync.
[SqlSourceGenerate(
    Path = "Queries/Locations.sql",
    MethodLocation = MethodLocation.Public,
    Parameters = "async-method-suffix"
)]
internal static partial class SuffixLocationQueries;
