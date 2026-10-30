using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Generation.Types.Maps;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Types.Maps;

public sealed class SqlServerTypeMapTests
{
    // The names a key may be written with are the map's own keys, so the map and the settings never drift apart.
    [Fact]
    public void Keys_OfTheMap_AreTheKnownSqlServerNamesOfTheSettings() =>
        SqlServerTypeMap.Keys.ShouldBe(TypeKeyNames.KnownSqlServerNames, ignoreOrder: true);

    private const string DateTime = "global::System.DateTime";
    private const string Vector = "global::Microsoft.Data.SqlTypes.SqlVector<float>";
    private const string NeedsDriver = "!NeedsDriver";
    private const string NeedsNewer = "!NeedsNewerSqlClient";
    private const string NeedsTypes = "!NeedsSqlServerTypes";
    private const string Assembly = "Microsoft.SqlServer.Types, Version=11.0.0.0";

    // A failing cell is '!' and the kind's name.
    public static TheoryData<string, string, string, string, string, bool> Rows =>
        new()
        {
            { "bigint", "long", "long", "long", "long", true },
            { "int", "int", "int", "int", "int", true },
            { "smallint", "short", "short", "short", "short", true },
            { "tinyint", "byte", "byte", "byte", "byte", true },
            { "bit", "bool", "bool", "bool", "bool", true },
            { "decimal", "decimal", "decimal", "decimal", "decimal", true },
            { "numeric", "decimal", "decimal", "decimal", "decimal", true },
            { "money", "decimal", "decimal", "decimal", "decimal", true },
            { "smallmoney", "decimal", "decimal", "decimal", "decimal", true },
            { "float", "double", "double", "double", "double", true },
            { "real", "float", "float", "float", "float", true },
            { "date", DateTime, DateTime, DateTime, DateTime, true },
            { "datetime", DateTime, DateTime, DateTime, DateTime, true },
            { "datetime2", DateTime, DateTime, DateTime, DateTime, true },
            { "smalldatetime", DateTime, DateTime, DateTime, DateTime, true },
            {
                "time",
                "global::System.TimeSpan",
                "global::System.TimeSpan",
                "global::System.TimeSpan",
                "global::System.TimeSpan",
                true
            },
            {
                "datetimeoffset",
                "global::System.DateTimeOffset",
                "global::System.DateTimeOffset",
                "global::System.DateTimeOffset",
                "global::System.DateTimeOffset",
                true
            },
            { "char", "string", "string", "string", "string", false },
            { "varchar", "string", "string", "string", "string", false },
            { "text", "string", "string", "string", "string", false },
            { "nchar", "string", "string", "string", "string", false },
            { "nvarchar", "string", "string", "string", "string", false },
            { "ntext", "string", "string", "string", "string", false },
            { "xml", "string", "string", "string", "string", false },
            { "json", "string", "string", "string", "string", false },
            { "sysname", "string", "string", "string", "string", false },
            { "binary", "byte[]", "byte[]", "byte[]", "byte[]", false },
            { "varbinary", "byte[]", "byte[]", "byte[]", "byte[]", false },
            { "image", "byte[]", "byte[]", "byte[]", "byte[]", false },
            { "timestamp", "byte[]", "byte[]", "byte[]", "byte[]", false },
            {
                "uniqueidentifier",
                "global::System.Guid",
                "global::System.Guid",
                "global::System.Guid",
                "global::System.Guid",
                true
            },
            { "sql_variant", "object", "object", "object", "object", false },
            { "vector", Vector, Vector, NeedsDriver, NeedsNewer, true },
            { "hierarchyid", NeedsTypes, NeedsTypes, NeedsTypes, NeedsTypes, false },
            { "geography", NeedsTypes, NeedsTypes, NeedsTypes, NeedsTypes, false },
            { "geometry", NeedsTypes, NeedsTypes, NeedsTypes, NeedsTypes, false },
        };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Map_EveryRowOfTheTable_GivesItsType(
        string name,
        string under61,
        string under7,
        string underNone,
        string underOlder,
        bool isValueType
    )
    {
        var type = IsClr(name)
            ? TestSidecars.SqlServer(name: name, userType: new SqlServerUserType("sys", name, Assembly))
            : TestSidecars.SqlServer(name: name);

        ShouldMapTo(SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient61), under61, isValueType, name);
        ShouldMapTo(SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7), under7, isValueType, name);
        ShouldMapTo(SqlServerTypeMap.Map(type, SqlClientColumn.None), underNone, isValueType, name);
        ShouldMapTo(SqlServerTypeMap.Map(type, SqlClientColumn.OlderThan61), underOlder, isValueType, name);
    }

    [Theory]
    [InlineData("long")]
    [InlineData("byte")]
    [InlineData("string")]
    [InlineData("byte[]")]
    [InlineData("int[]")]
    [InlineData("object")]
    [InlineData(DateTime)]
    [InlineData(Vector)]
    public void Gives_TypesOfTheMap_AreGiven(string text) => SqlServerTypeMap.Gives(text).ShouldBeTrue();

    // No row gives them, but SqlClient reads date and time as them when asked, under date=System.DateOnly.
    [Theory]
    [InlineData("global::System.DateOnly")]
    [InlineData("global::System.TimeOnly")]
    public void Gives_TypesSqlClientReadsWhenAsked_AreGiven(string text) => SqlServerTypeMap.Gives(text).ShouldBeTrue();

    // A type of the package Microsoft.SqlServer.Types is never the map's, which gives such a type none.
    [Theory]
    [InlineData("global::MyApp.Email")]
    [InlineData("global::MyApp.Email[]")]
    [InlineData("uint")]
    [InlineData("global::Microsoft.SqlServer.Types.SqlHierarchyId")]
    public void Gives_ForeignTypes_AreNot(string text) => SqlServerTypeMap.Gives(text).ShouldBeFalse();

    [Fact]
    public void Map_EveryRowOfTheTable_IsATestedRow()
    {
        var tested = new List<string>();
        foreach (var row in Rows)
        {
            tested.Add(row.Data.Item1);
        }

        tested.ShouldBe(SqlServerTypeMap.Keys, ignoreOrder: true);
    }

    [Theory]
    [InlineData("nvarchar(50)", "nvarchar", "string")]
    [InlineData("datetime2(7)", "datetime2", DateTime)]
    [InlineData("decimal(18,2)", "decimal", "decimal")]
    [InlineData("varchar(max)", "varchar", "string")]
    [InlineData("vector(3)", "vector", Vector)]
    public void Map_FacetedName_IsCutAtItsParenthesis(string name, string key, string expected)
    {
        var type = TestSidecars.SqlServer(name: name);

        SqlServerTypeMap.BaseName(name).ShouldBe(key);
        SqlServerTypeMap.KeyOf(type).ShouldBe(key);
        SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7).Key.ShouldBe(key);
        SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7).Text.ShouldBe(expected);
    }

    [Fact]
    public void Map_AliasType_IsAskedOfTheOverrideFirstThenMappedByItsBase()
    {
        var email = TestSidecars.SqlServer(
            name: "nvarchar(320)",
            userType: new SqlServerUserType("dbo", "Email", null)
        );
        var asked = new List<string>();

        var mapping = SqlServerTypeMap.Map(email, SqlClientColumn.SqlClient7, Recording(asked));

        mapping.Kind.ShouldBe(TypeMappingKind.Mapped);
        mapping.Text.ShouldBe("string");
        mapping.Key.ShouldBe("nvarchar");
        asked.ShouldBe(["dbo.\"Email\"", "nvarchar"]);

        var byAlias = SqlServerTypeMap.Map(email, SqlClientColumn.SqlClient7, Overrides(("dbo.\"Email\"", "Email")));
        byAlias.Text.ShouldBe("Email");

        var byBase = SqlServerTypeMap.Map(email, SqlClientColumn.SqlClient7, Overrides(("nvarchar", "Text")));
        byBase.Text.ShouldBe("Text");
    }

    [Fact]
    public void Map_Sysname_IsString()
    {
        var sysname = TestSidecars.SqlServer(
            name: "nvarchar(128)",
            userType: new SqlServerUserType("sys", "sysname", null)
        );
        var asked = new List<string>();

        var mapping = SqlServerTypeMap.Map(sysname, SqlClientColumn.SqlClient7, Recording(asked));

        mapping.Kind.ShouldBe(TypeMappingKind.Mapped);
        mapping.Text.ShouldBe("string");
        asked.ShouldBe(["sysname", "nvarchar"]);
    }

    [Fact]
    public void Map_ClrType_IsARowByItsName()
    {
        var geography = TestSidecars.SqlServer(
            name: "geography",
            userType: new SqlServerUserType("sys", "geography", Assembly)
        );

        var refused = SqlServerTypeMap.Map(geography, SqlClientColumn.SqlClient7);
        refused.Kind.ShouldBe(TypeMappingKind.NeedsSqlServerTypes);
        refused.Key.ShouldBe("geography");
        refused.Detail.ShouldBe("Microsoft.SqlServer.Types.SqlGeography");

        var overridden = SqlServerTypeMap.Map(geography, SqlClientColumn.SqlClient7, Overrides(("geography", "Geo")));
        overridden.Text.ShouldBe("Geo");

        var money = TestSidecars.SqlServer(name: "Money", userType: new SqlServerUserType("dbo", "Money", Assembly));
        var other = SqlServerTypeMap.Map(money, SqlClientColumn.SqlClient7);
        other.Kind.ShouldBe(TypeMappingKind.Unreadable);
        other.Key.ShouldBe("dbo.\"Money\"");
    }

    // A type that a Microsoft.Data.SqlClient older than 6.1 lacks names the version that first has it.
    [Fact]
    public void Map_VectorBelow61_NamesTheVersionItNeeds()
    {
        var mapping = SqlServerTypeMap.Map(TestSidecars.SqlServer(name: "vector(3)"), SqlClientColumn.OlderThan61);

        mapping.Kind.ShouldBe(TypeMappingKind.NeedsNewerSqlClient);
        mapping.Key.ShouldBe("vector");
        mapping.Detail.ShouldBe("6.1");
    }

    // A type that SqlClient first has in a version is lacked by every column of an older one, and by no SqlClient.
    [Theory]
    [InlineData(nameof(SqlClientColumn.None), nameof(SqlClientColumn.SqlClient61), true)]
    [InlineData(nameof(SqlClientColumn.OlderThan61), nameof(SqlClientColumn.SqlClient61), true)]
    [InlineData(nameof(SqlClientColumn.SqlClient61), nameof(SqlClientColumn.SqlClient61), false)]
    [InlineData(nameof(SqlClientColumn.SqlClient7), nameof(SqlClientColumn.SqlClient61), false)]
    [InlineData(nameof(SqlClientColumn.None), nameof(SqlClientColumn.SqlClient7), true)]
    [InlineData(nameof(SqlClientColumn.OlderThan61), nameof(SqlClientColumn.SqlClient7), true)]
    [InlineData(nameof(SqlClientColumn.SqlClient61), nameof(SqlClientColumn.SqlClient7), true)]
    [InlineData(nameof(SqlClientColumn.SqlClient7), nameof(SqlClientColumn.SqlClient7), false)]
    public void Lacks_ColumnAndFirstColumnOfTheType_IsAnOlderColumn(string column, string since, bool expected) =>
        SqlServerTypeMap
            .Lacks(Enum.Parse<SqlClientColumn>(column), Enum.Parse<SqlClientColumn>(since))
            .ShouldBe(expected);

    // An entry of the type override answers below 6.1 as it does under every column.
    [Fact]
    public void Map_VectorBelow61WithAnEntry_IsTheEntry()
    {
        var mapping = SqlServerTypeMap.Map(
            TestSidecars.SqlServer(name: "vector(3)"),
            SqlClientColumn.OlderThan61,
            Overrides(("vector", "Floats"))
        );

        mapping.Kind.ShouldBe(TypeMappingKind.Mapped);
        mapping.Text.ShouldBe("Floats");
    }

    [Theory]
    [InlineData("hierarchyid", "Microsoft.SqlServer.Types.SqlHierarchyId")]
    [InlineData("geography", "Microsoft.SqlServer.Types.SqlGeography")]
    [InlineData("geometry", "Microsoft.SqlServer.Types.SqlGeometry")]
    public void Map_TypeOfTheSqlServerTypesPackage_NamesItsCSharpType(string name, string detail) =>
        SqlServerTypeMap.Map(TestSidecars.SqlServer(name: name), SqlClientColumn.None).Detail.ShouldBe(detail);

    [Fact]
    public void Map_UnknownSystemName_IsUnreadable()
    {
        var unknown = SqlServerTypeMap.Map(TestSidecars.SqlServer(name: "newtype"), SqlClientColumn.SqlClient7);
        unknown.Kind.ShouldBe(TypeMappingKind.Unreadable);
        unknown.Key.ShouldBe("newtype");

        var empty = SqlServerTypeMap.Map(TestSidecars.SqlServer(name: string.Empty), SqlClientColumn.SqlClient7);
        empty.Kind.ShouldBe(TypeMappingKind.NotWrittenByTheTool);
    }

    [Fact]
    public void Map_Name_IsMatchedIgnoringCase()
    {
        var type = TestSidecars.SqlServer(name: "NVarChar(10)");
        var asked = new List<string>();

        var mapping = SqlServerTypeMap.Map(type, SqlClientColumn.None, Recording(asked));

        mapping.Text.ShouldBe("string");
        mapping.Key.ShouldBe("nvarchar");
        asked.ShouldBe(["nvarchar"]);
    }

    [Fact]
    public void Map_ClrTypeNamedAsAPlainRow_IsUnreadable()
    {
        var type = TestSidecars.SqlServer(name: "int", userType: new SqlServerUserType("sys", "int", Assembly));

        var mapping = SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7);

        mapping.Kind.ShouldBe(TypeMappingKind.Unreadable);
        mapping.Key.ShouldBe("int");
    }

    [Fact]
    public void Map_UserTypeWithoutAName_IsTreatedAsNoUserType()
    {
        var type = TestSidecars.SqlServer(name: "int", userType: new SqlServerUserType("dbo", null, null));

        SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7).Text.ShouldBe("int");
    }

    [Fact]
    public void Map_Override_AnswersBeforeTheRow()
    {
        var mapping = SqlServerTypeMap.Map(TestSidecars.SqlServer(), SqlClientColumn.None, Overrides(("int", "MyInt")));

        mapping.Text.ShouldBe("MyInt");
        mapping.Key.ShouldBe("int");
    }

    private const string Noda = "global::NodaTime.";

    // NodaTime's rows: the type each gives with the library on, all of them value types.
    public static TheoryData<string, string> NodaTimeRows =>
        new()
        {
            { "date", Noda + "LocalDate" },
            { "time", Noda + "LocalTime" },
            { "datetime2", Noda + "LocalDateTime" },
            { "datetime", Noda + "LocalDateTime" },
            { "smalldatetime", Noda + "LocalDateTime" },
            { "datetimeoffset", Noda + "OffsetDateTime" },
        };

    private static LibraryRows NodaTimeOn => LibraryRows.Of([SqlLibrary.NodaTime])!;

    private const string Geometry = "global::NetTopologySuite.Geometries.Geometry";

    private static LibraryRows NetTopologySuiteOn => LibraryRows.Of([SqlLibrary.NetTopologySuite])!;

    [Theory]
    [MemberData(nameof(NodaTimeRows))]
    public void Map_NodaTimeOn_GivesEachRow(string name, string expected)
    {
        foreach (var column in Enum.GetValues<SqlClientColumn>())
        {
            var mapping = SqlServerTypeMap.Map(TestSidecars.SqlServer(name: name), column, libraries: NodaTimeOn);

            ShouldMapTo(mapping, expected, true, name);
            mapping.Library.ShouldBe(SqlLibrary.NodaTime);
            mapping.Detail.ShouldBe("the NodaTime library");
        }
    }

    [Theory]
    [MemberData(nameof(NodaTimeRows))]
    public void Map_NodaTimeOff_IsTheBaseMap(string name, string notExpected)
    {
        var mapping = SqlServerTypeMap.Map(TestSidecars.SqlServer(name: name), SqlClientColumn.SqlClient7);

        mapping.Text.ShouldNotBe(notExpected);
        mapping.Library.ShouldBeNull();
    }

    [Fact]
    public void Map_NodaTimeOn_OtherTypesUnchanged()
    {
        foreach (var name in new[] { "int", "nvarchar(50)", "uniqueidentifier", "varbinary(max)", "bit" })
        {
            var type = TestSidecars.SqlServer(name: name);

            var on = SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7, libraries: NodaTimeOn);

            on.Text.ShouldBe(SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7).Text);
            on.Library.ShouldBeNull();
        }
    }

    [Fact]
    public void Map_NodaTimeOnForDatetime2WithFacets_GivesLocalDateTime()
    {
        var mapping = SqlServerTypeMap.Map(
            TestSidecars.SqlServer(name: "datetime2(3)"),
            SqlClientColumn.SqlClient7,
            libraries: NodaTimeOn
        );

        mapping.Text.ShouldBe(Noda + "LocalDateTime");
        mapping.Key.ShouldBe("datetime2");
        mapping.Library.ShouldBe(SqlLibrary.NodaTime);
    }

    [Fact]
    public void Map_NodaTimeOn_MatchesTheNameIgnoringCase()
    {
        var mapping = SqlServerTypeMap.Map(
            TestSidecars.SqlServer(name: "DATE"),
            SqlClientColumn.SqlClient7,
            libraries: NodaTimeOn
        );

        mapping.Text.ShouldBe(Noda + "LocalDate");
    }

    [Fact]
    public void Map_OverrideAndLibrary_OverrideWins()
    {
        var mapping = SqlServerTypeMap.Map(
            TestSidecars.SqlServer(name: "date"),
            SqlClientColumn.SqlClient7,
            Overrides(("date", "global::System.DateOnly")),
            NodaTimeOn
        );

        mapping.Text.ShouldBe("global::System.DateOnly");
        mapping.Library.ShouldBeNull();
    }

    [Fact]
    public void Map_AliasTypeOverDate_GivesLocalDate()
    {
        var birthday = TestSidecars.SqlServer(name: "date", userType: new SqlServerUserType("dbo", "Birthday", null));
        var asked = new List<string>();

        var mapping = SqlServerTypeMap.Map(birthday, SqlClientColumn.SqlClient7, Recording(asked), NodaTimeOn);

        mapping.Text.ShouldBe(Noda + "LocalDate");
        mapping.Key.ShouldBe("date");
        mapping.Library.ShouldBe(SqlLibrary.NodaTime);
        asked.ShouldBe(["dbo.\"Birthday\"", "date"]);
    }

    [Fact]
    public void Map_ClrUserType_TakesNoRow()
    {
        var clr = TestSidecars.SqlServer(name: "date", userType: new SqlServerUserType("sys", "date", Assembly));
        var other = TestSidecars.SqlServer(name: "date", userType: new SqlServerUserType("dbo", "date", Assembly));
        var geography = TestSidecars.SqlServer(
            name: "geography",
            userType: new SqlServerUserType("sys", "geography", Assembly)
        );

        foreach (var type in new[] { clr, other, geography })
        {
            var mapping = SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7, libraries: NodaTimeOn);

            mapping.Kind.ShouldNotBe(TypeMappingKind.Mapped);
            mapping.Library.ShouldBeNull();
        }
    }

    // SQL Server's spatial CLR types of sys are NetTopologySuite's Geometry, by their name ignoring case, with the
    // library as their place.
    [Theory]
    [InlineData("geography", "geography")]
    [InlineData("geometry", "geometry")]
    [InlineData("Geography", "geography")]
    public void Map_NetTopologySuiteOn_SpatialClrTypeOfSysIsGeometry(string name, string key)
    {
        var type = TestSidecars.SqlServer(name: name, userType: new SqlServerUserType("sys", name, Assembly));

        var mapping = SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7, libraries: NetTopologySuiteOn);

        ShouldMapTo(mapping, Geometry, false, key);
        mapping.Detail.ShouldBe("the NetTopologySuite library");
        mapping.Library.ShouldBe(SqlLibrary.NetTopologySuite);
    }

    // Only a CLR type of sys named geography or geometry takes the row: not hierarchyid, not a user's CLR type of that
    // name, and not a type without its user type, which only a sidecar edited by hand has.
    [Fact]
    public void Map_NetTopologySuiteOn_OtherTypesTakeNoRow()
    {
        var hierarchyId = TestSidecars.SqlServer(
            name: "hierarchyid",
            userType: new SqlServerUserType("sys", "hierarchyid", Assembly)
        );
        var users = TestSidecars.SqlServer(
            name: "geography",
            userType: new SqlServerUserType("dbo", "geography", Assembly)
        );
        var bare = TestSidecars.SqlServer(name: "geography");

        foreach (var type in new[] { hierarchyId, users, bare })
        {
            var mapping = SqlServerTypeMap.Map(type, SqlClientColumn.SqlClient7, libraries: NetTopologySuiteOn);

            mapping.Kind.ShouldNotBe(TypeMappingKind.Mapped);
            mapping.Library.ShouldBeNull();
        }

        NodaTimeOn
            .Find(
                TestSidecars.SqlServer(name: "geography", userType: new SqlServerUserType("sys", "geography", Assembly))
            )
            .ShouldBeNull();
    }

    // The type override is asked first, by the CLR type's key, and wins over the row.
    [Fact]
    public void Map_OverrideAndNetTopologySuite_OverrideWins()
    {
        var geography = TestSidecars.SqlServer(
            name: "geography",
            userType: new SqlServerUserType("sys", "geography", Assembly)
        );

        var mapping = SqlServerTypeMap.Map(
            geography,
            SqlClientColumn.SqlClient7,
            Overrides(("geography", "global::NetTopologySuite.Geometries.Point")),
            NetTopologySuiteOn
        );

        mapping.Text.ShouldBe("global::NetTopologySuite.Geometries.Point");
        mapping.Library.ShouldBeNull();
    }

    private static bool IsClr(string name) => name is "hierarchyid" or "geography" or "geometry";

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

    private static SqlServerOverrideOf Overrides(params (string Key, string Text)[] entries) =>
        key =>
        {
            foreach (var (entryKey, text) in entries)
            {
                if (entryKey.Equals(key, StringComparison.Ordinal))
                {
                    return TypeMapping.Mapped(key, text, false);
                }
            }

            return null;
        };

    private static SqlServerOverrideOf Recording(List<string> asked) =>
        key =>
        {
            asked.Add(key);
            return null;
        };
}
