-- The queries of the deliveries, at the default output, codegen, which StoreQueries calls.  The project's library
-- types turn on NodaTime, NetTopologySuite and System.Text.Json, so delivered_at, a datetimeoffset, is NodaTime's
-- OffsetDateTime, location, a geography, NetTopologySuite's Geometry, and events, a json, a JsonElement.  details, a
-- json too, is the project's class DeliveryDetails.  The methods convert each of them themselves, so nothing is
-- registered on the connection.

-- name: RecordDelivery -> rowcount
-- summary: Records where and when an order arrived.
-- rowcount: the method returns the count of rows inserted.  SQL Server gives a parameter written to a column the
-- column's type, so @deliveredAt is an OffsetDateTime, @location a Geometry and @events a JsonElement with no marker;
-- no column type reaches a parameter, so the marker gives @details the class.
-- param: @details -> DeliveryDetails
INSERT INTO deliveries (order_id, delivered_at, location, details, events)
VALUES (@orderId, @deliveredAt, @location, @details, @events);

-- name: FindDelivery -> one-optional
-- summary: The delivery of an order, or null when it has not arrived.
-- one-optional: the method returns the row, or null when there is none.  The marker gives the column details the
-- class.  order_id is left out: the table's columns in its own order would give the rows the table's model,
-- DeliveriesDto.
-- column: details -> DeliveryDetails
SELECT delivered_at, location, details, events
FROM deliveries
WHERE order_id = @orderId;
