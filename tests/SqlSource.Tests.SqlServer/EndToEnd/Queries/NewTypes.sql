-- The query of the types that SQL Server 2025 brings, json and vector, claimed by NewTypeQueries.  The table is made
-- by EndToEnd/app/new-types.sql on a server of major 17 or later alone, so this file is described on SQL Server 2025
-- alone, and a check against SQL Server 2017 leaves it out.

-- name: ListNewTypes
-- summary: Every row of new_types.
SELECT *
FROM dbo.new_types
ORDER BY id;
