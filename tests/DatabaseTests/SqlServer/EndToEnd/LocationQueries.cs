using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// Each MethodLocation of Queries/Locations.sql.

// The default, ExtensionClass: an extension of the connection in LocationQueriesExtensions.
[SqlSourceGenerate(Source = "Queries/Locations.sql")]
internal static partial class LocationQueries;

[SqlSourceGenerate(Source = "Queries/Locations.sql", MethodLocation = MethodLocation.Public)]
internal static partial class PublicLocationQueries;

[SqlSourceGenerate(Source = "Queries/Locations.sql", MethodLocation = MethodLocation.Internal)]
internal static partial class InternalLocationQueries;

// A private method, which only the type's own code can call: the tests call it through ListThroughWrapper.
[SqlSourceGenerate(Source = "Queries/Locations.sql", MethodLocation = MethodLocation.Private)]
internal static partial class PrivateLocationQueries
{
    public static Task<IReadOnlyList<ListAccountNamesDto>> ListThroughWrapper(
        DbConnection connection,
        CancellationToken cancellationToken
    ) => ListAccountNames(connection, cancellationToken: cancellationToken);
}
