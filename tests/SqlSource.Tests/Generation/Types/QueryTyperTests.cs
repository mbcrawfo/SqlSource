using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Snapshot;
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
                        new TypeOverride("public._user_status", Guid, SettingPlace.Property)
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

    // The help of each SQL Server type that has no C# type: a sidecar edited by hand, or written by a newer tool, gets
    // an entry to write, with sys. before a name that is no key without it or that is the alias of another type, or
    // the help to describe again.  Each type is a column's, but the null type, which only a parameter can have.
    [Theory]
    [InlineData(
        "geography",
        "column 'c0'",
        "geography",
        "Reference the package Microsoft.SqlServer.Types and map it with the type override, as "
            + "geography=Microsoft.SqlServer.Types.SqlGeography"
    )]
    [InlineData("newtype", "column 'c0'", "newtype", "Map it with the type override, as sys.newtype=<a C# type>")]
    [InlineData(
        "rowversion",
        "column 'c0'",
        "rowversion",
        "Map it with the type override, as sys.rowversion=<a C# type>"
    )]
    [InlineData("null", "parameter 'p0'", "null", "Run 'sqlsource describe'")]
    [InlineData("empty", "column 'c0'", "", "Run 'sqlsource describe'")]
    [InlineData("dbo.Money", "column 'c0'", "Money", "Map it with the type override, as dbo.\"Money\"=<a C# type>")]
    public void Type_SqlServerHelps(string type, string subject, string typeName, string help)
    {
        SidecarType? sidecarType = type switch
        {
            "geography" => TestSidecars.SqlServer(
                "geography",
                maxLength: -1,
                precision: 0,
                userType: new SqlServerUserType("sys", "geography", Assembly)
            ),
            "newtype" => TestSidecars.SqlServer("newtype"),
            "rowversion" => TestSidecars.SqlServer("rowversion", maxLength: 8, precision: 0),
            "null" => null,
            "empty" => TestSidecars.SqlServer(""),
            "dbo.Money" => TestSidecars.SqlServer(
                "Money",
                maxLength: 8,
                precision: 0,
                userType: new SqlServerUserType("dbo", "Money", Assembly)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
        var entry = sidecarType is null
            ? new SidecarEntry(
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
            )
            : Entry(SidecarValues.Engine.SqlServer, columns: [sidecarType]);

        var types = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, SettingsLevel.None));

        types.Queries.ShouldBeEmpty();
        var diagnostic = types.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.TypeHasNoCSharpType);
        diagnostic.Arguments.ShouldBe([subject, typeName, help]);
    }

    // The property serves both engines: a name both have reaches each engine's own type of that name, and a name one
    // engine alone reads reaches that engine's files alone, so timestamp, which both engines have, is mapped only in
    // the file of the engine whose key it is.
    [Theory]
    [InlineData("int=long", "long", "long", "byte[]")]
    [InlineData("int4=long", "long", "int", "byte[]")]
    [InlineData("rowversion=MyApp.Version", "int", "int", Version)]
    public void Type_SharedNameInTheProperty_ReachesEachEngine(
        string property,
        string postgresInt,
        string sqlServerInt,
        string sqlServerTimestamp
    )
    {
        var level = PropertyOf(property);
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
    [InlineData("DBO.EMAIL=MyApp.Email", Email)]
    [InlineData("\"dbo\".\"email\"=MyApp.Email", "string")]
    public void Type_UserKeyInAnotherCase_Matches(string property, string expected)
    {
        var entry = Entry(SidecarValues.Engine.SqlServer, columns: [EmailType()]);

        var types = QueryTyper.Type(InputOf(SqlDialect.SqlServer, entry, PropertyOf(property)));

        types.Diagnostics.ShouldBeEmpty();
        ColumnTexts(types).ShouldBe([expected]);
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

    // The property's level, read as an MSBuild value is, by each engine's rules.
    private static SettingsLevel PropertyOf(string value)
    {
        var invalid = new List<string>();
        var lists = TypeOverrideGrammar.ReadList(value, SettingPlace.Property, invalid);
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

    // The file Users of the dialect with the one query GetUser and its entry, under the property's level, Npgsql 10
    // and the SqlClient given, SqlClient 6.1 unless it is.
    private static FileTypesInput InputOf(
        SqlDialect dialect,
        SidecarEntry entry,
        SettingsLevel property,
        SqlClientProbe? sqlClient = null
    )
    {
        var file = SqlFileReader.Read(
            new InMemoryAdditionalText(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n"),
            GeneratorHarness.UsersNormalizedPath,
            dialect,
            null,
            false,
            TestContext.Current.CancellationToken
        );

        return new FileTypesInput(
            new SidecarCheckResult(file.NormalizedPath, EquatableArray<DiagnosticInfo>.Empty, TestSidecars.Of(entry)),
            file,
            SettingsLevel.None,
            property,
            new NpgsqlProbe(true, true, true, new Version(10, 0)),
            sqlClient ?? SqlClient61,
            TestSidecars.Of(Found(Email, false), Found(Version, false), Found(Guid, true), Found("long", true))
        );
    }

    private static MappedType Found(string name, bool isValueType) =>
        new(name, MappedTypeStatus.Found, isValueType, true, EquatableArray<string>.Empty);
}
