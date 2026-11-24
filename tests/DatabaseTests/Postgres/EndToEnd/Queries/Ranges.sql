-- The queries of a range of a user's own, reading_range, and its multirange, claimed by RangeQueries.  The header's
-- type overrides are the manual's mapping, NpgsqlRange<double> of the subtype float8, and an array of it for the
-- multirange, and the methods run on EndToEndSources.AppWithUnmappedTypes.
-- type-override: public.reading_range = NpgsqlTypes.NpgsqlRange<double>
-- type-override: public.reading_multirange = NpgsqlTypes.NpgsqlRange<double>[]

-- name: ListReadingsOverlapping -> many
-- summary: The readings whose span overlaps a range.
SELECT
    id,
    span,
    spans
FROM readings
WHERE span && @within
ORDER BY id;
