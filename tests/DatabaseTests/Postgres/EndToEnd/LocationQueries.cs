using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The one claim of Queries/Locations.sql, whose query and copies put their methods in each place by their markers: the
// extension class, the type, internal and private, and the nested class Sql.  The wrapper calls the private method,
// which only the type's own code can call.
[SqlSourceGenerate(Source = "Queries/Locations.sql")]
internal static partial class LocationQueries
{
    internal static Task<GetLocatedNameDto> ReadLocatedName(
        DbConnection connection,
        GetLocatedNameArgs parameters,
        CancellationToken cancellationToken
    ) => GetLocatedNamePrivate(connection, parameters, cancellationToken: cancellationToken);
}
