-- The queries of json_values, claimed by JsonQueries here and in Postgres.Npgsql8.  The folder's settings file turns
-- System.Text.Json on, so element, element_json and elements are the library's JsonElement and JsonElement[]; the
-- column types below give the other columns a JSON type or a class of the project, which a method reads and sends
-- through the driver, a generated conversion, the serializer or the class's converters, and text_meta, a text, a class
-- by the opt-in.
-- column: json_values.document -> System.Text.Json.JsonDocument
-- column: json_values.node -> System.Text.Json.Nodes.JsonNode
-- column: json_values.obj -> System.Text.Json.Nodes.JsonObject
-- column: json_values.arr -> System.Text.Json.Nodes.JsonArray
-- column: json_values.meta -> OrderMetadata
-- column: json_values.metas -> OrderMetadata[]
-- column: json_values.tags -> System.Collections.Generic.List<Tag>
-- column: json_values.camel -> CamelMetadata
-- column: json_values.text_meta -> json OrderMetadata

-- name: InsertJsonValues
-- summary: Inserts a row of json_values with a parameter for each column, any of which may be null.
-- A parameter is no column of a table, so each marker gives its parameter the type of its column.
-- param: @element null
-- param: @elementJson null
-- param: @elements null
-- param: @document null -> System.Text.Json.JsonDocument
-- param: @node null -> System.Text.Json.Nodes.JsonNode
-- param: @obj null -> System.Text.Json.Nodes.JsonObject
-- param: @arr null -> System.Text.Json.Nodes.JsonArray
-- param: @meta null -> OrderMetadata
-- param: @metas null -> OrderMetadata[]
-- param: @tags null -> System.Collections.Generic.List<Tag>
-- param: @camel null -> CamelMetadata
-- param: @textMeta null -> json OrderMetadata
INSERT INTO json_values (
    id, element, element_json, elements, document, node, obj, arr, meta, metas, tags, camel, text_meta
) VALUES (
    @id, @element, @elementJson, @elements, @document, @node, @obj, @arr, @meta, @metas, @tags, @camel, @textMeta
);

-- name: GetJsonValues -> one
-- summary: The row of json_values with the id, every column read.
SELECT *
FROM json_values
WHERE id = @id;

-- name: GetRequiredJson -> one
-- summary: The meta and node of the row of json_values with the id, each read as a value that cannot be null.
-- A JSON null in either is no SQL NULL, so it reaches the read, which throws for a property that cannot be null.
-- column: meta not null
-- column: node not null
SELECT meta, node
FROM json_values
WHERE id = @id;
