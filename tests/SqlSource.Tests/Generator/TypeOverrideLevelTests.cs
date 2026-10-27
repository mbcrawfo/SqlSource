// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The type override at each of its levels, merged per key, and the lookup of a type's key, as the types of the
// queries show them.  Each run is at Models under postgres on Npgsql 10, with an entry written for each query's
// current hash.
public sealed class TypeOverrideLevelTests
{
    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;
        }

        namespace MyApp
        {
            public class Outer
            {
                private sealed class Closed;
            }

            [System.Obsolete("Use another.")]
            public sealed class OldThing;

            internal readonly struct Inner;
        }
        """;

    private const string DateTimeOffset = "global::System.DateTimeOffset";

    private const string ThePropertyPlace = "the property SqlSourceTypeOverrides";

    private const string TheMetadataPlace = "the metadata SqlSourceTypeOverrides of the file";

    private const string TheProjectFilePlace =
        "the typeOverrides of the settings file " + GeneratorHarness.ProjectSettingsPath;

    private const string TheFolderFilePlace = "the typeOverrides of the settings file /app/Repo/sqlsource.json";

    private const string TheHeaderPlace = "a -- type-override: marker in the file header";

    private const string TheQueryPlace = "a -- type-override: marker of the query";

    private const string QuotedKey = "\"my schema\".\"order-status\"";

    // The warning ids of a C# type whose use gives none.
    private static readonly EquatableArray<string> NoWarnings = EquatableArray<string>.Empty;

    // What the base map alone reads the database types of these tests as, which a type that an override chose
    // carries: int4, timestamptz, text and an enum, and a composite, which it gives no C# type.
    private static readonly DatabaseType IntBase = new("int", true, false);

    private static readonly DatabaseType DateTimeBase = new("global::System.DateTime", true, false);

    private static readonly DatabaseType StringBase = new("string", false, false);

    private static readonly DatabaseType NoBase = new(null, false, false);

    private static readonly PostgresType Timestamptz = Catalog("timestamptz");

    // A key of timestamptz at each level below a default's, from the least specific, each as its level writes it.
    private static readonly (SettingLevel Level, string Value)[] KeysBelowADefault =
    [
        (
            SettingLevel.ProjectFile, /*lang=json,strict*/
            """{"timestamptz": "System.DateTimeOffset"}"""
        ),
        (
            SettingLevel.FolderFile, /*lang=json,strict*/
            """{"timestamptz": "long"}"""
        ),
        (SettingLevel.Header, "timestamptz = string"),
    ];

    // The place of each level, as SQLSRC250 to SQLSRC252 name it.
    public static TheoryData<SettingLevel, string> Places =>
        new()
        {
            { SettingLevel.ProjectFile, TheProjectFilePlace },
            { SettingLevel.FolderFile, TheFolderFilePlace },
            { SettingLevel.Header, TheHeaderPlace },
            { SettingLevel.Query, TheQueryPlace },
        };

    // The project's file maps timestamptz for the project.  The settings file of B's folder replaces it for B, C's
    // header for C, and the marker of C's second query for that query alone.  B's folder has a type of its own that
    // claims B, since a type claims the files of its own folder.
    [Fact]
    public void Run_EachLevel_ReplacesTheProjectFilesTypeForItsScopeAlone()
    {
        const string BSource = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class BQueries;
            }
            """;
        var a = new SqlFile("/app/Repo/A.sql", "-- name: GetA\nSELECT 1;\n");
        var b = new SqlFile("/app/Repo/B/B.sql", "-- name: GetB\nSELECT 1;\n");
        var bFolder = GeneratorHarness.SettingsFile(
            "/app/Repo/B/sqlsource.json", /*lang=json,strict*/
            """{"typeOverrides": {"timestamptz": "long"}}"""
        );
        var c = new SqlFile(
            "/app/Repo/C.sql",
            "-- type-override: timestamptz = string\n\n-- name: GetC1\nSELECT 1;\n\n-- name: GetC2\n"
                + "-- type-override: timestamptz = decimal\nSELECT 2;\n"
        );

        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source), new SourceFile("/app/Repo/B/BQueries.cs", BSource)],
            [
                a,
                Sidecars.Of(a, Entry(a, "GetA", Timestamptz)),
                b,
                Sidecars.Of(b, Entry(b, "GetB", Timestamptz)),
                bFolder,
                c,
                Sidecars.Of(c, Entry(c, "GetC1", Timestamptz), Entry(c, "GetC2", Timestamptz)),
            ],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres",
            settings: /*lang=json,strict*/
            """{"typeOverrides": {"timestamptz": "System.DateTimeOffset"}}"""
        );

        run.Diagnostics.ShouldBeEmpty();
        TextsOf(run)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["GetA"] = DateTimeOffset,
                    ["GetB"] = "long",
                    ["GetC1"] = "string",
                    ["GetC2"] = "decimal",
                },
                ignoreOrder: true
            );
    }

    // Each level's default hides the key of every level below it, and the base map answers.
    [Theory]
    [InlineData(
        SettingLevel.ProjectFile, /*lang=json,strict*/
        """{"timestamptz": "default"}"""
    )]
    [InlineData(
        SettingLevel.FolderFile, /*lang=json,strict*/
        """{"timestamptz": "default"}"""
    )]
    [InlineData(SettingLevel.Header, "timestamptz = default")]
    [InlineData(SettingLevel.Query, "timestamptz = default")]
    public void Run_DefaultAtALevel_GivesTheBaseMapsType(SettingLevel level, string value)
    {
        var settings = KeysBelowADefault
            .Where(key => key.Level < level)
            .Aggregate(new LevelSettings(), (settings, key) => settings.At(key.Level, Setting.TypeOverrides, key.Value))
            .At(level, Setting.TypeOverrides, value);
        var files = settings.Files(new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n"));
        var file = files[0];

        var run = Run(settings, files, Entry(file, "GetUser", Timestamptz));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([new PropertyType("global::System.DateTime", true, true, NoWarnings, "timestamptz", null)]);
    }

    [Fact]
    public void Run_QueryThatMapsOneKey_KeepsTheOthersOfTheProjectsFile()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- type-override: int4 = short\nSELECT 1;\n"
        );

        var run = Run(
            /*lang=json,strict*/
            """{"int4": "long", "timestamptz": "System.DateTimeOffset"}""",
            (file, [Entry(file, "GetUser", Int4, Timestamptz)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("short", true, true, NoWarnings, "int4", TheQueryPlace, IntBase),
                new PropertyType(
                    DateTimeOffset,
                    true,
                    true,
                    NoWarnings,
                    "timestamptz",
                    TheProjectFilePlace,
                    DateTimeBase
                ),
            ]);
    }

    // A test of the MSBuild path: the value is the property or the file's metadata.
    [Theory]
    [InlineData(true, ThePropertyPlace)]
    [InlineData(false, TheMetadataPlace)]
    public void Run_KeyTwiceInOneMSBuildValue_TheLastWins(bool property, string place)
    {
        const string Value = "int4=long int4=short";
        var file = OnMSBuild(new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser), property ? null : Value);

        var run = RunOnMSBuild(property ? Value : null, (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("short", true, true, NoWarnings, "int4", place, IntBase)]);
    }

    // An entry for an array is written with its element's key and [], or with its name in the catalog, which is read
    // as the same key for a type of pg_catalog, and for an extension's type in any schema when it is bare.  It wins
    // over the element's entry, which without it gives the array its element type.
    [Theory]
    [InlineData(
        "int4", /*lang=json,strict*/
        """{"int4": "long", "int4[]": "System.Collections.Generic.List<int>"}""",
        "int4[]"
    )]
    [InlineData(
        "int4", /*lang=json,strict*/
        """{"int4": "long", "_int4": "System.Collections.Generic.List<int>"}""",
        "int4[]"
    )]
    [InlineData(
        "citext", /*lang=json,strict*/
        """{"citext": "System.Uri", "citext[]": "System.Collections.Generic.List<int>"}""",
        "citext[]"
    )]
    [InlineData(
        "citext", /*lang=json,strict*/
        """{"citext": "System.Uri", "_citext": "System.Collections.Generic.List<int>"}""",
        "citext[]"
    )]
    public void Run_ArraysKey_WinsOverItsElements(string element, string overrides, string key)
    {
        var file = GeneratorHarness.UsersFile;
        var schema = element == "citext" ? "public" : "pg_catalog";
        var array = TestSidecars.Postgres(
            name: element + "[]",
            kind: SidecarValues.Kind.Array,
            schema: schema,
            internalName: "_" + element,
            element: TestSidecars.Postgres(name: element, schema: schema, internalName: element)
        );
        var (elementOverride, elementType) =
            element == "citext"
                ? ( /*lang=json,strict*/
                    """{"citext": "System.Uri"}""",
                    "global::System.Uri[]"
                )
                : ( /*lang=json,strict*/
                    """{"int4": "long"}""",
                    "long[]"
                );
        var database =
            element == "citext" ? new DatabaseType("string[]", false, false) : new DatabaseType("int[]", false, true);

        var ofTheArray = Run(overrides, (file, [Entry(file, "GetUser", array)]));
        var ofTheElement = Run(elementOverride, (file, [Entry(file, "GetUser", array)]));

        ofTheArray.Diagnostics.ShouldBeEmpty();
        ColumnsOf(ofTheArray, "GetUser")
            .ShouldBe([
                new PropertyType(
                    "global::System.Collections.Generic.List<int>",
                    false,
                    true,
                    NoWarnings,
                    key,
                    TheProjectFilePlace,
                    database
                ),
            ]);
        ColumnsOf(ofTheElement, "GetUser")
            .ShouldBe([new PropertyType(elementType, false, true, NoWarnings, element, TheProjectFilePlace, database)]);
    }

    // _int4 and int4[] are one key, so the query's entry replaces the project's file's whichever spelling each has, and
    // the query's default gives the array back to the base map.
    [Theory]
    [InlineData(
        "-- type-override: int4[] = System.Collections.Generic.List<long>\n",
        "global::System.Collections.Generic.List<long>"
    )]
    [InlineData("-- type-override: int4[] = default\n", "int[]")]
    public void Run_ArrayKeySpelledTwoWaysAtTwoLevels_IsOneKey(string marker, string expected)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n" + marker + "SELECT 1;\n");
        var array = TestSidecars.Postgres(
            name: "int4[]",
            kind: SidecarValues.Kind.Array,
            internalName: "_int4",
            element: Int4
        );

        var run = Run( /*lang=json,strict*/
            """{"_int4": "System.Collections.Generic.List<int>"}""",
            (file, [Entry(file, "GetUser", array)])
        );

        run.Diagnostics.ShouldBeEmpty();
        TextsOf(run)["GetUser"].ShouldBe(expected);
    }

    // The database names the array of public.user_status public._user_status, and an entry for that name maps
    // nothing at any level: the query is reported, with the key to write, and has no types.
    [Theory]
    [MemberData(nameof(Places))]
    public void Run_ArraysOwnKeyOutsideTheCatalog_IsSqlsrc261NamingTheKeyToWrite(SettingLevel level, string place)
    {
        var settings = new LevelSettings().At(
            level,
            Setting.TypeOverrides,
            EntryAt(level, "public._user_status", "System.Guid")
        );
        var files = settings.Files(new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n"));
        var file = files[0];

        var run = Run(settings, files, Entry(file, "GetUser", ArrayOf(EnumType("user_status"), "_user_status")));

        run.Diagnostics.ShouldBe([ArrayEntryError(file.Text!, "public._user_status", place, "public.user_status[]")]);
        TextsOf(run).ShouldBeEmpty();
    }

    // A default maps nothing either, so the entry is reported though it names no C# type.
    [Fact]
    public void Run_DefaultForAnArraysOwnKey_IsSqlsrc261()
    {
        const string Text = SqlTexts.GetUser;
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);

        var run = Run(
            /*lang=json,strict*/
            """{"public._user_status": "default"}""",
            (file, [Entry(file, "GetUser", ArrayOf(EnumType("user_status"), "_user_status"))])
        );

        run.Diagnostics.ShouldBe([
            ArrayEntryError(Text, "public._user_status", TheProjectFilePlace, "public.user_status[]"),
        ]);
    }

    // The entry that maps nothing is the user's to remove, though another entry maps the array.
    [Fact]
    public void Run_ArraysOwnKeyBesideItsElementsKey_IsStillSqlsrc261()
    {
        const string Text =
            "-- name: GetUser\n-- type-override: public.user_status[] = System.Collections.Generic.List<string>\n"
            + "SELECT 1;\n";
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);

        var run = Run(
            /*lang=json,strict*/
            """{"public._user_status": "System.Guid"}""",
            (file, [Entry(file, "GetUser", ArrayOf(EnumType("user_status"), "_user_status"))])
        );

        run.Diagnostics.ShouldBe([
            ArrayEntryError(Text, "public._user_status", TheProjectFilePlace, "public.user_status[]"),
        ]);
    }

    // The element's key with [] is the array's one key, so a query's default replaces the project's file's entry for
    // it.
    [Fact]
    public void Run_ElementsKeyWithBrackets_IsTheArraysOneKeyAtEveryLevel()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFirst\n-- type-override: public.user_status[] = default\nSELECT 1;\n\n"
                + "-- name: GetSecond\nSELECT 2;\n"
        );
        var array = ArrayOf(EnumType("user_status"), "_user_status");

        var run = Run(
            /*lang=json,strict*/
            """{"public.user_status[]": "System.Collections.Generic.List<string>"}""",
            (file, [Entry(file, "GetFirst", array), Entry(file, "GetSecond", array)])
        );

        run.Diagnostics.ShouldBeEmpty();
        TextsOf(run)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["GetFirst"] = "string[]",
                    ["GetSecond"] = "global::System.Collections.Generic.List<string>",
                },
                ignoreOrder: true
            );
    }

    // The qualified name of an extension's array maps nothing, as any array's outside pg_catalog does.
    [Fact]
    public void Run_ArraysOwnKeyOfAnExtensionsType_IsSqlsrc261NamingItsQualifiedKey()
    {
        const string Text = SqlTexts.GetUser;
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);
        var citext = TestSidecars.Postgres(name: "citext", schema: "public", internalName: "citext");

        var run = Run(
            /*lang=json,strict*/
            """{"public._citext": "System.Collections.Generic.List<int>"}""",
            (file, [Entry(file, "GetUser", ArrayOf(citext, "_citext"))])
        );

        run.Diagnostics.ShouldBe([ArrayEntryError(Text, "public._citext", TheProjectFilePlace, "public.citext[]")]);
    }

    // The parameter is named in the message as a column is.
    [Fact]
    public void Run_ArraysOwnKeyOnAParameter_NamesTheParameter()
    {
        const string Text = SqlTexts.GetUser;
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);
        var entry = TestSidecars.Entry(
            "GetUser",
            Sidecars.HashOf(file, "GetUser", SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            SidecarResultKind.None,
            parameters: [TestSidecars.Parameter(0, "statuses", ArrayOf(EnumType("user_status"), "_user_status"))]
        );

        var run = Run( /*lang=json,strict*/
            """{"public._user_status": "System.Guid"}""",
            (file, [entry])
        );

        run.Diagnostics.ShouldBe([
            ArrayEntryError(
                Text,
                "public._user_status",
                TheProjectFilePlace,
                "public.user_status[]",
                "parameter 'statuses'"
            ),
        ]);
    }

    // A composite named with a leading _ is no array, so its own key maps it, and its array, which the database names
    // public.__tag, gets its element's type.
    [Fact]
    public void Run_TypeNamedWithAnUnderscore_IsMappedByItsOwnKeyAndItsArrayByThatKeyWithBrackets()
    {
        var file = GeneratorHarness.UsersFile;
        var tag = Composite("public", "_tag");

        var run = Run( /*lang=json,strict*/
            """{"public._tag": "System.Uri"}""",
            (file, [Entry(file, "GetUser", tag, ArrayOf(tag, "__tag"))])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .Select(column => column.Text)
            .ShouldBe(["global::System.Uri", "global::System.Uri[]"]);
    }

    // The lookup reaches the array through the domain's base.
    [Fact]
    public void Run_DomainOverAnArrayWhoseOwnKeyHasAnEntry_IsSqlsrc261()
    {
        const string Text = SqlTexts.GetUser;
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);
        var statuses = TestSidecars.Postgres(
            name: "statuses",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "statuses",
            baseType: ArrayOf(EnumType("user_status"), "_user_status")
        );

        var run = Run( /*lang=json,strict*/
            """{"public._user_status": "System.Guid"}""",
            (file, [Entry(file, "GetUser", statuses)])
        );

        run.Diagnostics.ShouldBe([
            ArrayEntryError(Text, "public._user_status", TheProjectFilePlace, "public.user_status[]"),
        ]);
    }

    // The database's name of an array and its element's key with [] are two keys, so two lines of one scope that
    // give them two C# types are no conflict, and the first is reported where the array is used.
    [Fact]
    public void Run_CatalogsNameAndElementsKeyInOneScope_AreNoConflict()
    {
        const string Text =
            "-- name: GetUser\n-- type-override: public._user_status = System.Guid\n"
            + "-- type-override: public.user_status[] = System.Uri\nSELECT 1;\n";
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);

        var run = Run(null, (file, [Entry(file, "GetUser", ArrayOf(EnumType("user_status"), "_user_status"))]));

        run.Diagnostics.ShouldBe([
            ArrayEntryError(
                Text,
                "public._user_status",
                "a -- type-override: marker of the query",
                "public.user_status[]"
            ),
        ]);
    }

    // A domain's key wins over its base's, and the base's entry reaches every domain over it.
    [Fact]
    public void Run_DomainsKey_WinsOverItsBases()
    {
        var file = GeneratorHarness.UsersFile;
        var userId = TestSidecars.Postgres(
            name: "user_id",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "user_id",
            baseType: Int4
        );

        var own = Run( /*lang=json,strict*/
            """{"int4": "long", "public.user_id": "System.Guid"}""",
            (file, [Entry(file, "GetUser", userId, Int4)])
        );
        var ofTheBase = Run( /*lang=json,strict*/
            """{"int4": "long"}""",
            (file, [Entry(file, "GetUser", userId)])
        );

        own.Diagnostics.ShouldBeEmpty();
        ColumnsOf(own, "GetUser")
            .ShouldBe([
                new PropertyType(
                    "global::System.Guid",
                    true,
                    true,
                    NoWarnings,
                    "public.user_id",
                    TheProjectFilePlace,
                    IntBase
                ),
                new PropertyType("long", true, true, NoWarnings, "int4", TheProjectFilePlace, IntBase),
            ]);
        ColumnsOf(ofTheBase, "GetUser")
            .ShouldBe([new PropertyType("long", true, true, NoWarnings, "int4", TheProjectFilePlace, IntBase)]);
    }

    [Fact]
    public void Run_MappingOfASubtype_DoesNotReachItsRange()
    {
        var file = GeneratorHarness.UsersFile;
        var range = TestSidecars.Postgres(
            name: "int4range",
            kind: SidecarValues.Kind.Range,
            internalName: "int4range",
            subtype: Int4
        );

        var run = Run( /*lang=json,strict*/
            """{"int4": "long"}""",
            (file, [Entry(file, "GetUser", range)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("global::NpgsqlTypes.NpgsqlRange<int>", true, true, NoWarnings, "int4range", null),
            ]);
    }

    // The bare key of an extension's type matches it in any schema, and the qualified one in its schema alone.
    [Fact]
    public void Run_QualifiedKeyOfAnExtensionsType_WinsForItsSchemaAlone()
    {
        var file = GeneratorHarness.UsersFile;
        var inPublic = TestSidecars.Postgres(name: "citext", schema: "public", internalName: "citext");
        var inExt = TestSidecars.Postgres(name: "ext.citext", schema: "ext", internalName: "citext");

        var run = Run( /*lang=json,strict*/
            """{"citext": "System.Uri", "public.citext": "System.Guid"}""",
            (file, [Entry(file, "GetUser", inPublic, inExt)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType(
                    "global::System.Guid",
                    true,
                    true,
                    NoWarnings,
                    "public.citext",
                    TheProjectFilePlace,
                    StringBase
                ),
                new PropertyType(
                    "global::System.Uri",
                    false,
                    true,
                    NoWarnings,
                    "citext",
                    TheProjectFilePlace,
                    StringBase
                ),
            ]);
    }

    // Keys that differ only by case are two keys, each for its own type.
    [Fact]
    public void Run_KeysThatDifferOnlyByCase_EachMapsItsOwnType()
    {
        var file = GeneratorHarness.UsersFile;
        var upper = EnumType("Status");
        var lower = EnumType("status");

        var run = Run(
            /*lang=json,strict*/
            """{"public.Status": "System.Guid", "public.status": "System.Uri"}""",
            (file, [Entry(file, "GetUser", upper, lower)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType(
                    "global::System.Guid",
                    true,
                    true,
                    NoWarnings,
                    "public.Status",
                    TheProjectFilePlace,
                    StringBase
                ),
                new PropertyType(
                    "global::System.Uri",
                    false,
                    true,
                    NoWarnings,
                    "public.status",
                    TheProjectFilePlace,
                    StringBase
                ),
            ]);
    }

    // The query's default of the type's key takes the type out of the project's file's entry whose key differs from it
    // by case, so the domain gets its base's C# type.
    [Fact]
    public void Run_DefaultOfAQueryThatDiffersByCase_HidesTheProjectFilesEntry()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- type-override: public.status = default\nSELECT 1;\n"
        );
        var status = TestSidecars.Postgres(
            name: "status",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "status",
            baseType: Int4
        );

        var run = Run( /*lang=json,strict*/
            """{"public.Status": "System.Guid"}""",
            (file, [Entry(file, "GetUser", status)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("int", true, true, NoWarnings, "int4", null)]);
    }

    // The query's entry replaces the project's file's for the type, though the file's key is spelled exactly as the
    // type's and the query's differs from it by case.
    [Fact]
    public void Run_EntryOfAQueryThatDiffersByCase_ReplacesTheProjectFilesExactOne()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- type-override: public.User_Status = System.Uri\nSELECT 1;\n"
        );

        var run = Run( /*lang=json,strict*/
            """{"public.user_status": "System.Guid"}""",
            (file, [Entry(file, "GetUser", EnumType("user_status"))])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType(
                    "global::System.Uri",
                    false,
                    true,
                    NoWarnings,
                    "public.User_Status",
                    "a -- type-override: marker of the query",
                    StringBase
                ),
            ]);
    }

    // A quoted key is exact, so it does not map the type whose name differs from its text by case.
    [Fact]
    public void Run_QuotedKey_DoesNotMapATypeThatDiffersByCase()
    {
        const string Text = SqlTexts.GetUser;
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);
        var upper = Composite("public", "Status");
        var lower = Composite("public", "status");

        var run = Run( /*lang=json,strict*/
            """{"public.\"Status\"": "System.Guid"}""",
            (file, [Entry(file, "GetUser", upper, lower)])
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {NameOf(Text, "GetUser")}: The column 'c1' has the type 'status', "
                + "which SqlSource cannot give a C# type.  Map it with the type override, as "
                + "public.status=<a C# type>.",
        ]);
    }

    // The quotes let the key name a schema and a type that a plain key cannot, and the MSBuild value splits after
    // them.  A test of the MSBuild path: the value is the property or the file's metadata.
    [Theory]
    [InlineData(true, ThePropertyPlace)]
    [InlineData(false, TheMetadataPlace)]
    public void Run_QuotedSchemaAndNameInAnMSBuildValue_MapsTheTypeAndSplitsAfterThem(bool property, string place)
    {
        const string Value = QuotedKey + "=System.Guid int4=long";
        var file = OnMSBuild(
            new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n"),
            property ? null : Value
        );

        var run = RunOnMSBuild(
            property ? Value : null,
            (file, [Entry(file, "GetUser", Composite("my schema", "order-status"), Int4)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("global::System.Guid", true, true, NoWarnings, QuotedKey, place, NoBase),
                new PropertyType("long", true, true, NoWarnings, "int4", place, IntBase),
            ]);
    }

    // The quotes let a marker's key name a schema and a type that a plain key cannot.
    [Theory]
    [InlineData(SettingLevel.Header, TheHeaderPlace)]
    [InlineData(SettingLevel.Query, TheQueryPlace)]
    public void Run_QuotedSchemaAndNameInAMarker_MapsTheType(SettingLevel level, string place)
    {
        var settings = new LevelSettings().At(level, Setting.TypeOverrides, QuotedKey + " = System.Guid");
        var files = settings.Files(new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n"));

        var run = Run(settings, files, Entry(files[0], "GetUser", Composite("my schema", "order-status")));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([new PropertyType("global::System.Guid", true, true, NoWarnings, QuotedKey, place, NoBase)]);
    }

    // Two markers of one header, whose keys differ by case, are two keys and not a repeat of one.
    [Fact]
    public void Run_KeysThatDifferByCaseInOneScope_AreNoConflict()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.Status = System.Guid\n-- type-override: public.status = System.Uri\n\n"
                + SqlTexts.GetUser
        );

        var run = Run(null, (file, [Entry(file, "GetUser", EnumType("Status"), EnumType("status"))]));

        run.Diagnostics.ShouldBeEmpty();
        const string Place = "a -- type-override: marker in the file header";
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("global::System.Guid", true, true, NoWarnings, "public.Status", Place, StringBase),
                new PropertyType("global::System.Uri", false, true, NoWarnings, "public.status", Place, StringBase),
            ]);
    }

    // Both queries of int4 are reported, each at its name, and the query of text is typed.
    [Theory]
    [MemberData(nameof(Places))]
    public void Run_MappedTypeNotFound_IsSqlsrc250AtEachQueryThatUsesItNamingThePlace(SettingLevel level, string place)
    {
        var settings = new LevelSettings().At(level, Setting.TypeOverrides, EntryAt(level, "int4", "MyApp.Missing"));
        var files = settings.Files(
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- name: GetFirst\nSELECT 1;\n\n-- name: GetSecond\nSELECT 2;\n\n-- name: GetText\nSELECT 3;\n"
            ),
            "GetFirst",
            "GetSecond"
        );
        var file = files[0];
        var text = file.Text!;

        var run = Run(
            settings,
            files,
            Entry(file, "GetFirst", Int4),
            Entry(file, "GetSecond", Int4),
            Entry(file, "GetText", Catalog("text"))
        );

        string[] reported = ["GetFirst", "GetSecond"];
        run.Diagnostics.ShouldBe(
            reported.Select(name =>
                $"SQLSRC250 {NameOf(text, name)}: 'int4' is mapped to 'MyApp.Missing' for column "
                + $"'c0', in {place}, and no type of that name is found, or more than one that this project can see.  "
                + "Correct the name, with its namespace and type arguments, or for a name that two references have, "
                + "map to a type of this project."
            )
        );
        TextsOf(run).Keys.ShouldBe(["GetText"]);
    }

    [Theory]
    [InlineData(
        "MyApp.Outer.Closed",
        "SQLSRC251",
        "and this project cannot see that type.  Make it, and each type it is nested in, public, or internal when it "
            + "is this project's own, or give this project its assembly's internals with InternalsVisibleTo."
    )]
    [InlineData(
        "System.Math",
        "SQLSRC252",
        "and no property can have that type.  Choose another type, and for System.Nullable<T> name T, since a column "
            + "that can be null makes its property nullable."
    )]
    public void Run_MappedTypeThatCannotBeUsed_IsReportedAtTheQuery(string type, string id, string reason)
    {
        var file = GeneratorHarness.UsersFile;

        var run = Run($$"""{"int4": "{{type}}"}""", (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBe([
            $"{id} {GeneratorHarness.UsersPath}(1,10)-(1,17): 'int4' is mapped to '{type}' for column "
                + $"'c0', in {TheProjectFilePlace}, {reason}",
        ]);
    }

    // A parameter's type is reported as the parameter.
    [Fact]
    public void Run_MappedTypeNotFoundForAParameter_NamesTheParameter()
    {
        var file = GeneratorHarness.UsersFile;
        var entry = TestSidecars.Entry(
            "GetUser",
            Sidecars.HashOf(file, "GetUser", SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            SidecarResultKind.None,
            parameters: [TestSidecars.Parameter(0, "id", Int4)]
        );

        var run = Run( /*lang=json,strict*/
            """{"int4": "MyApp.Missing"}""",
            (file, [entry])
        );

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldStartWith(
                $"SQLSRC250 {GeneratorHarness.UsersPath}(1,10)-(1,17): 'int4' is mapped to "
                    + $"'MyApp.Missing' for parameter 'id', in {TheProjectFilePlace},"
            );
    }

    // A mapping that no query uses is never reported, so that a project can set its mappings before it has the
    // queries.
    [Fact]
    public void Run_BadMappingNoQueryUses_IsNotReported()
    {
        var file = GeneratorHarness.UsersFile;

        var run = Run( /*lang=json,strict*/
            """{"uuid": "MyApp.Missing", "int4": "System.Math"}""",
            (file, [Entry(file, "GetUser", Catalog("text"))])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("string", false, true, NoWarnings, "text", null)]);
    }

    [Fact]
    public void Run_MappedTypeMarkedObsolete_GivesTheObsoleteWarnings()
    {
        var file = GeneratorHarness.UsersFile;

        var run = Run( /*lang=json,strict*/
            """{"int4": "MyApp.OldThing"}""",
            (file, [Entry(file, "GetUser", Int4)])
        );

        run.Diagnostics.ShouldBeEmpty();
        var column = ColumnsOf(run, "GetUser").ShouldHaveSingleItem();
        column.WarningIds.ShouldBe(["CS0612", "CS0618"]);
        (column with { WarningIds = NoWarnings }).ShouldBe(
            new PropertyType("global::MyApp.OldThing", false, true, NoWarnings, "int4", TheProjectFilePlace, IntBase)
        );
    }

    // Claimed by an internal type, so that its model is internal and compiles.  In a public model the type is
    // SQLSRC251, which SharedModelTests holds.
    [Fact]
    public void Run_MappedTypeOfTheProjectThatIsInternal_IsNotPublic()
    {
        var file = GeneratorHarness.UsersFile;
        var source = Source.Replace(
            "public partial class Sample",
            "internal partial class Sample",
            StringComparison.Ordinal
        );

        var run = Run(
            source, /*lang=json,strict*/
            """{"int4": "MyApp.Inner"}""",
            (file, [Entry(file, "GetUser", Int4)])
        );

        run.ShouldBeClean();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("global::MyApp.Inner", true, false, NoWarnings, "int4", TheProjectFilePlace, IntBase),
            ]);
    }

    // The files with their sidecars, under the type overrides of the project's settings file, a JSON object of their
    // entries, or none.
    private static GeneratorRun Run(string? typeOverrides, params (SqlFile File, SidecarEntry[] Entries)[] files) =>
        Run(Source, typeOverrides, files);

    private static GeneratorRun Run(
        string source,
        string? typeOverrides,
        params (SqlFile File, SidecarEntry[] Entries)[] files
    ) =>
        ModelRuns.Run(
            source,
            [.. files.SelectMany(file => new[] { file.File, Sidecars.Of(file.File, file.Entries) })],
            typeOverrides is null ? null : "{\"typeOverrides\": " + typeOverrides + "}"
        );

    // The first of the files, a .sql file, with its sidecar of the entries, and the others, under the project's
    // settings of the level settings.
    private static GeneratorRun Run(LevelSettings settings, SqlFile[] files, params SidecarEntry[] entries) =>
        ModelRuns.Run(
            Source,
            [files[0], Sidecars.Of(files[0], entries), .. files.Skip(1)],
            settings: settings.ProjectSettings
        );

    // The files with their sidecars at Models under postgres, each setting the MSBuild way: the output, the dialect and
    // the type override, or none, are the project's properties, for a test of the MSBuild path.
    private static GeneratorRun RunOnMSBuild(string? property, params (SqlFile File, SidecarEntry[] Entries)[] files) =>
        GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [.. files.SelectMany(file => new[] { file.File, Sidecars.Of(file.File, file.Entries) })],
            references: [NpgsqlReferences.Npgsql10],
            properties: new Dictionary<string, string?>
            {
                ["SqlSourceOutput"] = "Models",
                ["SqlSourceDialect"] = "postgres",
                ["SqlSourceTypeOverrides"] = property,
            }
        );

    // The file with the type override as its metadata, or the file unchanged when it is null.
    private static SqlFile OnMSBuild(SqlFile file, string? metadata) =>
        metadata is null
            ? file
            : file with
            {
                Metadata = new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = metadata },
            };

    // The entry of the key as its level writes it: in a settings file a JSON object of the one entry, and in a marker
    // with spaces around =.
    private static string EntryAt(SettingLevel level, string key, string type) =>
        LevelSettings.IsFile(level) ? GeneratorHarness.Json(new JsonObject { [key] = type }) : key + " = " + type;

    // An entry of the query for its current hash, with a column of each type, named c0, c1 and so on.
    private static SidecarEntry Entry(SqlFile file, string name, params PostgresType[] columns) =>
        ModelRuns.Entry(file, name, [], [.. columns.Select((type, ordinal) => Column("c" + ordinal, type))]);

    private static PostgresType EnumType(string internalName) =>
        TestSidecars.Postgres(
            name: internalName,
            kind: SidecarValues.Kind.Enum,
            schema: "public",
            internalName: internalName,
            labels: ["new", "old"]
        );

    private static PostgresType Composite(string schema, string internalName) =>
        TestSidecars.Postgres(
            name: internalName,
            kind: SidecarValues.Kind.Composite,
            schema: schema,
            internalName: internalName
        );

    // SQLSRC261 at the query GetUser of Users, for column c0 unless the subject is given, the entry's key, the place
    // that wrote it and the key to write.
    private static string ArrayEntryError(
        string text,
        string key,
        string place,
        string write,
        string subject = "column 'c0'"
    ) =>
        $"SQLSRC261 {NameOf(text, "GetUser")}: The type override's entry for '{key}', in {place}, maps nothing: "
        + $"'{key}' is the database's own name of the array type of {subject}, and an entry names that array as "
        + $"its element's key with [].  Write the entry for '{write}' instead.";

    // The C# type of the one column of each query that has types, by the query's name.
    private static Dictionary<string, string> TextsOf(GeneratorRun run) =>
        StepOutputs
            .Of<FileTypes>(run, TrackingNames.FileTypes)
            .SelectMany(file => file.Queries)
            .ToDictionary(query => query.Name, query => query.Columns.ShouldNotBeNull().ShouldHaveSingleItem().Text);

    private static PropertyType[] ColumnsOf(GeneratorRun run, string queryName) =>
        [
            .. StepOutputs
                .Of<FileTypes>(run, TrackingNames.FileTypes)
                .SelectMany(file => file.Queries)
                .Single(query => query.Name == queryName)
                .Columns.ShouldNotBeNull(),
        ];

    // Where the name of the query is in Users, as the harness formats a location.
    private static string NameOf(string text, string queryName)
    {
        var line = Array.IndexOf(text.Split('\n'), "-- name: " + queryName) + 1;
        return $"{GeneratorHarness.UsersPath}({line},10)-({line},{10 + queryName.Length})";
    }
}
