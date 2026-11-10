using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NodaTime;
using SqlSource;

namespace SqlServerExamples.Methods;

// The queries of DeliveryQueries.sql, the file named for the class.  Their methods are in the generated class
// DeliveryQueriesExtensions.
[SqlSourceGenerate]
public sealed partial class DeliveryQueries(DbConnection connection)
{
    // rowcount returns the count of rows inserted.  deliveredAt is NodaTime's OffsetDateTime, location
    // NetTopologySuite's Geometry and events a JsonElement, by the project's library types; details is sent as JSON.
    // Nothing is registered on the connection: the method converts each of them itself.
    public Task<int> RecordDeliveryAsync(
        int orderId,
        OffsetDateTime deliveredAt,
        Geometry location,
        DeliveryDetails details,
        JsonElement events,
        CancellationToken cancellationToken = default
    ) =>
        connection.RecordDelivery(
            new RecordDeliveryParams(
                OrderId: orderId,
                DeliveredAt: deliveredAt,
                Location: location,
                Details: details,
                Events: events
            ),
            cancellationToken: cancellationToken
        );

    // one-optional returns the row, or null.  The column Details is a DeliveryDetails, read from its JSON.
    public Task<FindDeliveryDto?> FindDeliveryAsync(int orderId, CancellationToken cancellationToken = default) =>
        connection.FindDelivery(new FindDeliveryParams(OrderId: orderId), cancellationToken: cancellationToken);
}
