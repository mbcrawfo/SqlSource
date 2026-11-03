using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NodaTime;
using SqlSource;

namespace PostgresExamples.CodeGen;

// The queries of DeliveryQueries.sql, the file named for the class.  Their methods are in the generated class
// DeliveryQueriesExtensions, and need a connection of StoreDataSource's.
[SqlSourceGenerate]
public sealed partial class DeliveryQueries(DbConnection connection)
{
    // rowcount returns the count of rows inserted.  deliveredAt is NodaTime's Instant, by the file's type override, and
    // location NetTopologySuite's Geometry, as the project references both plugins; details is sent as JSON.
    public Task<int> RecordDeliveryAsync(
        int orderId,
        Instant deliveredAt,
        Geometry location,
        DeliveryDetails details,
        CancellationToken cancellationToken = default
    ) =>
        connection.RecordDelivery(
            new RecordDeliveryParams(OrderId: orderId, DeliveredAt: deliveredAt, Location: location, Details: details),
            cancellationToken: cancellationToken
        );

    // one-optional returns the row, or null.  The column Details is a DeliveryDetails, read from its JSON.
    public Task<FindDeliveryDto?> FindDeliveryAsync(int orderId, CancellationToken cancellationToken = default) =>
        connection.FindDelivery(new FindDeliveryParams(OrderId: orderId), cancellationToken: cancellationToken);
}
