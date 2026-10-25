-- The queries of the types that SQL Server 2025 brings, json and vector, claimed by NewTypesQueries.  The table is made
-- by EndToEnd/app/new-types.sql on a server of major 17 or later alone, so this file is described on SQL Server 2025
-- alone, and a check against SQL Server 2017 leaves it out.  WriteNewTypes, GetNewType and MeasureVectorDistance run
-- through their methods.

-- name: ListNewTypes
-- summary: Every row of new_types.
SELECT *
FROM dbo.new_types
ORDER BY id;

-- name: WriteNewTypes -> one
-- summary: Inserts a row of new_types, each column of which can be null, and returns it.
-- param: @jsonValue null
-- param: @vectorValue null
INSERT INTO dbo.new_types (id, json_value, vector_value)
OUTPUT INSERTED.*
VALUES (@id, @jsonValue, @vectorValue);

-- name: GetNewType -> one
-- summary: The vector of one row of new_types, which the marker says is never null, so that a NULL throws.
-- column: v not null
SELECT id, vector_value AS v
FROM dbo.new_types
WHERE id = @id;

-- name: MeasureVectorDistance -> one
-- summary: The cosine distance from the vector of one row to a probe that can be null, whose type the marker gives,
-- since the server infers none.  VECTOR_DISTANCE refuses a NULL of any type but vector.
-- param: @probe vector(3) null
SELECT VECTOR_DISTANCE('cosine', vector_value, @probe) AS distance
FROM dbo.new_types
WHERE id = @id;
