using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The rows of all_types and of new_types that the seed of SQL Server's end-to-end database "app" holds, as the models
// of the tables read through Dapper have them: row 1 with a value in every column, row 2 with NULL in every column but
// id and, in all_types, rowversion_value, which the server fills in every row.  A row is read by reflection, so that
// every project that reads the tables shares the expectations, whatever SqlClient it runs.  This library cannot
// reference SqlClient, so a SqlVector<float> is compared through its Memory, and a rowversion by its length alone,
// since the server chooses its value.
internal static class SqlServerAllTypesRows
{
    public const int FullRowId = 1;

    public const int NullRowId = 2;

    // The properties of AllTypesDto, the model of all_types, in the order of the columns, each with its type and
    // whether it can be null, as the generator gives them on SqlClient 6.1 and 7 alike.  This library cannot reference
    // SqlClient, so the three properties of new_types, one of them a SqlVector<float>, are in the tests that read them.
    public static IReadOnlyList<(string Name, Type Type, bool Nullable)> AllTypesProperties { get; } =
    [
        ("Id", typeof(int), false),
        ("BigintValue", typeof(long?), true),
        ("IntValue", typeof(int?), true),
        ("SmallintValue", typeof(short?), true),
        ("TinyintValue", typeof(byte?), true),
        ("BitValue", typeof(bool?), true),
        ("DecimalValue", typeof(decimal?), true),
        ("NumericValue", typeof(decimal?), true),
        ("MoneyValue", typeof(decimal?), true),
        ("SmallmoneyValue", typeof(decimal?), true),
        ("FloatValue", typeof(double?), true),
        ("RealValue", typeof(float?), true),
        ("DateValue", typeof(DateTime?), true),
        ("DatetimeValue", typeof(DateTime?), true),
        ("Datetime2Value", typeof(DateTime?), true),
        ("SmalldatetimeValue", typeof(DateTime?), true),
        ("TimeValue", typeof(TimeSpan?), true),
        ("DatetimeoffsetValue", typeof(DateTimeOffset?), true),
        ("CharValue", typeof(string), true),
        ("VarcharValue", typeof(string), true),
        ("TextValue", typeof(string), true),
        ("NcharValue", typeof(string), true),
        ("NvarcharValue", typeof(string), true),
        ("NtextValue", typeof(string), true),
        ("XmlValue", typeof(string), true),
        ("BinaryValue", typeof(byte[]), true),
        ("VarbinaryValue", typeof(byte[]), true),
        ("ImageValue", typeof(byte[]), true),
        ("RowversionValue", typeof(byte[]), true),
        ("UniqueidentifierValue", typeof(Guid?), true),
        ("SqlVariantValue", typeof(object), true),
        ("SysnameValue", typeof(string), true),
    ];

    private const string Rowversion = "RowversionValue";

    private const string SqlVector = "Microsoft.Data.SqlTypes.SqlVector`1";

    // The expectation of a rowversion: 8 bytes, whose value the server chooses.  Declared before the rows that use it.
    private static readonly object AnyRowversion = new();

    // The values of row 1 of all_types, as SqlClient reads them back: date and smalldatetime as a DateTime at midnight
    // or on the minute, every DateTime of no kind, and the sql_variant as the int it holds.
    private static readonly Dictionary<string, object> FullRow = new(StringComparer.Ordinal)
    {
        ["Id"] = FullRowId,
        ["BigintValue"] = long.MinValue,
        ["IntValue"] = int.MaxValue,
        ["SmallintValue"] = short.MinValue,
        ["TinyintValue"] = byte.MaxValue,
        ["BitValue"] = true,
        ["DecimalValue"] = 12345678.1234m,
        ["NumericValue"] = -1234567.89m,
        ["MoneyValue"] = 922337203685477.5807m,
        ["SmallmoneyValue"] = -214748.3648m,
        ["FloatValue"] = -2.25,
        ["RealValue"] = 1.5f,
        ["DateValue"] = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Unspecified),
        ["DatetimeValue"] = new DateTime(2024, 3, 15, 13, 45, 30, 3, DateTimeKind.Unspecified),
        ["Datetime2Value"] = new DateTime(2024, 3, 15, 13, 45, 30, DateTimeKind.Unspecified).AddTicks(1234567),
        ["SmalldatetimeValue"] = new DateTime(2024, 3, 15, 13, 46, 0, DateTimeKind.Unspecified),
        ["TimeValue"] = new TimeSpan(0, 13, 45, 30).Add(TimeSpan.FromTicks(1234567)),
        ["DatetimeoffsetValue"] = new DateTimeOffset(2024, 3, 15, 13, 45, 30, TimeSpan.FromHours(2)).AddTicks(1234567),
        ["CharValue"] = "héllo",
        ["VarcharValue"] = "Grüße, café",
        ["TextValue"] = "Ünïcödé text",
        ["NcharValue"] = "Zoë日本",
        ["NvarcharValue"] = "Grüße, 日本語",
        ["NtextValue"] = "Ζωή, 日本語",
        ["XmlValue"] = """<root><item id="1">Zoë</item></root>""",
        ["BinaryValue"] = new byte[] { 0xde, 0xad, 0xbe, 0xef },
        ["VarbinaryValue"] = new byte[] { 0x00, 0xff, 0x10, 0xee, 0x2a },
        ["ImageValue"] = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a },
        [Rowversion] = AnyRowversion,
        ["UniqueidentifierValue"] = new Guid("123e4567-e89b-12d3-a456-426614174000"),
        ["SqlVariantValue"] = 42,
        ["SysnameValue"] = "a_name",
    };

    // The values of row 1 of new_types, which SQL Server 2025 alone has: the json as the server gives its text back.
    private static readonly Dictionary<string, object> FullNewTypesRow = new(StringComparer.Ordinal)
    {
        ["Id"] = FullRowId,
        ["JsonValue"] = /*lang=json,strict*/
            """{"name":"Zoë","tags":["a","b"]}""",
        ["VectorValue"] = new[] { 1.5f, -2.25f, 0.5f },
    };

    // Asserts that the row of all_types has the values of row 1 of the seed, with another id, and with null in the
    // properties named, whose columns a test did not write.  The rowversion is never null, so it cannot be named.
    public static void ShouldBeTheFullRow(object row, int id = FullRowId, IReadOnlySet<string>? nullProperties = null)
    {
        var properties = PropertiesOf(row, FullRow);
        nullProperties ??= new HashSet<string>(StringComparer.Ordinal);
        nullProperties.ShouldNotContain("Id");
        nullProperties.ShouldNotContain(Rowversion);
        nullProperties.ShouldBeSubsetOf(properties.Select(static property => property.Name));

        foreach (var property in properties)
        {
            var expected = property.Name switch
            {
                "Id" => id,
                _ when nullProperties.Contains(property.Name) => null,
                _ => FullRow[property.Name],
            };
            ShouldBeValue(property.GetValue(row), expected, property.Name);
        }
    }

    // Asserts that the row of all_types is row 2 of the seed, with another id, with null in every column but id and the
    // rowversion.
    public static void ShouldBeTheNullRow(object row, int id = NullRowId)
    {
        foreach (var property in PropertiesOf(row, FullRow))
        {
            var expected = property.Name switch
            {
                "Id" => id,
                Rowversion => FullRow[Rowversion],
                _ => null,
            };
            ShouldBeValue(property.GetValue(row), expected, property.Name);
        }
    }

    // Asserts that the row of new_types is row 1 of the seed, with another id.
    public static void ShouldBeTheFullNewTypesRow(object row, int id = FullRowId)
    {
        foreach (var property in PropertiesOf(row, FullNewTypesRow))
        {
            ShouldBeValue(
                property.GetValue(row),
                property.Name == "Id" ? id : FullNewTypesRow[property.Name],
                property.Name
            );
        }
    }

    // Asserts that the row of new_types is row 2 of the seed, with another id, with null in both columns.
    public static void ShouldBeTheNullNewTypesRow(object row, int id = NullRowId)
    {
        foreach (var property in PropertiesOf(row, FullNewTypesRow))
        {
            ShouldBeValue(property.GetValue(row), property.Name == "Id" ? id : null, property.Name);
        }
    }

    // The input model of a write of all_types or of new_types, made by its one constructor: the id given, and each
    // other parameter the property of its name of the row read, or null when no row is given.  The methods' tests on
    // SqlClient 6.1 and 7 write the seed's rows back through it.
    public static TArgs ArgsOf<TArgs>(int id, object? row)
    {
        var constructor = typeof(TArgs).GetConstructors().ShouldHaveSingleItem(typeof(TArgs).Name);
        var arguments = constructor
            .GetParameters()
            .Select(parameter => parameter.Name == "Id" ? id : ValueOf(row, parameter.Name!))
            .ToArray();
        return (TArgs)constructor.Invoke(arguments);
    }

    private static object? ValueOf(object? row, string property) =>
        row?.GetType().GetProperty(property).ShouldNotBeNull(property).GetValue(row);

    // The public properties of the row, which are those that the expectations name, no more and no fewer.
    private static PropertyInfo[] PropertiesOf(object row, Dictionary<string, object> expectations)
    {
        var properties = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        properties
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)
            .ShouldBe(expectations.Keys.Order(StringComparer.Ordinal), $"The properties of {row.GetType()}");
        return properties;
    }

    private static void ShouldBeValue(object? actual, object? expected, string path)
    {
        if (expected is null)
        {
            actual.ShouldBeNull(path);
            return;
        }

        actual = actual.ShouldNotBeNull(path);
        if (actual.GetType().IsGenericType && actual.GetType().GetGenericTypeDefinition().FullName == SqlVector)
        {
            var memory = actual.GetType().GetProperty("Memory").ShouldNotBeNull($"{path}.Memory").GetValue(actual);
            ((ReadOnlyMemory<float>)memory.ShouldNotBeNull($"{path}.Memory"))
                .ToArray()
                .ShouldBe(expected.ShouldBeOfType<float[]>(path), path);
            return;
        }

        if (ReferenceEquals(expected, AnyRowversion))
        {
            actual.ShouldBeOfType<byte[]>(path).Length.ShouldBe(8, path);
            return;
        }

        switch (expected)
        {
            case DateTime expectedDateTime:
                var dateTime = actual.ShouldBeOfType<DateTime>(path);
                dateTime.ShouldBe(expectedDateTime, path);
                dateTime.Kind.ShouldBe(expectedDateTime.Kind, path);
                break;
            case DateTimeOffset expectedDateTimeOffset:
                var dateTimeOffset = actual.ShouldBeOfType<DateTimeOffset>(path);
                dateTimeOffset.ShouldBe(expectedDateTimeOffset, path);
                dateTimeOffset.Offset.ShouldBe(expectedDateTimeOffset.Offset, path);
                break;
            default:
                actual.GetType().ShouldBe(expected.GetType(), path);
                actual.ShouldBe(expected, path);
                break;
        }
    }
}
