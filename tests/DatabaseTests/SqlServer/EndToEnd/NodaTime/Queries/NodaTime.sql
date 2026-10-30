-- The queries of noda_values' six columns of the types that NodaTime's rows map, claimed by NodaTimeQueries here and
-- in SqlServer.SqlClient61.  The folder's settings file turns NodaTime on, so every column and parameter takes
-- NodaTime's type, read through SqlClient's own type and the generated NodaTimeConversions, and sent converted back.

-- name: InsertNodaValues
-- summary: Inserts a row of noda_values with a parameter for each of the six columns, any of which may be null.
-- param: @dateValue null
-- param: @timeValue null
-- param: @datetime2Value null
-- param: @datetimeValue null
-- param: @smalldatetimeValue null
-- param: @datetimeoffsetValue null
INSERT INTO dbo.noda_values (
    id, date_value, time_value, datetime2_value, datetime_value, smalldatetime_value, datetimeoffset_value
) VALUES (
    @id, @dateValue, @timeValue, @datetime2Value, @datetimeValue, @smalldatetimeValue, @datetimeoffsetValue
);

-- name: GetNodaValues -> one
-- summary: The row of noda_values with the id, its six columns of the types that NodaTime's rows map.
SELECT id, date_value, time_value, datetime2_value, datetime_value, smalldatetime_value, datetimeoffset_value
FROM dbo.noda_values
WHERE id = @id;
