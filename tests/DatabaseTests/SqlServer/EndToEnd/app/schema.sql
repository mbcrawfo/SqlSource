-- The schema of the database "app", which the end-to-end tests describe their queries against and run them on.
-- SqlServerContainer and tools/describe-end-to-end.sh apply it, then seed.sql, batch by batch between the lines that
-- hold GO alone.  Every batch runs on SQL Server 2017; the types that 2025 brings are in new-types.sql.  A change to it
-- is followed by a run of tools/describe-end-to-end.sh, which writes the sidecars again.

-- The alias type of accounts.email, which a query's parameter and column name.
CREATE TYPE dbo.Email FROM nvarchar(320) NOT NULL;
GO

-- The alias type over bigint of external_refs.external_id, which a query reads and sends as a value type.
CREATE TYPE dbo.ExternalId FROM bigint NOT NULL;
GO

-- A nullable column, a not null one, an identity, an alias type, a computed column and two defaults.
CREATE TABLE dbo.accounts (
    id int IDENTITY PRIMARY KEY,
    name nvarchar(100) NOT NULL,
    email dbo.Email NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'active',
    opened_on date NOT NULL,
    created_at datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    closed_at datetimeoffset NULL,
    name_length AS LEN(name)
);

-- An account with no note gives a LEFT JOIN its NULLs.
CREATE TABLE dbo.notes (
    id int IDENTITY PRIMARY KEY,
    account_id int NOT NULL REFERENCES dbo.accounts (id),
    kind varchar(10) NOT NULL,
    body nvarchar(max) NOT NULL,
    written_at datetime2(3) NOT NULL,
    remind_at time(0) NULL
);

-- One nullable column for each type that the base map gives a C# type and that SQL Server 2017 has, named for its
-- type.  Every column is read through Dapper into a generated model.
--
-- Left out: json and vector, which 2017 lacks, are in new-types.sql; hierarchyid, geography and geometry have no C#
-- type without Microsoft.SqlServer.Types.  rowversion is the one column that the server fills, in every row.
CREATE TABLE dbo.all_types (
    id int PRIMARY KEY,
    -- Numbers.
    bigint_value bigint NULL,
    int_value int NULL,
    smallint_value smallint NULL,
    tinyint_value tinyint NULL,
    bit_value bit NULL,
    decimal_value decimal(12, 4) NULL,
    numeric_value numeric(9, 2) NULL,
    money_value money NULL,
    smallmoney_value smallmoney NULL,
    float_value float NULL,
    real_value real NULL,
    -- Dates and times.
    date_value date NULL,
    datetime_value datetime NULL,
    datetime2_value datetime2(7) NULL,
    smalldatetime_value smalldatetime NULL,
    time_value time(7) NULL,
    datetimeoffset_value datetimeoffset(7) NULL,
    -- Text.
    char_value char(5) NULL,
    varchar_value varchar(20) NULL,
    text_value text NULL,
    nchar_value nchar(5) NULL,
    nvarchar_value nvarchar(20) NULL,
    ntext_value ntext NULL,
    xml_value xml NULL,
    -- Bytes.
    binary_value binary(4) NULL,
    varbinary_value varbinary(8) NULL,
    image_value image NULL,
    rowversion_value rowversion NULL,
    -- The rest.
    uniqueidentifier_value uniqueidentifier NULL,
    sql_variant_value sql_variant NULL,
    sysname_value sysname NULL
);

-- An id of an account in another system, of the alias type over bigint.
CREATE TABLE dbo.external_refs (
    id int PRIMARY KEY,
    account_id int NOT NULL REFERENCES dbo.accounts (id),
    external_id dbo.ExternalId NOT NULL
);

-- One nullable column for each date and time type that NodaTime's rows map, read and written by the queries of
-- EndToEnd/NodaTime/, whose folder turns NodaTime on, and three that their type overrides make an Instant, through
-- datetime2 and datetimeoffset, and a Duration, through time.  It has no seed: each test writes its rows in a
-- transaction that is rolled back.
CREATE TABLE dbo.noda_values (
    id int PRIMARY KEY,
    date_value date NULL,
    time_value time(7) NULL,
    datetime2_value datetime2(7) NULL,
    datetime_value datetime NULL,
    smalldatetime_value smalldatetime NULL,
    datetimeoffset_value datetimeoffset(7) NULL,
    instant_value datetime2(7) NULL,
    instant_offset_value datetimeoffset(7) NULL,
    duration_value time(7) NULL
);

-- A nullable column of each spatial type, read and written by the queries of EndToEnd/NetTopologySuite/, whose folder
-- turns NetTopologySuite on, and one more of each that their file header's type overrides make a Point, of geography,
-- and a Polygon, of geometry.  It has no seed: each test writes its rows in a transaction that is rolled back.
CREATE TABLE dbo.spatial_values (
    id int PRIMARY KEY,
    geography_value geography NULL,
    geometry_value geometry NULL,
    point_value geography NULL,
    polygon_value geometry NULL
);

-- One nullable column for each JSON type and class that the queries of Json/Queries/Json.sql read and send, under the
-- folder's settings file, which turns System.Text.Json on.  Every column is an nvarchar, which the library leaves a
-- string, so the queries' markers and the folder's column types give each its type: element, document, node, obj and
-- arr a type of System.Text.Json, settings_meta, header_meta, tags and marker_meta a class by the opt-in at each of its
-- places, camel a class with converters of its own, and plain_meta, an nvarchar(200), a class without the opt-in,
-- which SqlClient cannot read.  It has no seed: each test writes its rows in a transaction that is rolled back.
CREATE TABLE dbo.json_values (
    id int PRIMARY KEY,
    element nvarchar(max) NULL,
    document nvarchar(max) NULL,
    node nvarchar(max) NULL,
    obj nvarchar(max) NULL,
    arr nvarchar(max) NULL,
    settings_meta nvarchar(max) NULL,
    header_meta nvarchar(max) NULL,
    tags nvarchar(max) NULL,
    marker_meta nvarchar(max) NULL,
    camel nvarchar(max) NULL,
    plain_meta nvarchar(200) NULL
);
