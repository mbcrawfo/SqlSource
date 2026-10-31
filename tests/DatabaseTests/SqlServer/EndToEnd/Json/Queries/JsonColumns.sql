-- The queries of json_columns, claimed by JsonQueries here and in SqlServer.SqlClient61.  The table is made by
-- EndToEnd/app/new-types.sql on a server of major 17 or later alone, so this file is described on SQL Server 2025
-- alone, and a check against an earlier server leaves it out.  The folder's settings file turns System.Text.Json on,
-- so element, a json, is the library's JsonElement, and the header's column type gives meta, a json too, a class of
-- the project, read and sent through the serializer, which needs no opt-in on json.
-- column: json_columns.meta -> OrderMetadata

-- name: InsertJsonColumns
-- summary: Inserts a row of json_columns with a parameter for each column, either of which may be null.
-- param: @element null
-- param: @meta null -> OrderMetadata
INSERT INTO dbo.json_columns (id, element, meta)
VALUES (@id, @element, @meta);

-- name: GetJsonColumns -> one
-- summary: The row of json_columns with the id, both columns read.
SELECT id, element, meta
FROM dbo.json_columns
WHERE id = @id;
