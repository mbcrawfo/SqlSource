using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The one claim of Queries/Locations.sql, whose query and copies put their methods in each place by their markers: the
// extension class, the default, the type, public, internal and private, and the nested class Sql.  A private method
// is one that only the type's own code can call: the tests call it through ListThroughWrapper.
[SqlSourceGenerate(Source = "Queries/Locations.sql")]
internal static partial class LocationQueries
{
    public static Task<IReadOnlyList<ListAccountNamesDto>> ListThroughWrapper(
        DbConnection connection,
        CancellationToken cancellationToken
    ) => ListAccountNamesPrivate(connection, cancellationToken: cancellationToken);
}
