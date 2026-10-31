-- The queries of json_values, claimed by JsonQueries here and in SqlServer.SqlClient61.  Every column is an nvarchar,
-- which stays a string with System.Text.Json on, so the header's column types give element, document, node, obj and arr
-- a type of System.Text.Json and camel a class with converters of its own, and the opt-in, "-> json", gives
-- header_meta and tags a class here, settings_meta in the folder's settings file, and marker_meta in each query that
-- reads it and by the parameter's marker, each read and sent through the serializer.  A method reads and sends each
-- type of System.Text.Json through a generated conversion from and to the string that SqlClient reads.
-- column: json_values.element -> System.Text.Json.JsonElement
-- column: json_values.document -> System.Text.Json.JsonDocument
-- column: json_values.node -> System.Text.Json.Nodes.JsonNode
-- column: json_values.obj -> System.Text.Json.Nodes.JsonObject
-- column: json_values.arr -> System.Text.Json.Nodes.JsonArray
-- column: json_values.header_meta -> json OrderMetadata
-- column: json_values.tags -> json System.Collections.Generic.List<Tag>
-- column: json_values.camel -> CamelMetadata

-- name: InsertJsonValues
-- summary: Inserts a row of json_values with a parameter for each column but plain_meta, any of which may be null.
-- A parameter is no column of a table, so each marker gives its parameter the type of its column.
-- param: @element null -> System.Text.Json.JsonElement
-- param: @document null -> System.Text.Json.JsonDocument
-- param: @node null -> System.Text.Json.Nodes.JsonNode
-- param: @obj null -> System.Text.Json.Nodes.JsonObject
-- param: @arr null -> System.Text.Json.Nodes.JsonArray
-- param: @settingsMeta null -> json OrderMetadata
-- param: @headerMeta null -> json OrderMetadata
-- param: @tags null -> json System.Collections.Generic.List<Tag>
-- param: @markerMeta null -> json OrderMetadata
-- param: @camel null -> CamelMetadata
INSERT INTO dbo.json_values (
    id, element, document, node, obj, arr, settings_meta, header_meta, tags, marker_meta, camel
) VALUES (
    @id, @element, @document, @node, @obj, @arr, @settingsMeta, @headerMeta, @tags, @markerMeta, @camel
);

-- name: GetJsonValues -> one
-- summary: The row of json_values with the id, every column but plain_meta read.
-- column: marker_meta -> json OrderMetadata
SELECT id, element, document, node, obj, arr, settings_meta, header_meta, tags, marker_meta, camel
FROM dbo.json_values
WHERE id = @id;

-- name: GetRequiredJson -> one
-- summary: The settings_meta and obj of the row of json_values with the id, each read as a value that cannot be null.
-- A JSON null in either is no SQL NULL, so it reaches the read, which throws for a property that cannot be null.
-- column: settings_meta not null
-- column: obj not null
SELECT settings_meta, obj
FROM dbo.json_values
WHERE id = @id;

-- name: GetPlainMeta -> one
-- summary: The plain_meta of the row of json_values with the id, a class on an nvarchar(200) without the opt-in.
-- With no converter and no opt-in, the method asks SqlClient for the class itself, which it cannot read.
-- column: plain_meta -> OrderMetadata
SELECT plain_meta
FROM dbo.json_values
WHERE id = @id;
