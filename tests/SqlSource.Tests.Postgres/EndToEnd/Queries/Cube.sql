-- The query of cube, the one type of the type map that all_types leaves out, claimed by CubeQueries: Npgsql 8 cannot
-- read it, and the Npgsql 8 project links AllTypes.sql alone, so it is written and read here, on Npgsql 10.

-- name: EchoCube
-- summary: The cube given, read back.
SELECT CAST(@value AS cube) AS value;
