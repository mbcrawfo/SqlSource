-- The queries of every type of the type map that SQL Server 2017 has, claimed by AllTypesQueries.  Their rows are read
-- into the default record model, with date given back to the type map, a DateTime.  InsertAllTypes writes the columns
-- whose parameter Dapper can send exactly: not datetime2_value, which Dapper sends as a datetime, to 1/300 of a
-- second, nor rowversion_value, which the server fills.

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
