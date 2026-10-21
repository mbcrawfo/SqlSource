-- The schema every fixture case of the SQL Server describer is recorded against, and every container test of
-- tests/SqlSource.Tests.SqlServer runs against: that project creates the database fixtures and applies this file to
-- it through SqlServerScripts, which runs each batch between the lines that hold GO alone as one command of SqlClient.
-- SqlClient runs with QUOTED_IDENTIFIER on, which the persisted computed column of dbo.users needs.  A statement that
-- must start a batch, CREATE SCHEMA, CREATE TRIGGER or CREATE VIEW, has a GO before it and after it.  A change here
-- can change what the describer answers: record every case again with tools/record-fixtures.sh mssql.

CREATE TYPE dbo.Email FROM nvarchar(320) NOT NULL;

CREATE TYPE dbo.IdList AS TABLE (id int NOT NULL);
GO

CREATE SCHEMA billing;
GO

-- An identity, an alias type, a default, a computed and a persisted computed column, and a rowversion.
CREATE TABLE dbo.users (
    id int IDENTITY PRIMARY KEY,
    name nvarchar(100) NOT NULL,
    email dbo.Email,
    nickname nvarchar(50) NULL,
    credit decimal(10, 2) NOT NULL DEFAULT 0,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    deleted_at datetime2 NULL,
    name_len AS LEN(name),
    name_len_p AS CAST(LEN(name) AS int) PERSISTED NOT NULL,
    rv rowversion
);

CREATE TABLE dbo.orders (
    id int IDENTITY PRIMARY KEY,
    user_id int NOT NULL REFERENCES dbo.users (id),
    total decimal(18, 2) NOT NULL,
    note varchar(200) NULL
);

CREATE TABLE billing.invoices (
    id bigint NOT NULL PRIMARY KEY,
    order_id int NOT NULL,
    amount money NOT NULL
);

-- The canary of the tests that prove a text of the user's runs nothing: a statement that drops it fails them.
CREATE TABLE dbo.victim (id int);

-- A table with an AFTER trigger, for the result sets of sub-phase 6.2.
CREATE TABLE dbo.trig (
    id int NOT NULL PRIMARY KEY,
    v int NOT NULL
);
GO

CREATE TRIGGER dbo.trig_after ON dbo.trig AFTER INSERT AS SET NOCOUNT ON;
GO

-- A view over an outer join, whose columns of the outer side are nullable.
CREATE VIEW dbo.user_orders AS
SELECT u.id AS user_id, u.name, o.id AS order_id, o.total
FROM dbo.users AS u
LEFT JOIN dbo.orders AS o ON o.user_id = u.id;
GO

-- A system-versioned table, whose period columns the server fills.
CREATE TABLE dbo.prices (
    id int NOT NULL PRIMARY KEY,
    amount decimal(10, 2) NOT NULL,
    valid_from datetime2 GENERATED ALWAYS AS ROW START NOT NULL,
    valid_to datetime2 GENERATED ALWAYS AS ROW END NOT NULL,
    PERIOD FOR SYSTEM_TIME (valid_from, valid_to)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.prices_history));

-- One NOT NULL column of every type of the type table of the spike of phase 6, each named for its type, but json and
-- vector, which SQL Server 2017 lacks.
CREATE TABLE dbo.all_types (
    id int PRIMARY KEY,
    bigint_value bigint NOT NULL,
    int_value int NOT NULL,
    smallint_value smallint NOT NULL,
    tinyint_value tinyint NOT NULL,
    bit_value bit NOT NULL,
    decimal_18_4_value decimal(18, 4) NOT NULL,
    numeric_10_0_value numeric(10, 0) NOT NULL,
    money_value money NOT NULL,
    smallmoney_value smallmoney NOT NULL,
    float_value float NOT NULL,
    float_24_value float(24) NOT NULL,
    real_value real NOT NULL,
    date_value date NOT NULL,
    time_value time NOT NULL,
    time_3_value time(3) NOT NULL,
    datetime_value datetime NOT NULL,
    smalldatetime_value smalldatetime NOT NULL,
    datetime2_value datetime2 NOT NULL,
    datetime2_0_value datetime2(0) NOT NULL,
    datetimeoffset_value datetimeoffset NOT NULL,
    datetimeoffset_3_value datetimeoffset(3) NOT NULL,
    char_10_value char(10) NOT NULL,
    varchar_50_value varchar(50) NOT NULL,
    varchar_max_value varchar(max) NOT NULL,
    text_value text NOT NULL,
    nchar_10_value nchar(10) NOT NULL,
    nvarchar_50_value nvarchar(50) NOT NULL,
    nvarchar_max_value nvarchar(max) NOT NULL,
    ntext_value ntext NOT NULL,
    binary_16_value binary(16) NOT NULL,
    varbinary_50_value varbinary(50) NOT NULL,
    varbinary_max_value varbinary(max) NOT NULL,
    image_value image NOT NULL,
    uniqueidentifier_value uniqueidentifier NOT NULL,
    xml_value xml NOT NULL,
    sql_variant_value sql_variant NOT NULL,
    rowversion_value rowversion NOT NULL,
    hierarchyid_value hierarchyid NOT NULL,
    geography_value geography NOT NULL,
    geometry_value geometry NOT NULL,
    sysname_value sysname NOT NULL,
    dbo_email_value dbo.Email NOT NULL
);
