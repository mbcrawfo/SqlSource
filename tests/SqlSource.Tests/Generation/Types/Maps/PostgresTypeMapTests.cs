using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Generation.Types.Maps;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Types.Maps;

public sealed class PostgresTypeMapTests
{
    private const string DateTime = "global::System.DateTime";
    private const string DateOnly = "global::System.DateOnly";
    private const string BitArray = "global::System.Collections.BitArray";
    private const string Range = "global::NpgsqlTypes.NpgsqlRange";
    private const string Noda = "global::NodaTime.";
    private const string NodaRange = "global::NpgsqlTypes.NpgsqlRange<global::NodaTime.";

    // Each type built for a case of the rule of its kind, by the name of the case.
    private static readonly Dictionary<string, KindCase> Kinds = new()
    {
        ["array of int4"] = new(ArrayOf(TestSidecars.Postgres()), "int[]", false, "int4"),
        ["array of bit(1)"] = new(ArrayOf(Bit("bit", 1)), "bool[]", false, "bit"),
        ["array of an enum"] = new(ArrayOf(UserStatus()), "string[]", false, "public.user_status"),
        ["array of a domain over text"] = new(ArrayOf(DomainOf("label", Text())), "string[]", false, "text"),
        ["domain over a domain over int4"] = new(
            DomainOf("user_id", DomainOf("positive", TestSidecars.Postgres())),
            "int",
            true,
            "int4"
        ),
        ["enum"] = new(UserStatus(), "string", false, "public.user_status"),
        ["int4range"] = new(
            ModelRuns.Catalog("int4range", SidecarValues.Kind.Range, subtype: TestSidecars.Postgres()),
            $"{Range}<int>",
            true,
            "int4range"
        ),
        ["tstzrange"] = new(
            ModelRuns.Catalog("tstzrange", SidecarValues.Kind.Range, subtype: ModelRuns.Catalog("timestamptz")),
            $"{Range}<{DateTime}>",
            true,
            "tstzrange"
        ),
        ["int4multirange"] = new(
            ModelRuns.Catalog("int4multirange", SidecarValues.Kind.Multirange, subtype: ModelRuns.Catalog("int4range")),
            $"{Range}<int>[]",
            false,
            "int4multirange"
        ),
        ["range of a subtype the map does not list"] = new(
            User("floatrange", SidecarValues.Kind.Range, subtype: ModelRuns.Catalog("float8")),
            "!UserDefinedRange",
            false,
            "public.floatrange"
        ),
        ["multirange of such a range"] = new(
            User(
                "floatmultirange",
                SidecarValues.Kind.Multirange,
                subtype: User("floatrange", SidecarValues.Kind.Range)
            ),
            "!UserDefinedRange",
            false,
            "public.floatmultirange"
        ),
        ["composite"] = new(User("users", SidecarValues.Kind.Composite), "!Unreadable", false, "public.users"),
        ["kind this version does not know"] = new(
            User("users", "table"),
            "!NotWrittenByTheTool",
            false,
            "public.users"
        ),
        ["base type of public that is no extension"] = new(
            User("geometry", SidecarValues.Kind.Base),
            "!Unreadable",
            false,
            "public.geometry"
        ),
        ["base type of pg_catalog the map lacks"] = new(ModelRuns.Catalog("aclitem"), "!CastToText", false, "aclitem"),
        ["composite of pg_catalog"] = new(
            ModelRuns.Catalog("pg_class", SidecarValues.Kind.Composite),
            "!Unreadable",
            false,
            "pg_class"
        ),
        ["composite named as an extension's type"] = new(
            User("cube", SidecarValues.Kind.Composite),
            "!Unreadable",
            false,
            "public.cube"
        ),
        ["domain named as an extension's type"] = new(DomainOf("hstore", Text()), "string", false, "text"),
    };

    // One row for each key of the base map, bit and varbit aside: its type under Npgsql 8 and 9, under 10, and
    // with no Npgsql, or the kind of the failure after a "!", then whether the type is a value type.
    public static TheoryData<string, string, string, string, bool> Rows =>
        new()
        {
            { "bool", "bool", "bool", "bool", true },
            { "int2", "short", "short", "short", true },
            { "int4", "int", "int", "int", true },
            { "int8", "long", "long", "long", true },
            { "float4", "float", "float", "float", true },
            { "float8", "double", "double", "double", true },
            { "numeric", "decimal", "decimal", "decimal", true },
            { "money", "decimal", "decimal", "decimal", true },
            { "text", "string", "string", "string", false },
            { "varchar", "string", "string", "string", false },
            { "bpchar", "string", "string", "string", false },
            { "name", "string", "string", "string", false },
            { "refcursor", "string", "string", "string", false },
            { "char", "char", "char", "char", true },
            { "citext", "string", "string", "string", false },
            { "bytea", "byte[]", "byte[]", "byte[]", false },
            { "date", DateTime, DateOnly, "!DiffersByDriver", true },
            { "time", "global::System.TimeSpan", "global::System.TimeOnly", "!DiffersByDriver", true },
            {
                "timetz",
                "global::System.DateTimeOffset",
                "global::System.DateTimeOffset",
                "global::System.DateTimeOffset",
                true
            },
            { "timestamp", DateTime, DateTime, DateTime, true },
            { "timestamptz", DateTime, DateTime, DateTime, true },
            { "interval", "global::System.TimeSpan", "global::System.TimeSpan", "global::System.TimeSpan", true },
            { "uuid", "global::System.Guid", "global::System.Guid", "global::System.Guid", true },
            { "json", "string", "string", "string", false },
            { "jsonb", "string", "string", "string", false },
            { "jsonpath", "string", "string", "string", false },
            { "xml", "string", "string", "string", false },
            {
                "inet",
                "global::System.Net.IPAddress",
                "global::System.Net.IPAddress",
                "global::System.Net.IPAddress",
                false
            },
            { "cidr", "global::NpgsqlTypes.NpgsqlCidr", "global::System.Net.IPNetwork", "!DiffersByDriver", true },
            {
                "macaddr",
                "global::System.Net.NetworkInformation.PhysicalAddress",
                "global::System.Net.NetworkInformation.PhysicalAddress",
                "global::System.Net.NetworkInformation.PhysicalAddress",
                false
            },
            {
                "macaddr8",
                "global::System.Net.NetworkInformation.PhysicalAddress",
                "global::System.Net.NetworkInformation.PhysicalAddress",
                "global::System.Net.NetworkInformation.PhysicalAddress",
                false
            },
            { "point", "global::NpgsqlTypes.NpgsqlPoint", "global::NpgsqlTypes.NpgsqlPoint", "!NeedsDriver", true },
            { "line", "global::NpgsqlTypes.NpgsqlLine", "global::NpgsqlTypes.NpgsqlLine", "!NeedsDriver", true },
            { "lseg", "global::NpgsqlTypes.NpgsqlLSeg", "global::NpgsqlTypes.NpgsqlLSeg", "!NeedsDriver", true },
            { "box", "global::NpgsqlTypes.NpgsqlBox", "global::NpgsqlTypes.NpgsqlBox", "!NeedsDriver", true },
            { "path", "global::NpgsqlTypes.NpgsqlPath", "global::NpgsqlTypes.NpgsqlPath", "!NeedsDriver", true },
            {
                "polygon",
                "global::NpgsqlTypes.NpgsqlPolygon",
                "global::NpgsqlTypes.NpgsqlPolygon",
                "!NeedsDriver",
                true
            },
            { "circle", "global::NpgsqlTypes.NpgsqlCircle", "global::NpgsqlTypes.NpgsqlCircle", "!NeedsDriver", true },
            {
                "tsvector",
                "global::NpgsqlTypes.NpgsqlTsVector",
                "global::NpgsqlTypes.NpgsqlTsVector",
                "!NeedsDriver",
                false
            },
            {
                "tsquery",
                "global::NpgsqlTypes.NpgsqlTsQuery",
                "global::NpgsqlTypes.NpgsqlTsQuery",
                "!NeedsDriver",
                false
            },
            { "oid", "uint", "uint", "uint", true },
            { "xid", "uint", "uint", "uint", true },
            { "cid", "uint", "uint", "uint", true },
            { "regtype", "uint", "uint", "uint", true },
            { "regconfig", "uint", "uint", "uint", true },
            { "xid8", "ulong", "ulong", "ulong", true },
            { "tid", "global::NpgsqlTypes.NpgsqlTid", "global::NpgsqlTypes.NpgsqlTid", "!NeedsDriver", true },
            {
                "pg_lsn",
                "global::NpgsqlTypes.NpgsqlLogSequenceNumber",
                "global::NpgsqlTypes.NpgsqlLogSequenceNumber",
                "!NeedsDriver",
                true
            },
            { "int2vector", "short[]", "short[]", "short[]", false },
            { "oidvector", "uint[]", "uint[]", "uint[]", false },
            {
                "hstore",
                "global::System.Collections.Generic.Dictionary<string, string?>",
                "global::System.Collections.Generic.Dictionary<string, string?>",
                "global::System.Collections.Generic.Dictionary<string, string?>",
                false
            },
            { "ltree", "string", "string", "string", false },
            { "lquery", "string", "string", "string", false },
            { "ltxtquery", "string", "string", "string", false },
            { "cube", "!NeedsNpgsql10", "global::NpgsqlTypes.NpgsqlCube", "!NeedsDriver", true },
            { "int4range", $"{Range}<int>", $"{Range}<int>", "!NeedsDriver", true },
            { "int8range", $"{Range}<long>", $"{Range}<long>", "!NeedsDriver", true },
            { "numrange", $"{Range}<decimal>", $"{Range}<decimal>", "!NeedsDriver", true },
            { "tsrange", $"{Range}<{DateTime}>", $"{Range}<{DateTime}>", "!NeedsDriver", true },
            { "tstzrange", $"{Range}<{DateTime}>", $"{Range}<{DateTime}>", "!NeedsDriver", true },
            { "daterange", $"{Range}<{DateTime}>", $"{Range}<{DateOnly}>", "!DiffersByDriver", true },
            { "int4multirange", $"{Range}<int>[]", $"{Range}<int>[]", "!NeedsDriver", false },
            { "int8multirange", $"{Range}<long>[]", $"{Range}<long>[]", "!NeedsDriver", false },
            { "nummultirange", $"{Range}<decimal>[]", $"{Range}<decimal>[]", "!NeedsDriver", false },
            { "tsmultirange", $"{Range}<{DateTime}>[]", $"{Range}<{DateTime}>[]", "!NeedsDriver", false },
            { "tstzmultirange", $"{Range}<{DateTime}>[]", $"{Range}<{DateTime}>[]", "!NeedsDriver", false },
            { "datemultirange", $"{Range}<{DateTime}>[]", $"{Range}<{DateOnly}>[]", "!DiffersByDriver", false },
            { "regclass", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regproc", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regprocedure", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regoper", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regoperator", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regrole", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regnamespace", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regcollation", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "regdictionary", "!CastToOid", "!CastToOid", "!CastToOid", false },
            { "pg_snapshot", "!CastToText", "!CastToText", "!CastToText", false },
            { "txid_snapshot", "!CastToText", "!CastToText", "!CastToText", false },
            { "pg_node_tree", "!CastToText", "!CastToText", "!CastToText", false },
            { "record", "!NotWrittenByTheTool", "!NotWrittenByTheTool", "!NotWrittenByTheTool", false },
            { "unknown", "!NotWrittenByTheTool", "!NotWrittenByTheTool", "!NotWrittenByTheTool", false },
            { "void", "!NotWrittenByTheTool", "!NotWrittenByTheTool", "!NotWrittenByTheTool", false },
        };

    public static TheoryData<string> KindCases
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in Kinds.Keys)
            {
                data.Add(name);
            }

            return data;
        }
    }

    // A range or a multirange is built of its kind; an extension's type is in public, where it is usually created.
    [Theory]
    [MemberData(nameof(Rows))]
    public void Map_EveryRowOfTheTable_GivesItsType(
        string internalName,
        string under8,
        string under10,
        string underNone,
        bool isValueType
    )
    {
        var kind = SidecarValues.Kind.Base;
        if (internalName.EndsWith("multirange", StringComparison.Ordinal))
        {
            kind = SidecarValues.Kind.Multirange;
        }
        else if (internalName.EndsWith("range", StringComparison.Ordinal))
        {
            kind = SidecarValues.Kind.Range;
        }

        var schema = TypeKeyNames.IsExtension(internalName) ? "public" : "pg_catalog";
        var type = TestSidecars.Postgres(name: internalName, kind: kind, schema: schema, internalName: internalName);

        ShouldMapTo(PostgresTypeMap.Map(type, NpgsqlColumn.Npgsql8), under8, isValueType, internalName);
        ShouldMapTo(PostgresTypeMap.Map(type, NpgsqlColumn.Npgsql10), under10, isValueType, internalName);
        ShouldMapTo(PostgresTypeMap.Map(type, NpgsqlColumn.None), underNone, isValueType, internalName);
    }

    // The names a key may be written with are the map's own keys, so the map and the settings never drift apart.
    [Fact]
    public void Keys_OfTheMap_AreTheKnownPostgresNamesOfTheSettings() =>
        PostgresTypeMap.Keys.ShouldBe(TypeKeyNames.KnownPostgresNames, ignoreOrder: true);

    [Theory]
    [InlineData("citext", "public", "string")]
    [InlineData("citext", "ext", "string")]
    [InlineData("hstore", "public", "global::System.Collections.Generic.Dictionary<string, string?>")]
    [InlineData("hstore", "ext", "global::System.Collections.Generic.Dictionary<string, string?>")]
    [InlineData("ltree", "ext", "string")]
    public void Map_ExtensionType_IsMatchedInAnySchema(string internalName, string schema, string expected)
    {
        var type = TestSidecars.Postgres(name: internalName, schema: schema, internalName: internalName);

        foreach (var column in Enum.GetValues<NpgsqlColumn>())
        {
            ShouldMapTo(PostgresTypeMap.Map(type, column), expected, false, internalName);
        }
    }

    [Theory]
    [InlineData("bit", 1, "bool", true)]
    [InlineData("bit", 8, BitArray, false)]
    [InlineData("bit", null, BitArray, false)]
    [InlineData("varbit", 1, "bool", true)]
    [InlineData("varbit", 64, BitArray, false)]
    [InlineData("varbit", null, BitArray, false)]
    public void Map_Bit_IsDecidedByItsLength(string internalName, int? length, string expected, bool isValueType)
    {
        foreach (var column in Enum.GetValues<NpgsqlColumn>())
        {
            ShouldMapTo(PostgresTypeMap.Map(Bit(internalName, length), column), expected, isValueType, internalName);
        }
    }

    [Theory]
    [MemberData(nameof(KindCases))]
    public void Map_Kinds_FollowTheRuleOfTheKind(string name)
    {
        var (type, expected, isValueType, key) = Kinds[name];

        ShouldMapTo(PostgresTypeMap.Map(type, NpgsqlColumn.Npgsql10), expected, isValueType, key);
    }

    [Fact]
    public void Map_ArrayOrDomainOfATypeThatDiffers_DiffersWithoutTheDriver()
    {
        ShouldMapTo(
            PostgresTypeMap.Map(ArrayOf(ModelRuns.Catalog("date")), NpgsqlColumn.None),
            "!DiffersByDriver",
            false,
            "date"
        );
        ShouldMapTo(
            PostgresTypeMap.Map(DomainOf("birthday", ModelRuns.Catalog("date")), NpgsqlColumn.None),
            "!DiffersByDriver",
            false,
            "date"
        );
    }

    // The detail is the type override's entry for each driver, as a user writes it, with the key the failure has.  A
    // type of NpgsqlTypes cannot be named without Npgsql, so it is left out, and a type with only such types has none.
    [Theory]
    [InlineData("date", "date=System.DateTime on Npgsql 8 and 9, date=System.DateOnly on Npgsql 10")]
    [InlineData("cidr", "cidr=System.Net.IPNetwork on Npgsql 10")]
    [InlineData("daterange", null)]
    [InlineData("datemultirange", null)]
    public void Map_TypeThatDiffersWithoutTheDriver_NamesTheEntryForEachDriver(string internalName, string? detail) =>
        PostgresTypeMap.Map(ModelRuns.Catalog(internalName), NpgsqlColumn.None).Detail.ShouldBe(detail);

    // The cast reads an array of the type as an array of oid.
    [Fact]
    public void Map_TypeReadByACastToOid_NamesTheCastOfItsRank()
    {
        PostgresTypeMap.Map(ModelRuns.Catalog("regclass"), NpgsqlColumn.Npgsql10).Detail.ShouldBe("oid");
        PostgresTypeMap.Map(ArrayOf(ModelRuns.Catalog("regclass")), NpgsqlColumn.Npgsql10).Detail.ShouldBe("oid[]");
        PostgresTypeMap
            .Map(ArrayOf(DomainOf("relation", ModelRuns.Catalog("regclass"))), NpgsqlColumn.Npgsql10)
            .Detail.ShouldBe("oid[]");
    }

    // No Npgsql reads these, or Npgsql 8 and 9 cube, and a cast to text reads each, an array by a cast to text[].
    [Theory]
    [InlineData("pg_snapshot", "pg_catalog")]
    [InlineData("pg_node_tree", "pg_catalog")]
    [InlineData("aclitem", "pg_catalog")]
    [InlineData("cube", "public")]
    public void Map_TypeReadByACastToText_NamesTheCastOfItsRank(string internalName, string schema)
    {
        var type = TestSidecars.Postgres(name: internalName, schema: schema, internalName: internalName);

        PostgresTypeMap.Map(type, NpgsqlColumn.Npgsql8).Detail.ShouldBe("text");
        PostgresTypeMap.Map(ArrayOf(type), NpgsqlColumn.Npgsql8).Detail.ShouldBe("text[]");
    }

    [Fact]
    public void Map_ArrayOfATypeOfNpgsqlTypes_NeedsTheDriverWithoutIt() =>
        ShouldMapTo(
            PostgresTypeMap.Map(ArrayOf(ModelRuns.Catalog("point")), NpgsqlColumn.None),
            "!NeedsDriver",
            false,
            "point"
        );

    [Fact]
    public void Map_Override_IsAskedAtEveryLevelFirst()
    {
        var array = ArrayOf(TestSidecars.Postgres());
        var ofElement = Overrides(("int4", "global::MyApp.Id", true));
        var ofArrayAndElement = Overrides(
            ("_int4", "global::System.Collections.Generic.List<int>", false),
            ("int4", "global::MyApp.Id", true)
        );

        ShouldMapTo(PostgresTypeMap.Map(array, NpgsqlColumn.Npgsql10, ofElement), "global::MyApp.Id[]", false, "int4");
        ShouldMapTo(
            PostgresTypeMap.Map(array, NpgsqlColumn.Npgsql10, ofArrayAndElement),
            "global::System.Collections.Generic.List<int>",
            false,
            "_int4"
        );
        ShouldMapTo(
            PostgresTypeMap.Map(DomainOf("user_id", TestSidecars.Postgres()), NpgsqlColumn.Npgsql10, ofElement),
            "global::MyApp.Id",
            true,
            "int4"
        );
        ShouldMapTo(
            PostgresTypeMap.Map(ModelRuns.Catalog("date"), NpgsqlColumn.None, Overrides(("date", DateOnly, true))),
            DateOnly,
            true,
            "date"
        );
    }

    [Theory]
    [InlineData("pg_catalog", "int4", "int4")]
    [InlineData("public", "user_status", "public.user_status")]
    [InlineData("ext", "citext", "ext.citext")]
    [InlineData("public", "Status", "public.\"Status\"")]
    [InlineData("my schema", "order-status", "\"my schema\".\"order-status\"")]
    [InlineData("public", "say \"hi\"", "public.\"say \"\"hi\"\"\"")]
    [InlineData("pg_catalog", "Odd", "\"Odd\"")]
    public void KeyOf_Type_IsWrittenAsTheTypeOverrideReadsIt(string schema, string internalName, string key) =>
        PostgresTypeMap.KeyOf(TestSidecars.Postgres(schema: schema, internalName: internalName)).ShouldBe(key);

    // A row under either driver's column, the rule of bits and of an enum, exactly, and an array of any of them.
    [Theory]
    [InlineData("long")]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData(BitArray)]
    [InlineData(DateOnly)]
    [InlineData(DateTime)]
    [InlineData("int[]")]
    [InlineData("int[][]")]
    [InlineData("short")]
    [InlineData("short[]")]
    [InlineData("byte[]")]
    [InlineData("byte[][]")]
    [InlineData("global::NpgsqlTypes.NpgsqlRange<int>")]
    [InlineData("global::NpgsqlTypes.NpgsqlRange<int>[]")]
    [InlineData("global::NpgsqlTypes.NpgsqlCube")]
    public void Gives_TypesOfTheMap_AreGiven(string text) => PostgresTypeMap.Gives(text).ShouldBeTrue();

    [Theory]
    [InlineData("global::MyApp.Email")]
    [InlineData("global::MyApp.Email[]")]
    [InlineData("global::System.Uri")]
    [InlineData("int?")]
    [InlineData("byte")]
    public void Gives_ForeignTypes_AreNot(string text) => PostgresTypeMap.Gives(text).ShouldBeFalse();

    // NodaTime's rows: the type each gives with the library on, and whether it is a value type.  A multirange is an
    // array of its range's type, and Period is a class.
    public static TheoryData<string, string, bool> NodaTimeRows =>
        new()
        {
            { "timestamptz", Noda + "Instant", true },
            { "timestamp", Noda + "LocalDateTime", true },
            { "date", Noda + "LocalDate", true },
            { "time", Noda + "LocalTime", true },
            { "timetz", Noda + "OffsetTime", true },
            { "interval", Noda + "Period", false },
            { "tstzrange", NodaRange + "Instant>", true },
            { "tsrange", NodaRange + "LocalDateTime>", true },
            { "daterange", NodaRange + "LocalDate>", true },
            { "tstzmultirange", NodaRange + "Instant>[]", false },
            { "tsmultirange", NodaRange + "LocalDateTime>[]", false },
            { "datemultirange", NodaRange + "LocalDate>[]", false },
        };

    private static LibraryRows NodaTimeOn => LibraryRows.Of([SqlLibrary.NodaTime])!;

    [Theory]
    [MemberData(nameof(NodaTimeRows))]
    public void Map_NodaTimeOn_GivesEachRow(string internalName, string expected, bool isValueType)
    {
        foreach (var column in Enum.GetValues<NpgsqlColumn>())
        {
            var mapping = PostgresTypeMap.Map(ModelRuns.Catalog(internalName), column, libraries: NodaTimeOn);

            ShouldMapTo(mapping, expected, isValueType, internalName);
            mapping.Library.ShouldBe(SqlLibrary.NodaTime);
        }
    }

    [Fact]
    public void Map_NodaTimeOn_ArrayIsTheElementsArray()
    {
        var mapping = PostgresTypeMap.Map(
            ArrayOf(ModelRuns.Catalog("timestamptz")),
            NpgsqlColumn.Npgsql10,
            libraries: NodaTimeOn
        );

        ShouldMapTo(mapping, Noda + "Instant[]", false, "timestamptz");
        mapping.Library.ShouldBe(SqlLibrary.NodaTime);
    }

    [Fact]
    public void Map_NodaTimeOn_DomainIsItsBases()
    {
        var mapping = PostgresTypeMap.Map(
            DomainOf("mydomain", ModelRuns.Catalog("timestamptz")),
            NpgsqlColumn.Npgsql10,
            libraries: NodaTimeOn
        );

        ShouldMapTo(mapping, Noda + "Instant", true, "timestamptz");
        mapping.Library.ShouldBe(SqlLibrary.NodaTime);
    }

    [Fact]
    public void Map_NodaTimeOn_AnArrayOfADomainIsTheBasesArray()
    {
        var mapping = PostgresTypeMap.Map(
            ArrayOf(DomainOf("mydomain", ModelRuns.Catalog("date"))),
            NpgsqlColumn.None,
            libraries: NodaTimeOn
        );

        ShouldMapTo(mapping, Noda + "LocalDate[]", false, "date");
        mapping.Library.ShouldBe(SqlLibrary.NodaTime);
    }

    [Fact]
    public void Map_Override_WinsAtEachLevel()
    {
        var timestamptz = ModelRuns.Catalog("timestamptz");
        var ofElement = Overrides(("timestamptz", "global::System.DateTimeOffset", true));
        var ofArray = Overrides(("_timestamptz", "global::MyApp.Stamps", false));
        var ofDomain = Overrides(("public.mydomain", "global::MyApp.Y", true));

        var element = PostgresTypeMap.Map(timestamptz, NpgsqlColumn.Npgsql10, ofElement, NodaTimeOn);
        var array = PostgresTypeMap.Map(ArrayOf(timestamptz), NpgsqlColumn.Npgsql10, ofElement, NodaTimeOn);
        var whole = PostgresTypeMap.Map(ArrayOf(timestamptz), NpgsqlColumn.Npgsql10, ofArray, NodaTimeOn);
        var domain = PostgresTypeMap.Map(
            DomainOf("mydomain", timestamptz),
            NpgsqlColumn.Npgsql10,
            ofDomain,
            NodaTimeOn
        );

        ShouldMapTo(element, "global::System.DateTimeOffset", true, "timestamptz");
        ShouldMapTo(array, "global::System.DateTimeOffset[]", false, "timestamptz");
        ShouldMapTo(whole, "global::MyApp.Stamps", false, "_timestamptz");
        ShouldMapTo(domain, "global::MyApp.Y", true, "public.mydomain");
        new[] { element, array, whole, domain }.ShouldAllBe(mapping => mapping.Library == null);
    }

    [Theory]
    [MemberData(nameof(NodaTimeRows))]
    public void Map_NodaTimeOff_IsTheBaseMap(string internalName, string notExpected, bool isValueType)
    {
        _ = isValueType;
        var type = ModelRuns.Catalog(internalName);

        foreach (var column in Enum.GetValues<NpgsqlColumn>())
        {
            var mapping = PostgresTypeMap.Map(type, column, libraries: null);

            mapping.ShouldBe(PostgresTypeMap.Map(type, column));
            mapping.Library.ShouldBeNull();
            mapping.Text.ShouldNotBe(notExpected);
        }
    }

    [Fact]
    public void Map_NodaTimeOn_OtherTypesUnchanged()
    {
        PostgresType[] types =
        [
            TestSidecars.Postgres(),
            Text(),
            ModelRuns.Catalog("int4range", SidecarValues.Kind.Range, subtype: TestSidecars.Postgres()),
            ArrayOf(TestSidecars.Postgres()),
            DomainOf("user_id", TestSidecars.Postgres()),
            UserStatus(),
            TestSidecars.Postgres(
                name: "timestamptz",
                schema: "ext",
                internalName: "timestamptz",
                kind: SidecarValues.Kind.Domain,
                baseType: Text()
            ),
            User("timestamptz", SidecarValues.Kind.Base),
        ];

        foreach (var type in types)
        {
            var on = PostgresTypeMap.Map(type, NpgsqlColumn.Npgsql10, libraries: NodaTimeOn);

            on.ShouldBe(PostgresTypeMap.Map(type, NpgsqlColumn.Npgsql10));
            on.Library.ShouldBeNull();
        }
    }

    [Fact]
    public void Map_NodaTimeOn_MatchesTheNameIgnoringCase() =>
        ShouldMapTo(
            PostgresTypeMap.Map(ModelRuns.Catalog("TimestampTZ"), NpgsqlColumn.Npgsql10, libraries: NodaTimeOn),
            Noda + "Instant",
            true,
            "timestamptz"
        );

    [Fact]
    public void Of_NoLibraries_IsNull() => LibraryRows.Of([]).ShouldBeNull();

    [Fact]
    public void Of_ALibrary_IsTheSameInstanceEachTime() =>
        LibraryRows.Of([SqlLibrary.NodaTime]).ShouldBeSameAs(LibraryRows.Of([SqlLibrary.NodaTime]));

    [Theory]
    [InlineData(Noda + "Instant")]
    [InlineData(Noda + "Instant[]")]
    [InlineData(NodaRange + "Instant>")]
    public void Gives_LibraryTypeIsForeign(string text) => PostgresTypeMap.Gives(text).ShouldBeFalse();

    // A cell is the C# type the mapping gives, or "!" and the kind of its failure.
    private static void ShouldMapTo(TypeMapping mapping, string expected, bool isValueType, string key)
    {
        mapping.Key.ShouldBe(key);
        if (expected.StartsWith('!'))
        {
            mapping.Kind.ShouldBe(Enum.Parse<TypeMappingKind>(expected[1..]));
            mapping.Text.ShouldBeNull();
            return;
        }

        mapping.Kind.ShouldBe(TypeMappingKind.Mapped);
        mapping.Text.ShouldBe(expected);
        mapping.IsValueType.ShouldBe(isValueType);
        mapping.IsPublic.ShouldBeTrue();
        mapping.WarningIds.ShouldBeEmpty();
    }

    private static PostgresOverrideOf Overrides(params (string Key, string Text, bool IsValueType)[] entries) =>
        type =>
        {
            var key = PostgresTypeMap.KeyOf(type);
            var entry = entries.FirstOrDefault(entry => entry.Key == key);
            return entry.Key is null ? null : TypeMapping.Mapped(entry.Key, entry.Text, entry.IsValueType);
        };

    private static PostgresType User(string internalName, string kind, PostgresType? subtype = null) =>
        TestSidecars.Postgres(
            name: internalName,
            kind: kind,
            schema: "public",
            internalName: internalName,
            subtype: subtype
        );

    private static PostgresType Text() => ModelRuns.Catalog("text");

    private static PostgresType Bit(string internalName, int? length) =>
        TestSidecars.Postgres(name: internalName, internalName: internalName, length: length);

    private static PostgresType UserStatus() =>
        TestSidecars.Postgres(
            name: "user_status",
            kind: SidecarValues.Kind.Enum,
            schema: "public",
            internalName: "user_status",
            labels: ["active", "disabled"]
        );

    private static PostgresType ArrayOf(PostgresType element) =>
        TestSidecars.Postgres(
            name: element.Name + "[]",
            kind: SidecarValues.Kind.Array,
            schema: element.Schema,
            internalName: "_" + element.InternalName,
            element: element
        );

    private static PostgresType DomainOf(string internalName, PostgresType baseType) =>
        TestSidecars.Postgres(
            name: internalName,
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: internalName,
            baseType: baseType
        );

    private sealed record KindCase(PostgresType Type, string Expected, bool IsValueType, string Key);
}
