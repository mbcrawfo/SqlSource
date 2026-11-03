using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The method on the type as an internal static method, which any code of the project calls.
[SqlSourceGenerate(Source = "Queries/Locations.sql", MethodLocation = MethodLocation.Internal)]
internal static partial class InternalLocationQueries;

// The method on the type as a private static method, which only the type's own code calls, as its wrapper does.
[SqlSourceGenerate(Source = "Queries/Locations.sql", MethodLocation = MethodLocation.Private)]
internal static partial class PrivateLocationQueries;

internal static partial class PrivateLocationQueries
{
    internal static Task<GetLocatedNameDto> ReadLocatedName(
        DbConnection connection,
        GetLocatedNameArgs parameters,
        CancellationToken cancellationToken
    ) => GetLocatedName(connection, parameters, cancellationToken: cancellationToken);
}
