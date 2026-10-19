-- The queries of every type of the type map, claimed by AllTypesQueries.  Their rows are read into class models: Dapper
-- cannot construct a positional record with an array column.
-- output-model-type: class

-- name: ListAllTypes
-- summary: Every row of all_types, with timestamptz given back to the type map.
-- type-override: timestamptz = default
SELECT *
FROM all_types
ORDER BY id;

-- name: InsertAllTypes
-- summary: Inserts a row of all_types with a parameter for each column, and returns it.
-- type-override: timestamptz = default
INSERT INTO all_types (
    id,
    bool_value, int2_value, int4_value, int8_value, float4_value, float8_value, numeric_value, money_value,
    text_value, varchar_value, bpchar_value, name_value, char_value, citext_value, bytea_value,
    date_value, time_value, timetz_value, timestamp_value, timestamptz_value, interval_value,
    uuid_value, json_value, jsonb_value, jsonpath_value, xml_value,
    inet_value, cidr_value, macaddr_value, macaddr8_value,
    bit1_value, bit8_value, varbit_value,
    point_value, line_value, lseg_value, box_value, path_value, polygon_value, circle_value,
    tsvector_value, tsquery_value,
    oid_value, xid_value, cid_value, regtype_value, regconfig_value, xid8_value, tid_value, pg_lsn_value,
    int2vector_value, oidvector_value, refcursor_value,
    hstore_value, ltree_value, lquery_value, ltxtquery_value,
    int4range_value, int8range_value, numrange_value, tsrange_value, tstzrange_value, daterange_value,
    int4multirange_value, int8multirange_value, nummultirange_value, tsmultirange_value, tstzmultirange_value,
    datemultirange_value,
    int4_array_value, text_array_value, date_array_value, status_array_value, email_value, status_value
) VALUES (
    @id,
    @boolValue, @int2Value, @int4Value, @int8Value, @float4Value, @float8Value, @numericValue, @moneyValue,
    @textValue, @varcharValue, @bpcharValue, @nameValue, @charValue, @citextValue, @byteaValue,
    @dateValue, @timeValue, @timetzValue, @timestampValue, @timestamptzValue, @intervalValue,
    @uuidValue, @jsonValue, @jsonbValue, @jsonpathValue, @xmlValue,
    @inetValue, @cidrValue, @macaddrValue, @macaddr8Value,
    @bit1Value, @bit8Value, @varbitValue,
    @pointValue, @lineValue, @lsegValue, @boxValue, @pathValue, @polygonValue, @circleValue,
    @tsvectorValue, @tsqueryValue,
    @oidValue, @xidValue, @cidValue, @regtypeValue, @regconfigValue, @xid8Value, @tidValue, @pgLsnValue,
    @int2vectorValue, @oidvectorValue, @refcursorValue,
    @hstoreValue, @ltreeValue, @lqueryValue, @ltxtqueryValue,
    @int4rangeValue, @int8rangeValue, @numrangeValue, @tsrangeValue, @tstzrangeValue, @daterangeValue,
    @int4multirangeValue, @int8multirangeValue, @nummultirangeValue, @tsmultirangeValue, @tstzmultirangeValue,
    @datemultirangeValue,
    @int4ArrayValue, @textArrayValue, @dateArrayValue, @statusArrayValue, @emailValue, @statusValue
)
RETURNING *;
