-- The queries of noda_values' three columns that the file header's type overrides make NodaTime's Instant, through
-- datetime2 and datetimeoffset, and its Duration, through time, which no row of NodaTime maps.  An override to those
-- types takes the generated conversion whether NodaTime is on or not.
-- type-override: datetime2 = NodaTime.Instant
-- type-override: datetimeoffset = NodaTime.Instant
-- type-override: time = NodaTime.Duration

-- name: InsertNodaOverrides
-- summary: Inserts a row of noda_values with a parameter for each of the three columns, any of which may be null.
-- param: @instantValue null
-- param: @instantOffsetValue null
-- param: @durationValue null
INSERT INTO dbo.noda_values (id, instant_value, instant_offset_value, duration_value)
VALUES (@id, @instantValue, @instantOffsetValue, @durationValue);

-- name: GetNodaOverrides -> one
-- summary: The row of noda_values with the id, its three columns of the types that the overrides name.
SELECT id, instant_value, instant_offset_value, duration_value
FROM dbo.noda_values
WHERE id = @id;
