-- The schema of the database "app", which the end-to-end tests describe their queries against and run them on.
-- PostgresContainer and tools/describe-end-to-end.sh apply it, then seed.sql.  A change to it is followed by a run of
-- tools/describe-end-to-end.sh, which writes the sidecars again.

-- The extensions whose types all_types reads, and cube, which it leaves out: Npgsql 8 cannot read it, so the query
-- EchoCube of Queries/Cube.sql writes and reads it alone, on Npgsql 10.
CREATE EXTENSION citext;
CREATE EXTENSION cube;
CREATE EXTENSION hstore;
CREATE EXTENSION ltree;

-- The enum that is read as a string everywhere, in accounts and in all_types.
CREATE TYPE account_status AS ENUM ('active', 'suspended', 'closed');

-- The enum that the queries map to a C# enum.
CREATE TYPE note_kind AS ENUM ('call', 'email', 'visit');

CREATE DOMAIN email AS text CHECK (VALUE LIKE '%@%');

-- The id that the queries read and send as a value type of the project, through its converters.
CREATE DOMAIN note_ref AS bigint;

-- A nullable column, a not null one, an identity, a generated column and a default.
CREATE TABLE accounts (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL,
    email email NOT NULL,
    status account_status NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    closed_at timestamptz,
    name_length integer GENERATED ALWAYS AS (length(name)) STORED
);

-- An account with no note gives a LEFT JOIN its NULLs, and a note with no ref a NULL of the domain.
CREATE TABLE notes (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    account_id integer NOT NULL REFERENCES accounts (id),
    kind note_kind NOT NULL,
    body text NOT NULL,
    written_at timestamptz NOT NULL,
    ref note_ref
);

-- One nullable column for each type that the base map gives a C# type on both Npgsql 8 and Npgsql 10 and that
-- PostgreSQL 14 has, named for its type.  Every column is written and read through the generated methods, and read
-- through Dapper into a generated model.
--
-- Left out: cube, as above.  The registered types that Npgsql reads only by a cast to oid (regclass and the others),
-- pg_snapshot, txid_snapshot and pg_node_tree, which it reads only by a cast to text, and record, unknown and void,
-- which no table can hold.
CREATE TABLE all_types (
    id integer PRIMARY KEY,
    -- Numbers.
    bool_value boolean,
    int2_value smallint,
    int4_value integer,
    int8_value bigint,
    float4_value real,
    float8_value double precision,
    numeric_value numeric(12, 4),
    money_value money,
    -- Text.
    text_value text,
    varchar_value varchar(20),
    bpchar_value char(5),
    name_value name,
    char_value "char",
    citext_value citext,
    bytea_value bytea,
    -- Dates and times.
    date_value date,
    time_value time,
    timetz_value timetz,
    timestamp_value timestamp,
    timestamptz_value timestamptz,
    interval_value interval,
    -- Identifiers and documents.
    uuid_value uuid,
    json_value json,
    jsonb_value jsonb,
    jsonpath_value jsonpath,
    xml_value xml,
    -- Network addresses.
    inet_value inet,
    cidr_value cidr,
    macaddr_value macaddr,
    macaddr8_value macaddr8,
    -- Bit strings.
    bit1_value bit(1),
    bit8_value bit(8),
    varbit_value varbit,
    -- Geometry.
    point_value point,
    line_value line,
    lseg_value lseg,
    box_value box,
    path_value path,
    polygon_value polygon,
    circle_value circle,
    -- Text search.
    tsvector_value tsvector,
    tsquery_value tsquery,
    -- The types of the catalogs.
    oid_value oid,
    xid_value xid,
    cid_value cid,
    regtype_value regtype,
    regconfig_value regconfig,
    xid8_value xid8,
    tid_value tid,
    pg_lsn_value pg_lsn,
    int2vector_value int2vector,
    oidvector_value oidvector,
    refcursor_value refcursor,
    -- The types of the extensions.
    hstore_value hstore,
    ltree_value ltree,
    lquery_value lquery,
    ltxtquery_value ltxtquery,
    -- Ranges.
    int4range_value int4range,
    int8range_value int8range,
    numrange_value numrange,
    tsrange_value tsrange,
    tstzrange_value tstzrange,
    daterange_value daterange,
    -- Multiranges.
    int4multirange_value int4multirange,
    int8multirange_value int8multirange,
    nummultirange_value nummultirange,
    tsmultirange_value tsmultirange,
    tstzmultirange_value tstzmultirange,
    datemultirange_value datemultirange,
    -- Arrays, a domain and an enum.
    int4_array_value integer[],
    text_array_value text[],
    date_array_value date[],
    status_array_value account_status[],
    email_value email,
    status_value account_status
);

-- One nullable column for each type that NodaTime's rows map, and an array of each of those that is not a multirange,
-- named for its type.  The queries of NodaTime/Queries/NodaTime.sql write and read every column through
-- Npgsql.NodaTime, with NodaTime's types; all_types keeps the base map's.
CREATE TABLE noda_values (
    id integer PRIMARY KEY,
    timestamptz_value timestamptz,
    timestamp_value timestamp,
    date_value date,
    time_value time,
    timetz_value timetz,
    interval_value interval,
    tstzrange_value tstzrange,
    tsrange_value tsrange,
    daterange_value daterange,
    tstzmultirange_value tstzmultirange,
    tsmultirange_value tsmultirange,
    datemultirange_value datemultirange,
    timestamptz_array_value timestamptz[],
    timestamp_array_value timestamp[],
    date_array_value date[],
    time_array_value time[],
    timetz_array_value timetz[],
    interval_array_value interval[],
    tstzrange_array_value tstzrange[],
    tsrange_array_value tsrange[],
    daterange_array_value daterange[]
);

-- One nullable column for each JSON type and class that the queries of Json/Queries/Json.sql read and send, under the
-- folder's settings file, which turns System.Text.Json on: element, element_json and elements take the library's
-- JsonElement, the column types of the file's header give the others, and text_meta, a text, holds a class by the
-- opt-in.
CREATE TABLE json_values (
    id integer PRIMARY KEY,
    element jsonb,
    element_json json,
    elements jsonb[],
    document jsonb,
    node jsonb,
    obj jsonb,
    arr jsonb,
    meta jsonb,
    metas jsonb[],
    tags jsonb,
    camel jsonb,
    text_meta text
);
