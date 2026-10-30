using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Generation.Types;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Generation.Types.MappedTypes;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Types;

// The typer called directly, for what no sidecar the reader accepts can hold, and for SQL Server's map and type
// override over one entry built here.  The driver tests of the type override are in
// tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs, and those of SQL Server's models in
// tests/SqlSource.Tests/Generator/SqlServerModelTests.cs.
public sealed class QueryTyperTests
{
    private const string Guid = "global::System.Guid";
    private const string DateTime = "global::System.DateTime";
    private const string Email = "global::MyApp.Email";
    private const string Version = "global::MyApp.Version";
    private const string Assembly = "MyApp.Types, Version=1.0.0.0";

    private static readonly SqlClientProbe WithoutSqlClient = new(false, false, false, null);

    private static readonly SqlClientProbe SqlClient61 = new(true, true, false, "6.1.7");

    private static readonly SqlClientProbe SqlClient60 = new(true, false, false, "6.0.5");

    // A driver below 6.1 merged into an assembly of another name, whose version is unknown.
    private static readonly SqlClientProbe MergedSqlClient60 = new(true, false, false, null);

    // The reader refuses an array without its element, which only a sidecar edited by hand has.  Such an array has no
    // key to write, so an entry of its own name does not map it: it gets the help to describe again.
    [Fact]
    public void Type_ArrayWithoutItsElement_IsSqlsrc246EvenWithAnEntryOfItsOwnKey()
    {
        var array = TestSidecars.Postgres(
            name: "user_status[]",
            kind: SidecarValues.Kind.Array,
            schema: "public",
            internalName: "_user_status"
        );

        var types = QueryTyper.Type(
            InputOf(
                SqlDialect.PostgreSql,
                Entry(SidecarValues.Engine.Postgres, columns: [array]),
                new SettingsLevel
                {
                    TypeOverrides = TestSidecars.Of(
                        new TypeOverride(
                            "public._user_status",
                            Guid,
                            SettingPlace.OfSettingsFile("/app/sqlsource.json", 1)
                        )
                    ),
                }
            )
        );

        types.Queries.ShouldBeEmpty();
        var diagnostic = types.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.TypeHasNoCSharpType);
        diagnostic.Arguments.ShouldBe(["column 'c0'", "user_status[]", "Run 'sqlsource describe'"]);
    }

    // Each type of an mssql file's entry is SQL Server's map's, an alias type's its base's, facets dropped.
    [Fact]
    public void Type_SqlServerEntry_IsTypedByItsMap()
    {
        var entry = Entry(
            SidecarValues.Engine.SqlServer,
            parameters: [TestSidecars.SqlServer("int"), TestSidecars.SqlServer("nvarchar(50)", maxLength: 100)],
            columns:
            [
                TestSidecars.SqlServer("datetime2(7)", maxLength: 8, precision: 27, scale: 7),
                TestSidecars.SqlServer("uniqueidentifier", maxLength: 16, precision: 0),
                EmailType(),
            ]
        );

        var types = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsLevel.None));

        types.Diagnostics.ShouldBeEmpty();
        var query = types.Queries.ShouldHaveSingleItem();
        query.Parameters.Select(type => type.Text).ShouldBe(["int", "string"]);
        query.Columns.ShouldNotBeNull().Select(type => type.Text).ShouldBe([DateTime, Guid, "string"]);
    }

    // vector's C# type is SqlClient's own, which a project without SqlClient cannot name.
    [Fact]
    public void Type_SqlServerTypeOfSqlClientWithoutIt_IsSqlsrc269()
    {
        var entry = Entry(
            SidecarValues.Engine.SqlServer,
            columns: [TestSidecars.SqlServer("vector(3)", maxLength: 20, precision: 0)]
        );

        var without = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsLevel.None, WithoutSqlClient));
        var with = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsLevel.None, SqlClient61));

        without.Queries.ShouldBeEmpty();
        var diagnostic = without.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.TypeNeedsSqlClient);
        diagnostic.Arguments.ShouldBe(["column 'c0'", "vector(3)", "Reference Microsoft.Data.SqlClient"]);
        with.Diagnostics.ShouldBeEmpty();
        with.Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::Microsoft.Data.SqlTypes.SqlVector<float>");
    }

    // vector's C# type is first in SqlClient 6.1, which an older one lacks: the error names both versions, for a
    // column and for a parameter alike.
    [Theory]
    [InlineData(true, "column 'c0'")]
    [InlineData(false, "parameter 'p0'")]
    public void Type_VectorBelowSqlClient61_IsSqlsrc270(bool asColumn, string subject)
    {
        SidecarType[] vector = [TestSidecars.SqlServer("vector(3)", maxLength: 20, precision: 0)];
        var entry = asColumn
            ? Entry(SidecarValues.Engine.SqlServer, columns: vector)
            : Entry(SidecarValues.Engine.SqlServer, parameters: vector);

        var types = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsLevel.None, SqlClient60));

        types.Queries.ShouldBeEmpty();
        var diagnostic = types.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.TypeNeedsNewerSqlClient);
        diagnostic.Arguments.ShouldBe([subject, "vector(3)", "6.1", "6.0.5"]);
    }

    [Fact]
    public void Type_VectorBelowSqlClient61OfAnUnknownVersion_SaysTheVersionIsUnknown()
    {
        var entry = Entry(
            SidecarValues.Engine.SqlServer,
            columns: [TestSidecars.SqlServer("vector(3)", maxLength: 20, precision: 0)]
        );

        var types = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsLevel.None, MergedSqlClient60));

        types
            .Diagnostics.ShouldHaveSingleItem()
            .Arguments.ShouldBe(["column 'c0'", "vector(3)", "6.1", "of an unknown version"]);
    }

    // The help of each SQL Server type that has no C# type: a sidecar edited by hand, or written by a newer tool, gets
    // an entry to write, with sys. before a name that is no key without it or that is the alias of another type, or
    // the help to describe again.  Each type is a column's, but the null type, which only a parameter can have.
    [Fact]
    public void Type_SqlServerGeographyWithNoMapping_GivesTheHelp() =>
        ShouldGiveTheHelp(
            Entry(
                SidecarValues.Engine.SqlServer,
                columns:
                [
                    TestSidecars.SqlServer(
                        "geography",
                        maxLength: -1,
                        precision: 0,
                        userType: new SqlServerUserType("sys", "geography", Assembly)
                    ),
                ]
            ),
            "column 'c0'",
            "geography",
            "Reference NetTopologySuite.IO.SqlServerBytes and turn the netTopologySuite library on, to read it as "
                + "NetTopologySuite's Geometry in a codegen query, or reference the package Microsoft.SqlServer.Types "
                + "and map it with the type override, as geography=Microsoft.SqlServer.Types.SqlGeography"
        );

    [Fact]
    public void Type_SqlServerNewTypeWithNoMapping_GivesTheHelp() =>
        ShouldGiveTheHelp(
            Entry(SidecarValues.Engine.SqlServer, columns: [TestSidecars.SqlServer("newtype")]),
            "column 'c0'",
            "newtype",
            "Map it with the type override, as sys.newtype=<a C# type>"
        );

    [Fact]
    public void Type_SqlServerRowversionWithNoMapping_GivesTheHelp() =>
        ShouldGiveTheHelp(
            Entry(
                SidecarValues.Engine.SqlServer,
                columns: [TestSidecars.SqlServer("rowversion", maxLength: 8, precision: 0)]
            ),
            "column 'c0'",
            "rowversion",
            "Map it with the type override, as sys.rowversion=<a C# type>"
        );

    [Fact]
    public void Type_SqlServerNullTypeWithNoMapping_GivesTheHelp() =>
        ShouldGiveTheHelp(
            new SidecarEntry(
                "GetUser",
                default,
                "h",
                SidecarValues.Engine.SqlServer,
                "app",
                "17.0",
                SidecarResultKind.Rows,
                null,
                SidecarValues.Plan.NotNeeded,
                SidecarValues.TableMatch.NoOrigin,
                TestSidecars.Of(TestSidecars.Parameter(0, "p0", null)),
                TestSidecars.Of(TestSidecars.Column(0, "c0", TestSidecars.SqlServer()))
            ),
            "parameter 'p0'",
            "null",
            "Run 'sqlsource describe'"
        );

    [Fact]
    public void Type_SqlServerEmptyTypeWithNoMapping_GivesTheHelp() =>
        ShouldGiveTheHelp(
            Entry(SidecarValues.Engine.SqlServer, columns: [TestSidecars.SqlServer("")]),
            "column 'c0'",
            "",
            "Run 'sqlsource describe'"
        );

    [Fact]
    public void Type_SqlServerUserTypeWithNoMapping_GivesTheHelp() =>
        ShouldGiveTheHelp(
            Entry(
                SidecarValues.Engine.SqlServer,
                columns:
                [
                    TestSidecars.SqlServer(
                        "Money",
                        maxLength: 8,
                        precision: 0,
                        userType: new SqlServerUserType("dbo", "Money", Assembly)
                    ),
                ]
            ),
            "column 'c0'",
            "Money",
            "Map it with the type override, as dbo.\"Money\"=<a C# type>"
        );

    // An object of a settings file serves both engines: a name both have reaches each engine's own type of that name,
    // and a name one engine alone reads reaches that engine's files alone, so timestamp, which both engines have, is
    // mapped only in the file of the engine whose key it is.
    [Theory]
    [InlineData("int", "long", "long", "long", "byte[]")]
    [InlineData("int4", "long", "long", "int", "byte[]")]
    [InlineData("rowversion", "MyApp.Version", "int", "int", Version)]
    public void Type_SharedNameInASettingsFile_ReachesEachEngine(
        string key,
        string type,
        string postgresInt,
        string sqlServerInt,
        string sqlServerTimestamp
    )
    {
        var level = SettingsFileLevelOf(key, type);
        var postgres = Entry(
            SidecarValues.Engine.Postgres,
            columns:
            [
                TestSidecars.Postgres(),
                TestSidecars.Postgres(name: "timestamp without time zone", internalName: "timestamp"),
            ]
        );
        var sqlServer = Entry(
            SidecarValues.Engine.SqlServer,
            columns: [TestSidecars.SqlServer("int"), TestSidecars.SqlServer("timestamp", maxLength: 8, precision: 0)]
        );

        var postgresTypes = QueryTyper.Type(InputOf(SqlDialect.PostgreSql, postgres, level));
        var sqlServerTypes = QueryTyper.Type(InputOf(SqlDialect.SqlServer, sqlServer, level));

        postgresTypes.Diagnostics.ShouldBeEmpty();
        sqlServerTypes.Diagnostics.ShouldBeEmpty();
        ColumnTexts(postgresTypes).ShouldBe([postgresInt, DateTime]);
        ColumnTexts(sqlServerTypes).ShouldBe([sqlServerInt, sqlServerTimestamp]);
    }

    // A plain part of a user's key matches the type's name in any case, and a quoted part its own case alone.
    [Theory]
    [InlineData("DBO.EMAIL", Email)]
    [InlineData("\"dbo\".\"email\"", "string")]
    public void Type_UserKeyInAnotherCase_Matches(string key, string expected)
    {
        var entry = Entry(SidecarValues.Engine.SqlServer, columns: [EmailType()]);

        var types = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsFileLevelOf(key, "MyApp.Email")));

        types.Diagnostics.ShouldBeEmpty();
        ColumnTexts(types).ShouldBe([expected]);
    }

    // A settings file names itself in a diagnostic about a type it mapped: its path, and the setting it wrote.
    [Fact]
    public void Type_TypeOverrideOfASettingsFile_NamesTheFileInSqlsrc250()
    {
        var entry = Entry(SidecarValues.Engine.Postgres, columns: [TestSidecars.Postgres()]);
        var level = new SettingsLevel
        {
            TypeOverrides = TestSidecars.Of(
                new TypeOverride("int4", "MyApp.Missing", SettingPlace.OfSettingsFile("/app/sqlsource.json", 1))
            ),
        };

        var types = QueryTyper.Type(InputOf(SqlDialect.PostgreSql, entry, level));

        var diagnostic = types.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.MappedTypeNotFound);
        diagnostic.Arguments[3].ShouldBe("the typeOverrides of the settings file /app/sqlsource.json");
    }

    [Fact]
    public void Type_ColumnTypeOfASettingsFile_NamesTheFileInSqlsrc250()
    {
        var origin = new SidecarOrigin("public", "users", "id");
        var entry = TestSidecars.Entry(
            "GetUser",
            columns: [TestSidecars.Column(0, "id", TestSidecars.Postgres(), origin: origin)]
        );
        var level = new SettingsLevel
        {
            ColumnTypes = TestSidecars.Of(
                new ColumnType(
                    "public.users.id",
                    "MyApp.Missing",
                    SettingPlace.OfSettingsFile("/app/sqlsource.json", 1)
                )
            ),
        };

        var types = QueryTyper.Type(InputOf(SqlDialect.PostgreSql, entry, level));

        var diagnostic = types.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.MappedTypeNotFound);
        diagnostic.Arguments[3].ShouldBe("the columnTypes of the settings file /app/sqlsource.json");
    }

    // The entry under mssql, with no type override, gives no query and SQLSRC246 with the subject, the type's name and
    // the help.
    private static void ShouldGiveTheHelp(SidecarEntry entry, string subject, string typeName, string help)
    {
        var types = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsLevel.None));

        types.Queries.ShouldBeEmpty();
        var diagnostic = types.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.TypeHasNoCSharpType);
        diagnostic.Arguments.ShouldBe([subject, typeName, help]);
    }

    // An alias type of dbo over nvarchar(320).
    private static SqlServerType EmailType() =>
        TestSidecars.SqlServer(
            "nvarchar(320)",
            maxLength: 640,
            precision: 0,
            userType: new SqlServerUserType("dbo", "Email", null)
        );

    private static IEnumerable<string> ColumnTexts(FileTypes types) =>
        types.Queries.ShouldHaveSingleItem().Columns.ShouldNotBeNull().Select(type => type.Text);

    // The level of a settings file whose type override has the one member, read by each engine's rules.
    private static SettingsLevel SettingsFileLevelOf(string key, string type)
    {
        var invalid = new List<int>();
        var lists = TypeOverrideGrammar.ReadMembers(
            [new KeyValuePair<string, string>(key, type)],
            SettingPlace.OfSettingsFile("/app/sqlsource.json", 1),
            invalid,
            []
        );
        invalid.ShouldBeEmpty();
        return new SettingsLevel { TypeOverrides = lists.PostgreSql, SqlServerTypeOverrides = lists.SqlServer };
    }

    // The entry GetUser, with a parameter p<n> and a column c<n> of each type.
    private static SidecarEntry Entry(string engine, SidecarType[]? parameters = null, SidecarType[]? columns = null) =>
        TestSidecars.Entry(
            "GetUser",
            engine: engine,
            parameters: parameters?.Select((type, at) => TestSidecars.Parameter(at, "p" + at, type)).ToArray(),
            columns: columns?.Select((type, at) => TestSidecars.Column(at, "c" + at, type)).ToArray()
        );

    // The file Users of the dialect with the one query GetUser and its entry, under the file's level, Npgsql 10 and the
    // SqlClient given, SqlClient 6.1 unless it is, and no library.
    private static FileTypesInput InputOf(
        SqlDialect dialect,
        SidecarEntry entry,
        SettingsLevel level,
        SqlClientProbe? sqlClient = null
    )
    {
        var file = SqlFileReader.Read(
            new InMemoryAdditionalText(GeneratorHarness.UsersPath, SqlTexts.GetUser),
            GeneratorHarness.UsersNormalizedPath,
            dialect,
            false,
            TestContext.Current.CancellationToken
        );

        return new FileTypesInput(
            new SidecarCheckResult(file.NormalizedPath, EquatableArray<DiagnosticInfo>.Empty, TestSidecars.Of(entry)),
            file,
            level,
            new NpgsqlProbe(true, true, true, new Version(10, 0)),
            sqlClient ?? SqlClient61,
            new LibraryProbe(NodaTime: false, NpgsqlNodaTime: false, NodaTimeVersion: null),
            TestSidecars.Of(Found(Email, false), Found(Version, false), Found(Guid, true), Found("long", true))
        );
    }

    private static MappedType Found(string name, bool isValueType) =>
        new(
            name,
            MappedTypeStatus.Found,
            isValueType,
            true,
            EquatableArray<string>.Empty,
            name,
            EquatableArray<string>.Empty,
            null
        );
}
