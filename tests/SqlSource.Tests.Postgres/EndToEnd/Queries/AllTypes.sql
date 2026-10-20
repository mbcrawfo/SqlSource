-- The queries of every type of the type map, claimed by AllTypesQueries.  Their rows are read into the default record
-- model, array columns included.  InsertAllTypes writes the columns whose parameter Dapper can send;
-- AllTypesQueryTests.WriteSideLeftOut names the others, each with the reason.

-- name: ListAllTypes
-- summary: Every row of all_types, with timestamptz given back to the type map.
-- type-override: timestamptz = default
SELECT *
FROM all_types
ORDER BY id;

-- name: InsertAllTypes
-- summary: Inserts a row of all_types with a parameter for each column that Dapper can send, and returns it.
-- type-override: timestamptz = default
INSERT INTO all_types (
    id,
    bool_value, int2_value, int4_value, int8_value, float4_value, float8_value, numeric_value, money_value,
    text_value, varchar_value, bpchar_value, name_value, char_value, citext_value, bytea_value,
    date_value, time_value, timestamp_value, timestamptz_value, interval_value,
    uuid_value,
    bit1_value, bit8_value, varbit_value,
    path_value, polygon_value,
    tsvector_value,
    hstore_value,
    int4multirange_value, int8multirange_value, nummultirange_value, tsmultirange_value, tstzmultirange_value,
    datemultirange_value,
    int4_array_value, text_array_value, date_array_value, email_value
) VALUES (
    @id,
    @boolValue, @int2Value, @int4Value, @int8Value, @float4Value, @float8Value, @numericValue, @moneyValue,
    @textValue, @varcharValue, @bpcharValue, @nameValue, @charValue, @citextValue, @byteaValue,
    @dateValue, @timeValue, @timestampValue, @timestamptzValue, @intervalValue,
    @uuidValue,
    @bit1Value, @bit8Value, @varbitValue,
    @pathValue, @polygonValue,
    @tsvectorValue,
    @hstoreValue,
    @int4multirangeValue, @int8multirangeValue, @nummultirangeValue, @tsmultirangeValue, @tstzmultirangeValue,
    @datemultirangeValue,
    @int4ArrayValue, @textArrayValue, @dateArrayValue, @emailValue
)
RETURNING *;
