using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using Shouldly;

namespace SqlSource.Testing.EndToEnd;

// The two rows of all_types that the seed of the end-to-end database "app" holds, as a model of the table read through
// Dapper has them: row 1 with a value in every column, row 2 with NULL in every column but id.  A row is read by
// reflection, so that every project that reads the table shares the expectations, whatever driver it runs.  Each
// expectation is the value of a property, by its name, for the property's type: a date is a DateOnly on Npgsql 10 and a
// DateTime on Npgsql 8.  This library cannot reference Npgsql, so a value of NpgsqlTypes is compared by its text under
// the invariant culture, and so is a cidr, which is an NpgsqlCidr on Npgsql 8.
internal static class AllTypesRows
{
    public const int FullRowId = 1;

    public const int NullRowId = 2;

    private const string NpgsqlTypes = "NpgsqlTypes";

    // The values of row 1 of the seed, as Npgsql reads them back: the jsonpath and the lquery in the server's canonical
    // text, the regtype and the regconfig as the oids of integer and simple, which are the same on every major.
    private static readonly Dictionary<string, Func<Type, object?>> FullRow = new(StringComparer.Ordinal)
    {
        ["Id"] = static _ => FullRowId,
        ["BoolValue"] = static _ => true,
        ["Int2Value"] = static _ => short.MinValue,
        ["Int4Value"] = static _ => int.MaxValue,
        ["Int8Value"] = static _ => long.MinValue,
        ["Float4Value"] = static _ => 1.5f,
        ["Float8Value"] = static _ => -2.25,
        ["NumericValue"] = static _ => 12345678.1234m,
        ["MoneyValue"] = static _ => 1234.56m,
        ["TextValue"] = static _ => "Grüße, 日本語",
        ["VarcharValue"] = static _ => "Ünïcödé",
        ["BpcharValue"] = static _ => "héllo",
        ["NameValue"] = static _ => "a_name",
        ["CharValue"] = static _ => 'A',
        ["CitextValue"] = static _ => "Mixed Case",
        ["ByteaValue"] = static _ => new byte[] { 0xde, 0xad, 0xbe, 0xef, 0x00, 0xff },
        ["DateValue"] = static type =>
            ByDriver(
                ScalarOf(type),
                (typeof(DateOnly), new DateOnly(2024, 3, 15)),
                (typeof(DateTime), new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Unspecified))
            ),
        ["TimeValue"] = static type =>
            ByDriver(
                ScalarOf(type),
                (typeof(TimeOnly), new TimeOnly(13, 45, 30, 123, 456)),
                (typeof(TimeSpan), new TimeSpan(0, 13, 45, 30, 123, 456))
            ),
        ["TimetzValue"] = static _ => new DateTimeOffset(1, 1, 2, 13, 45, 30, 123, 456, TimeSpan.FromHours(2)),
        ["TimestampValue"] = static _ => new DateTime(2024, 3, 15, 13, 45, 30, 123, 456, DateTimeKind.Unspecified),
        ["TimestamptzValue"] = static _ => new DateTime(2024, 3, 15, 13, 45, 30, 123, 456, DateTimeKind.Utc),
        ["IntervalValue"] = static _ => new TimeSpan(3, 4, 5, 6, 789, 12),
        ["UuidValue"] = static _ => new Guid("123e4567-e89b-12d3-a456-426614174000"),
        ["JsonValue"] = static _ => /*lang=json,strict*/
            """{"name": "Zoë", "tags": ["a", "b"]}""",
        ["JsonbValue"] = static _ => /*lang=json,strict*/
            """{"name": "Zoë", "tags": ["a", "b"]}""",
        ["JsonpathValue"] = static _ => "$.\"name\"",
        ["XmlValue"] = static _ => """<root><item id="1">Zoë</item></root>""",
        ["InetValue"] = static _ => IPAddress.Parse("192.168.1.10"),
        ["CidrValue"] = static _ => "192.168.100.128/25",
        ["MacaddrValue"] = static _ => PhysicalAddress.Parse("08-00-2B-01-02-03"),
        ["Macaddr8Value"] = static _ => PhysicalAddress.Parse("08-00-2B-01-02-03-04-05"),
        ["Bit1Value"] = static _ => true,
        ["Bit8Value"] = static _ => Bits("10101010"),
        ["VarbitValue"] = static _ => Bits("1011001"),
        ["PointValue"] = static _ => "(1.5,-2.25)",
        ["LineValue"] = static _ => "{1,-1,0}",
        ["LsegValue"] = static _ => "[(0,0),(1.5,2.5)]",
        ["BoxValue"] = static _ => "(3,4),(1,2)",
        ["PathValue"] = static _ => "[(0,0),(1,1),(2,0)]",
        ["PolygonValue"] = static _ => "((0,0),(4,0),(4,3))",
        ["CircleValue"] = static _ => "<(1,2),3>",
        ["TsvectorValue"] = static _ => "'fat':2 'rat':3",
        ["TsqueryValue"] = static _ => "'fat' & 'rat'",
        ["OidValue"] = static _ => uint.MaxValue,
        ["XidValue"] = static _ => uint.MaxValue,
        ["CidValue"] = static _ => 12345u,
        ["RegtypeValue"] = static _ => 23u,
        ["RegconfigValue"] = static _ => 3748u,
        ["Xid8Value"] = static _ => ulong.MaxValue,
        ["TidValue"] = static _ => "(0,1)",
        ["PgLsnValue"] = static _ => "16/B374D848",
        ["Int2vectorValue"] = static _ => new short[] { 1, 2, 3 },
        ["OidvectorValue"] = static _ => new uint[] { 4, 5, 6 },
        ["RefcursorValue"] = static _ => "cursor_1",
        ["HstoreValue"] = static _ => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["k1"] = "v1",
            ["k2"] = "Zoë",
            ["k3"] = null,
        },
        ["LtreeValue"] = static _ => "Top.Science.Astronomy",
        ["LqueryValue"] = static _ => "Top.*{,2}.Astronomy",
        ["LtxtqueryValue"] = static _ => "Science & Astronomy",
        ["Int4rangeValue"] = static _ => "[1,10)",
        ["Int8rangeValue"] = static _ => "[1,100)",
        ["NumrangeValue"] = static _ => "(1.5,10.25]",
        ["TsrangeValue"] = static _ => "[01/01/2024 00:00:00,02/01/2024 00:00:00)",
        ["TstzrangeValue"] = static _ => "[01/01/2024 00:00:00,02/01/2024 00:00:00)",
        ["DaterangeValue"] = static type =>
            ByDriver(
                SubtypeOf(type),
                (typeof(DateOnly), "[01/01/2024,02/01/2024)"),
                (typeof(DateTime), "[01/01/2024 00:00:00,02/01/2024 00:00:00)")
            ),
        ["Int4multirangeValue"] = static _ => new[] { "[1,5)", "[10,20)" },
        ["Int8multirangeValue"] = static _ => new[] { "[1,5)", "[10,20)" },
        ["NummultirangeValue"] = static _ => new[] { "(1.5,2.5]", "[4,5)" },
        ["TsmultirangeValue"] = static _ =>
            new[] { "[01/01/2024 00:00:00,01/02/2024 00:00:00)", "[02/01/2024 00:00:00,02/02/2024 00:00:00)" },
        ["TstzmultirangeValue"] = static _ =>
            new[] { "[01/01/2024 00:00:00,01/02/2024 00:00:00)", "[02/01/2024 00:00:00,02/02/2024 00:00:00)" },
        ["DatemultirangeValue"] = static type =>
            ByDriver(
                SubtypeOf(type),
                (typeof(DateOnly), new[] { "[01/01/2024,01/05/2024)", "[02/01/2024,02/05/2024)" }),
                (
                    typeof(DateTime),
                    new[] { "[01/01/2024 00:00:00,01/05/2024 00:00:00)", "[02/01/2024 00:00:00,02/05/2024 00:00:00)" }
                )
            ),
        ["Int4ArrayValue"] = static _ => new[] { 1, 2, 3 },
        ["TextArrayValue"] = static _ => new[] { "a", "b c", "Zoë" },
        ["DateArrayValue"] = static type =>
            ByDriver(
                type.GetElementType()!,
                (typeof(DateOnly), new[] { new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31) }),
                (
                    typeof(DateTime),
                    new[]
                    {
                        new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                        new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Unspecified),
                    }
                )
            ),
        ["StatusArrayValue"] = static _ => new[] { "active", "closed" },
        ["EmailValue"] = static _ => "zoe@example.com",
        ["StatusValue"] = static _ => "suspended",
    };

    // Asserts that the row has the values of row 1 of the seed, with another id, and with null in the properties named,
    // whose columns a test did not write.
    public static void ShouldBeTheFullRow(object row, int id = FullRowId, IReadOnlySet<string>? nullProperties = null)
    {
        var properties = PropertiesOf(row);
        nullProperties ??= new HashSet<string>(StringComparer.Ordinal);
        nullProperties.ShouldNotContain("Id");
        nullProperties.ShouldBeSubsetOf(properties.Select(static property => property.Name));

        foreach (var property in properties)
        {
            var expected = property.Name switch
            {
                "Id" => id,
                _ when nullProperties.Contains(property.Name) => null,
                _ => FullRow[property.Name](property.PropertyType),
            };
            ShouldBeValue(property.GetValue(row), expected, property.Name);
        }
    }

    // Asserts that the row is row 2 of the seed, with null in every column but id.
    public static void ShouldBeTheNullRow(object row)
    {
        foreach (var property in PropertiesOf(row))
        {
            ShouldBeValue(property.GetValue(row), property.Name == "Id" ? NullRowId : null, property.Name);
        }
    }

    // The public properties of the row, which are those that the expectations name, no more and no fewer.
    private static PropertyInfo[] PropertiesOf(object row)
    {
        var properties = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        properties
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)
            .ShouldBe(FullRow.Keys.Order(StringComparer.Ordinal), $"The properties of {row.GetType()}");
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
        if (IsComparedByText(actual.GetType()))
        {
            TextOf(actual).ShouldBe(expected.ShouldBeOfType<string>(path), path);
            return;
        }

        switch (actual)
        {
            case BitArray bits:
                var expectedBits = expected.ShouldBeOfType<BitArray>(path);
                bits.Length.ShouldBe(expectedBits.Length, path);
                for (var i = 0; i < bits.Length; i++)
                {
                    bits[i].ShouldBe(expectedBits[i], $"{path}[{i}]");
                }

                break;
            case IDictionary dictionary:
                var expectedDictionary = expected.ShouldBeAssignableTo<IDictionary>(path).ShouldNotBeNull(path);
                dictionary.Count.ShouldBe(expectedDictionary.Count, path);
                foreach (DictionaryEntry entry in expectedDictionary)
                {
                    dictionary.Contains(entry.Key).ShouldBeTrue($"{path}[{entry.Key}]");
                    ShouldBeValue(dictionary[entry.Key], entry.Value, $"{path}[{entry.Key}]");
                }

                break;
            case Array array:
                var expectedArray = expected.ShouldBeAssignableTo<Array>(path).ShouldNotBeNull(path);
                array.Length.ShouldBe(expectedArray.Length, path);
                for (var i = 0; i < array.Length; i++)
                {
                    ShouldBeValue(array.GetValue(i), expectedArray.GetValue(i), $"{path}[{i}]");
                }

                break;
            case DateTime dateTime:
                var expectedDateTime = expected.ShouldBeOfType<DateTime>(path);
                dateTime.ShouldBe(expectedDateTime, path);
                dateTime.Kind.ShouldBe(expectedDateTime.Kind, path);
                break;
            case DateTimeOffset dateTimeOffset:
                var expectedDateTimeOffset = expected.ShouldBeOfType<DateTimeOffset>(path);
                dateTimeOffset.ShouldBe(expectedDateTimeOffset, path);
                dateTimeOffset.Offset.ShouldBe(expectedDateTimeOffset.Offset, path);
                break;
            default:
                actual.GetType().ShouldBe(expected.GetType(), path);
                actual.ShouldBe(expected, path);
                break;
        }
    }

    // An array of them is compared element by element: its type has the namespace of its elements.
    private static bool IsComparedByText(Type type) =>
        !type.IsArray && (type.Namespace == NpgsqlTypes || type == typeof(IPNetwork));

    // The text of a value of NpgsqlTypes, which formats its parts, such as the bounds of a range, in the current
    // culture: the culture is the invariant one for the call, and is given back after it.
    private static string TextOf(object value)
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            return Convert.ToString(value, CultureInfo.InvariantCulture).ShouldNotBeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    // The expectation of a column whose C# type differs by driver, by the type that decides it.
    private static object ByDriver(Type type, params (Type Type, object Value)[] cases) =>
        cases.SingleOrDefault(@case => @case.Type == type).Value
        ?? throw new ShouldAssertException($"No expectation is held for {type}.");

    private static Type ScalarOf(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    // The type of the bounds of a range, or of the ranges of a multirange.
    private static Type SubtypeOf(Type type) =>
        ScalarOf(type.IsArray ? type.GetElementType()! : type).GetGenericArguments().ShouldHaveSingleItem();

    // A bit string as Npgsql reads it, its first bit first.
    private static BitArray Bits(string bits) => new(bits.Select(static bit => bit == '1').ToArray());
}
