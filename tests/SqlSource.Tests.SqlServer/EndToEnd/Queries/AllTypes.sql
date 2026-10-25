-- The queries of every type of the type map that SQL Server 2017 has, claimed by AllTypesQueries.  Their rows are read
-- into the default record model, with date given back to the type map, a DateTime.  InsertAllTypes writes the columns
-- whose parameter Dapper can send exactly: not datetime2_value, which Dapper sends as a datetime, to 1/300 of a
-- second, nor rowversion_value, which the server fills.  WriteAllTypes writes every column but rowversion_value through
-- its method, which binds each parameter as its type; the queries after it bind the types whose binding has facets, in
-- a WHERE or a write.  tests/SqlSource.Tests.SqlServer.SqlClient61 links this file, so ListTinyintLevels names an
-- enum of tests/SqlSource.Testing, which both projects reference.

-- name: ListAllTypes
-- summary: Every row of all_types, with date given back to the type map.
-- type-override: date = default
SELECT *
FROM dbo.all_types
ORDER BY id;

-- name: InsertAllTypes
-- summary: Inserts a row of all_types with a parameter for each column that Dapper can send, and returns it.
-- type-override: date = default
INSERT INTO dbo.all_types (
    id,
    bigint_value, int_value, smallint_value, tinyint_value, bit_value, decimal_value, numeric_value, money_value,
    smallmoney_value, float_value, real_value,
    date_value, datetime_value, smalldatetime_value, time_value, datetimeoffset_value,
    char_value, varchar_value, text_value, nchar_value, nvarchar_value, ntext_value, xml_value,
    binary_value, varbinary_value, image_value,
    uniqueidentifier_value, sql_variant_value, sysname_value
)
OUTPUT INSERTED.*
VALUES (
    @id,
    @bigintValue, @intValue, @smallintValue, @tinyintValue, @bitValue, @decimalValue, @numericValue, @moneyValue,
    @smallmoneyValue, @floatValue, @realValue,
    @dateValue, @datetimeValue, @smalldatetimeValue, @timeValue, @datetimeoffsetValue,
    @charValue, @varcharValue, @textValue, @ncharValue, @nvarcharValue, @ntextValue, @xmlValue,
    @binaryValue, @varbinaryValue, @imageValue,
    @uniqueidentifierValue, @sqlVariantValue, @sysnameValue
);

-- name: WriteAllTypes -> one
-- summary: Inserts a row of all_types with a parameter for every column but the rowversion, which the server fills,
-- each of which can be null, and returns it.
-- type-override: date = default
-- param: @bigintValue null
-- param: @intValue null
-- param: @smallintValue null
-- param: @tinyintValue null
-- param: @bitValue null
-- param: @decimalValue null
-- param: @numericValue null
-- param: @moneyValue null
-- param: @smallmoneyValue null
-- param: @floatValue null
-- param: @realValue null
-- param: @dateValue null
-- param: @datetimeValue null
-- param: @datetime2Value null
-- param: @smalldatetimeValue null
-- param: @timeValue null
-- param: @datetimeoffsetValue null
-- param: @charValue null
-- param: @varcharValue null
-- param: @textValue null
-- param: @ncharValue null
-- param: @nvarcharValue null
-- param: @ntextValue null
-- param: @xmlValue null
-- param: @binaryValue null
-- param: @varbinaryValue null
-- param: @imageValue null
-- param: @uniqueidentifierValue null
-- param: @sqlVariantValue null
-- param: @sysnameValue null
INSERT INTO dbo.all_types (
    id,
    bigint_value, int_value, smallint_value, tinyint_value, bit_value, decimal_value, numeric_value, money_value,
    smallmoney_value, float_value, real_value,
    date_value, datetime_value, datetime2_value, smalldatetime_value, time_value, datetimeoffset_value,
    char_value, varchar_value, text_value, nchar_value, nvarchar_value, ntext_value, xml_value,
    binary_value, varbinary_value, image_value,
    uniqueidentifier_value, sql_variant_value, sysname_value
)
OUTPUT INSERTED.*
VALUES (
    @id,
    @bigintValue, @intValue, @smallintValue, @tinyintValue, @bitValue, @decimalValue, @numericValue, @moneyValue,
    @smallmoneyValue, @floatValue, @realValue,
    @dateValue, @datetimeValue, @datetime2Value, @smalldatetimeValue, @timeValue, @datetimeoffsetValue,
    @charValue, @varcharValue, @textValue, @ncharValue, @nvarcharValue, @ntextValue, @xmlValue,
    @binaryValue, @varbinaryValue, @imageValue,
    @uniqueidentifierValue, @sqlVariantValue, @sysnameValue
);

-- name: FindByRowversion
-- summary: The ids of the rows of a rowversion, a parameter that no insert can write.
SELECT id
FROM dbo.all_types
WHERE rowversion_value = @rowversion
ORDER BY id;

-- name: FindByDatetime2
-- summary: The ids of the rows of a datetime2(7), whose seventh digit a datetime would lose.
SELECT id
FROM dbo.all_types
WHERE datetime2_value = @value
ORDER BY id;

-- name: FindByChar
-- summary: The ids of the rows of a char(5), to which a longer value is compared whole.
SELECT id
FROM dbo.all_types
WHERE char_value = @value
ORDER BY id;

-- name: FindByDecimal
-- summary: The ids of the rows of a decimal(12,4), to whose scale a value with more is rounded.
SELECT id
FROM dbo.all_types
WHERE decimal_value = @value
ORDER BY id;

-- name: WriteDatetime2Of3 -> one
-- summary: Writes a value to a datetime2(3), which the server rounds, and reads it back.
DECLARE @written TABLE (value datetime2(3) NULL);
INSERT INTO @written (value) VALUES (@value);
SELECT value FROM @written;

-- name: ListTinyintLevels
-- summary: The tinyint values of the rows that hold a level, sent and read as a C# enum over byte.
-- type-override: tinyint = SqlSource.Testing.EndToEnd.Level
SELECT tinyint_value
FROM dbo.all_types
WHERE tinyint_value = @level
ORDER BY id;
