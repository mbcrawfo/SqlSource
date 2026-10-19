// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The C# type of each parameter and column of a usable entry, read from what the step of the types gave, since
// nothing of the generator emits them yet.  Each run is at Models under postgres, with an entry written for the
// query's current hash.
public sealed class QueryTypesTests
{
    private const string UsersPath = "/app/Repo/Users.sql";

    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;
        }

        namespace MyApp
        {
            public sealed class Tag;
        }
        """;

    // The name of GetUser in Users.
    private const string GetUserName = "/app/Repo/Users.sql(1,10)-(1,17)";

    // The warning ids of a C# type whose use gives none.
    private static readonly EquatableArray<string> NoWarnings = EquatableArray<string>.Empty;

    private static readonly SqlFile Users = new(UsersPath, "-- name: GetUser\nSELECT 1;\n");

    private static readonly PropertyType Int = new("int", true, true, NoWarnings, "int4", null);

    [Theory]
    [InlineData("none")]
    [InlineData("8")]
    [InlineData("10")]
    public void Run_Int4_IsIntUnderEachDriver(string driver)
    {
        var run = Run(
            Driver(driver),
            Users,
            Entry(Users, "GetUser", [Parameter("id", Catalog("int4"))], [Column("id", Catalog("int4"))])
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .Queries.ShouldHaveSingleItem()
            .ShouldBe(new QueryTypes("GetUser", Of(Int), Of(Int)));
    }

    [Theory]
    [InlineData("8", "global::System.DateTime")]
    [InlineData("9", "global::System.DateTime")]
    [InlineData("10", "global::System.DateOnly")]
    public void Run_Date_IsTheTypeOfTheDriver(string driver, string expected)
    {
        var run = Run(Driver(driver), Users, Entry(Users, "GetUser", [], [Column("born", Catalog("date"))]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run).ShouldBe([new PropertyType(expected, true, true, NoWarnings, "date", null)]);
    }

    [Fact]
    public void Run_CubeOnNpgsql8_IsSqlsrc246AtTheQuery()
    {
        var run = Run(
            [NpgsqlReferences.Npgsql8],
            Users,
            Entry(Users, "GetUser", [], [Column("box", Extension("cube"))])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'box' has the type 'cube', which SqlSource cannot give a C# type.  "
                + "Update Npgsql to 10, or cast it to text in the query.",
        ]);
        run.SeverityOf("SQLSRC246").ShouldBe(DiagnosticSeverity.Error);
        StepOutputs.Single<FileTypes>(run, TrackingNames.FileTypes).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_CubeOnNpgsql10_IsNpgsqlCube()
    {
        var run = Run(
            [NpgsqlReferences.Npgsql10],
            Users,
            Entry(Users, "GetUser", [], [Column("box", Extension("cube"))])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run)
            .ShouldBe([new PropertyType("global::NpgsqlTypes.NpgsqlCube", true, true, NoWarnings, "cube", null)]);
    }

    [Fact]
    public void Run_Regclass_IsSqlsrc246WithTheCast()
    {
        var run = Run(
            [NpgsqlReferences.Npgsql10],
            Users,
            Entry(Users, "GetUser", [], [Column("relation", Catalog("regclass"))])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'relation' has the type 'regclass', which SqlSource cannot give a "
                + "C# type.  Cast it to oid in the query.",
        ]);
        StepOutputs.Single<FileTypes>(run, TrackingNames.FileTypes).Queries.ShouldBeEmpty();
    }

    // regclass[]::oid is an error of the server, and regclass[]::oid[] reads the array.
    [Fact]
    public void Run_ArrayOfRegclass_IsSqlsrc246WithTheCastToAnArray()
    {
        var array = TestSidecars.Postgres(
            name: "regclass[]",
            kind: SidecarValues.Kind.Array,
            internalName: "_regclass",
            element: Catalog("regclass")
        );

        var run = Run([NpgsqlReferences.Npgsql10], Users, Entry(Users, "GetUser", [], [Column("relations", array)]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'relations' has the type 'regclass[]', which SqlSource cannot give "
                + "a C# type.  Cast it to oid[] in the query.",
        ]);
    }

    // No Npgsql reads these, and the type override cannot help: an entry would compile and fail as the query runs.
    // A base type of pg_catalog that SqlSource does not know is one of them, and its bare name is no key.
    [Theory]
    [InlineData("pg_snapshot", "pg_snapshot", "text")]
    [InlineData("txid_snapshot", "txid_snapshot", "text")]
    [InlineData("pg_node_tree", "pg_node_tree", "text")]
    [InlineData("aclitem", "aclitem", "text")]
    [InlineData("aclitem[]", "_aclitem", "text[]")]
    public void Run_TypeNoNpgsqlReads_IsSqlsrc246WithTheCastToText(string name, string internalName, string cast)
    {
        var type = name.EndsWith("[]", StringComparison.Ordinal)
            ? TestSidecars.Postgres(
                name: name,
                kind: SidecarValues.Kind.Array,
                internalName: internalName,
                element: Catalog("aclitem")
            )
            : Catalog(internalName);

        var run = Run([NpgsqlReferences.Npgsql10], Users, Entry(Users, "GetUser", [], [Column("value", type)]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'value' has the type '{name}', which SqlSource cannot give a C# "
                + $"type.  Cast it to {cast} in the query.",
        ]);
    }

    // pg_class is no key, so the entry names its schema, and pg_catalog.pg_class is read as pg_class.
    [Fact]
    public void Run_CompositeOfTheCatalog_IsSqlsrc246WithTheQualifiedKey()
    {
        var pgClass = TestSidecars.Postgres(
            name: "pg_class",
            kind: SidecarValues.Kind.Composite,
            internalName: "pg_class"
        );
        var mapped = new SqlFile(
            UsersPath,
            "-- type-override: pg_catalog.pg_class=MyApp.Tag\n-- name: GetUser\nSELECT 1;\n"
        );

        var run = Run([NpgsqlReferences.Npgsql10], Users, Entry(Users, "GetUser", [], [Column("relation", pgClass)]));
        var mappedRun = Run(
            [NpgsqlReferences.Npgsql10],
            mapped,
            Entry(mapped, "GetUser", [], [Column("relation", pgClass)])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'relation' has the type 'pg_class', which SqlSource cannot give a "
                + "C# type.  Map it with the type override, as pg_catalog.pg_class=<a C# type>.",
        ]);
        mappedRun.Diagnostics.ShouldBeEmpty();
        ColumnsOf(mappedRun)
            .ShouldBe([
                new PropertyType(
                    "global::MyApp.Tag",
                    false,
                    true,
                    NoWarnings,
                    "pg_class",
                    "a -- type-override: marker in the file header"
                ),
            ]);
    }

    // The tool never writes record, unknown or void, or a kind this version does not know, so describing again
    // mends the sidecar.
    [Theory]
    [InlineData("record", "base")]
    [InlineData("void", "base")]
    [InlineData("users", "table")]
    public void Run_TypeTheToolNeverWrites_IsSqlsrc246WithDescribe(string internalName, string kind)
    {
        var type = TestSidecars.Postgres(name: internalName, kind: kind, internalName: internalName);

        var run = Run([NpgsqlReferences.Npgsql10], Users, Entry(Users, "GetUser", [], [Column("row", type)]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'row' has the type '{internalName}', which SqlSource cannot give a "
                + "C# type.  Run 'sqlsource describe'.",
        ]);
    }

    // NpgsqlRange<T> is a type of Npgsql, so a project without it is told to reference it first.
    [Theory]
    [InlineData("10", "Map it")]
    [InlineData("none", "Reference Npgsql, then map it")]
    public void Run_UserDefinedRange_IsSqlsrc246WithTheRangeAndTheOptIn(string driver, string start)
    {
        var floatRange = TestSidecars.Postgres(
            name: "floatrange",
            kind: SidecarValues.Kind.Range,
            schema: "public",
            internalName: "floatrange",
            subtype: Catalog("float8")
        );

        var run = Run(Driver(driver), Users, Entry(Users, "GetUser", [], [Column("span", floatRange)]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'span' has the type 'floatrange', which SqlSource cannot give a C# "
                + $"type.  {start} with the type override, as public.floatrange=NpgsqlTypes.NpgsqlRange<T> with the "
                + "data source's EnableUnmappedTypes().",
        ]);
    }

    // No engine in scope writes a parameter without a type, so only a sidecar edited by hand has one.
    [Fact]
    public void Run_ParameterWithoutAType_IsSqlsrc246()
    {
        var run = Run([NpgsqlReferences.Npgsql10], Users, Entry(Users, "GetUser", [Parameter("id", null)], null));

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The parameter 'id' has the type 'null', which SqlSource cannot give a C# "
                + "type.  Run 'sqlsource describe'.",
        ]);
        StepOutputs.Single<FileTypes>(run, TrackingNames.FileTypes).Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Run_Composite_IsSqlsrc246()
    {
        var address = TestSidecars.Postgres(
            name: "address",
            kind: SidecarValues.Kind.Composite,
            schema: "public",
            internalName: "address"
        );

        var run = Run([NpgsqlReferences.Npgsql10], Users, Entry(Users, "GetUser", [], [Column("home", address)]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The column 'home' has the type 'address', which SqlSource cannot give a C# "
                + "type.  Map it with the type override, as public.address=<a C# type>.",
        ]);
    }

    // The first failure stops the query, and the parameters are typed before the columns: the column of cube, which
    // Npgsql 8 cannot read either, is not reported.  The other query of the file is typed.
    [Fact]
    public void Run_QueryWithTwoFailures_ReportsTheFirstAndLeavesTheOtherQueryTyped()
    {
        var file = new SqlFile(UsersPath, "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n");

        var run = Run(
            [NpgsqlReferences.Npgsql8],
            file,
            Entry(file, "GetUser", [Parameter("relation", Catalog("regclass"))], [Column("box", Extension("cube"))]),
            Entry(file, "ListUsers", [], [Column("id", Catalog("int4"))])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {GetUserName}: The parameter 'relation' has the type 'regclass', which SqlSource cannot give "
                + "a C# type.  Cast it to oid in the query.",
        ]);
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .Queries.ShouldHaveSingleItem()
            .ShouldBe(new QueryTypes("ListUsers", Of<PropertyType>(), Of(Int)));
    }

    // GetUser needs no entry at Sql, so it has none to type, and ListUsers, at Models, has its own.
    [Fact]
    public void Run_QueryAtSql_HasNoTypes()
    {
        var file = new SqlFile(
            UsersPath,
            "-- name: GetUser\n-- output: sql\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n"
        );

        var run = Run([NpgsqlReferences.Npgsql10], file, Entry(file, "ListUsers", [], [Column("id", Catalog("int4"))]));

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .Queries.Select(query => query.Name)
            .ShouldBe(["ListUsers"]);
    }

    [Fact]
    public void Run_StaleEntry_HasNoTypes()
    {
        var stale = TestSidecars.Entry(
            "GetUser",
            "stale",
            "postgres",
            "main",
            "18.0",
            SidecarResultKind.Rows,
            columns: [Column("id", Catalog("int4"))]
        );

        var run = Run([NpgsqlReferences.Npgsql10], Users, stale);

        run.Diagnostics.ShouldBe([
            $"SQLSRC244 {GetUserName}: The query 'GetUser' has changed since its entry was written.  Run 'sqlsource "
                + "describe'.",
        ]);
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .ShouldBe(new FileTypes("app/Repo/Users.sql", Of<QueryTypes>(), EquatableArray<DiagnosticInfo>.Empty));
    }

    // The entries are written in the other order, and the types follow the file's.  An entry without rows has no
    // columns.
    [Fact]
    public void Run_TwoQueries_KeepTheOrderOfTheFile()
    {
        var file = new SqlFile(UsersPath, "-- name: GetUser\nSELECT 1;\n\n-- name: DeleteUser\nDELETE FROM users;\n");

        var run = Run(
            [NpgsqlReferences.Npgsql10],
            file,
            Entry(file, "DeleteUser", [Parameter("id", Catalog("int4"))], null),
            Entry(file, "GetUser", [], [Column("id", Catalog("int4")), Column("name", Catalog("text"))])
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .Queries.ShouldBe([
                new QueryTypes(
                    "GetUser",
                    Of<PropertyType>(),
                    Of(Int, new PropertyType("string", false, true, NoWarnings, "text", null))
                ),
                new QueryTypes("DeleteUser", Of(Int), null),
            ]);
    }

    // A domain over an array over a domain over text: the array's element is the inner domain, and its type is
    // text's, or the override's of the inner domain, which reaches it through the array and the outer domain.
    [Theory]
    [InlineData("", "string[]", "text", null)]
    [InlineData(
        "-- type-override: public.tag=MyApp.Tag\n",
        "global::MyApp.Tag[]",
        "public.tag",
        "a -- type-override: marker in the file header"
    )]
    public void Run_DomainOverAnArrayOverADomain_IsAnArrayOfTheInnerDomainsType(
        string marker,
        string expected,
        string key,
        string? place
    )
    {
        var tag = TestSidecars.Postgres(
            name: "tag",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "tag",
            baseType: Catalog("text")
        );
        var tags = TestSidecars.Postgres(
            name: "tags",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "tags",
            baseType: TestSidecars.Postgres(
                name: "tag[]",
                kind: SidecarValues.Kind.Array,
                schema: "public",
                internalName: "_tag",
                element: tag
            )
        );
        var file = new SqlFile(UsersPath, marker + "-- name: GetUser\nSELECT 1;\n");

        var run = Run([NpgsqlReferences.Npgsql10], file, Entry(file, "GetUser", [], [Column("tags", tags)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run).ShouldBe([new PropertyType(expected, false, true, NoWarnings, key, place)]);
    }

    private static MetadataReference[] Driver(string driver) =>
        driver switch
        {
            "none" => [],
            "8" => [NpgsqlReferences.Npgsql8],
            "9" => [NpgsqlReferences.Npgsql9],
            "10" => [NpgsqlReferences.Npgsql10],
            _ => throw new ArgumentOutOfRangeException(nameof(driver), driver, "No such driver."),
        };

    private static GeneratorRun Run(MetadataReference[] references, SqlFile file, params SidecarEntry[] entries) =>
        GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, entries)],
            references: references,
            dialect: "postgres"
        );

    // An entry of the query for its current hash: with rows when it has columns, and without when they are null.
    private static SidecarEntry Entry(
        SqlFile file,
        string name,
        SidecarParameter[] parameters,
        SidecarColumn[]? columns
    ) =>
        TestSidecars.Entry(
            name,
            Sidecars.HashOf(file, name, SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            columns is null ? SidecarResultKind.None : SidecarResultKind.Rows,
            parameters: [.. parameters.Select((parameter, ordinal) => parameter with { Ordinal = ordinal })],
            columns: columns?.Select((column, ordinal) => column with { Ordinal = ordinal }).ToArray()
        );

    private static SidecarParameter Parameter(string name, SidecarType? type) => TestSidecars.Parameter(0, name, type);

    private static SidecarColumn Column(string name, SidecarType type) => TestSidecars.Column(0, name, type);

    private static PostgresType Catalog(string internalName) =>
        TestSidecars.Postgres(name: internalName, internalName: internalName);

    private static PostgresType Extension(string internalName) =>
        TestSidecars.Postgres(name: internalName, schema: "public", internalName: internalName);

    private static EquatableArray<T> Of<T>(params T[] items)
        where T : IEquatable<T> => TestSidecars.Of(items);

    // The column types of the one query.
    private static EquatableArray<PropertyType> ColumnsOf(GeneratorRun run) =>
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull();
}
