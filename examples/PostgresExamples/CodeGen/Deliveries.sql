-- The queries of the deliveries, at the default output, codegen, which StoreQueries calls.  The project's type
-- override makes every timestamptz a DateTimeOffset, for Dapper, and would win over NodaTime, so the file's type
-- override gives timestamptz back to its default, and the file's timestamptz is NodaTime's Instant.  The project
-- references Npgsql's plugins for NodaTime and NetTopologySuite, so location, a geography, is NetTopologySuite's
-- Geometry.  details, a jsonb, is the project's class DeliveryDetails, which the methods read and send through
-- System.Text.Json.
-- type-override: timestamptz=default

-- name: RecordDelivery -> rowcount
-- summary: Records where and when an order arrived.
-- rowcount: the method returns the count of rows inserted.  A parameter written to a column has the column's type,
-- so @deliveredAt is an Instant and @location a Geometry with no marker; no column type reaches a parameter, so the
-- marker gives @details the class.
-- param: @details -> DeliveryDetails
INSERT INTO deliveries (order_id, delivered_at, location, details)
VALUES (@orderId, @deliveredAt, @location, @details);

-- name: FindDelivery -> one-optional
-- summary: The delivery of an order, or null when it has not arrived.
-- one-optional: the method returns the row, or null when there is none.  The marker gives the column details the
-- class.  order_id is left out: the table's columns in its own order would give the rows the table's model,
-- DeliveriesDto.
-- column: details -> DeliveryDetails
SELECT delivered_at, location, details
FROM deliveries
WHERE order_id = @orderId;
