-- The queries of noda_values, claimed by NodaTimeQueries here and in Postgres.Npgsql8.  The folder's settings file
-- leaves NodaTime on, so every column and parameter takes NodaTime's type, read and written through Npgsql.NodaTime on
-- a data source that calls UseNodaTime().

-- name: InsertNodaValues
-- summary: Inserts a row of noda_values with a parameter for each column, any of which may be null.
-- param: @timestamptzValue null
-- param: @timestampValue null
-- param: @dateValue null
-- param: @timeValue null
-- param: @timetzValue null
-- param: @intervalValue null
-- param: @tstzrangeValue null
-- param: @tsrangeValue null
-- param: @daterangeValue null
-- param: @tstzmultirangeValue null
-- param: @tsmultirangeValue null
-- param: @datemultirangeValue null
-- param: @timestamptzArrayValue null
-- param: @timestampArrayValue null
-- param: @dateArrayValue null
-- param: @timeArrayValue null
-- param: @timetzArrayValue null
-- param: @intervalArrayValue null
-- param: @tstzrangeArrayValue null
-- param: @tsrangeArrayValue null
-- param: @daterangeArrayValue null
INSERT INTO noda_values (
    id,
    timestamptz_value, timestamp_value, date_value, time_value, timetz_value, interval_value,
    tstzrange_value, tsrange_value, daterange_value, tstzmultirange_value, tsmultirange_value, datemultirange_value,
    timestamptz_array_value, timestamp_array_value, date_array_value, time_array_value, timetz_array_value,
    interval_array_value, tstzrange_array_value, tsrange_array_value, daterange_array_value
) VALUES (
    @id,
    @timestamptzValue, @timestampValue, @dateValue, @timeValue, @timetzValue, @intervalValue,
    @tstzrangeValue, @tsrangeValue, @daterangeValue, @tstzmultirangeValue, @tsmultirangeValue, @datemultirangeValue,
    @timestamptzArrayValue, @timestampArrayValue, @dateArrayValue, @timeArrayValue, @timetzArrayValue,
    @intervalArrayValue, @tstzrangeArrayValue, @tsrangeArrayValue, @daterangeArrayValue
);

-- name: GetNodaValues -> one
-- summary: The row of noda_values with the id, every column read.
SELECT *
FROM noda_values
WHERE id = @id;
