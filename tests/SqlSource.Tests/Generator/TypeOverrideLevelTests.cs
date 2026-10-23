// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
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

    // The warning ids of a C# type whose use gives none.
    private static readonly EquatableArray<string> NoWarnings = EquatableArray<string>.Empty;

    private static readonly PostgresType Timestamptz = Catalog("timestamptz");

    // The place of each level, as SQLSRC250 to SQLSRC252 name it.
    public static TheoryData<string, string> Places =>
        new()
        {
            { "property", "the property SqlSourceTypeOverrides" },
            { "metadata", "the metadata SqlSourceTypeOverrides of the file" },
            { "header", "a -- type-override: marker in the file header" },
            { "query", "a -- type-override: marker of the query" },
        };

    // The property maps timestamptz for the project.  B's metadata replaces it for B, C's header for C, and the
    // marker of C's second query for that query alone.
    [Fact]
    public void Run_EachLevel_ReplacesThePropertysTypeForItsScopeAlone()
    {
        var a = new SqlFile("/app/Repo/A.sql", "-- name: GetA\nSELECT 1;\n");
        var b = new SqlFile(
            "/app/Repo/B.sql",
            "-- name: GetB\nSELECT 1;\n",
            Metadata: new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = "timestamptz=long" }
        );
        var c = new SqlFile(
            "/app/Repo/C.sql",
            "-- type-override: timestamptz = string\n\n-- name: GetC1\nSELECT 1;\n\n-- name: GetC2\n"
                + "-- type-override: timestamptz = decimal\nSELECT 2;\n"
        );

        var run = Run(
            "timestamptz=System.DateTimeOffset",
            (a, [Entry(a, "GetA", Timestamptz)]),
            (b, [Entry(b, "GetB", Timestamptz)]),
            (c, [Entry(c, "GetC1", Timestamptz), Entry(c, "GetC2", Timestamptz)])
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
    [InlineData("property")]
    [InlineData("metadata")]
    [InlineData("header")]
    [InlineData("query")]
    public void Run_DefaultAtALevel_GivesTheBaseMapsType(string level)
    {
        var header = level switch
        {
            "header" => "-- type-override: timestamptz = default\n\n",
            "query" => "-- type-override: timestamptz = string\n\n",
            _ => "",
        };
        var marker = level == "query" ? "-- type-override: timestamptz = default\n" : "";
        var metadata = level == "metadata" ? "timestamptz=default" : "timestamptz=long";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            header + "-- name: GetUser\n" + marker + "SELECT 1;\n",
            Metadata: level == "property"
                ? null
                : new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = metadata }
        );

        var run = Run(
            level == "property" ? "timestamptz=default" : "timestamptz=System.DateTimeOffset",
            (file, [Entry(file, "GetUser", Timestamptz)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([new PropertyType("global::System.DateTime", true, true, NoWarnings, "timestamptz", null)]);
    }

    [Fact]
    public void Run_QueryThatMapsOneKey_KeepsTheOthersOfTheProperty()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- type-override: int4 = short\nSELECT 1;\n"
        );

        var run = Run(
            "int4=long timestamptz=System.DateTimeOffset",
            (file, [Entry(file, "GetUser", Int4, Timestamptz)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("short", true, true, NoWarnings, "int4", "a -- type-override: marker of the query"),
                new PropertyType(DateTimeOffset, true, true, NoWarnings, "timestamptz", ThePropertyPlace),
            ]);
    }

    [Theory]
    [InlineData("property")]
    [InlineData("metadata")]
    public void Run_KeyTwiceInOneMSBuildValue_TheLastWins(string level)
    {
        const string Value = "int4=long int4=short";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            SqlTexts.GetUser,
            Metadata: level == "metadata" ? new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = Value } : null
        );

        var run = Run(level == "property" ? Value : null, (file, [Entry(file, "GetUser", Int4)]));
        var place = level == "property" ? ThePropertyPlace : "the metadata SqlSourceTypeOverrides of the file";

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("short", true, true, NoWarnings, "int4", place)]);
    }

    // An entry for an array is written with its element's key and [], or with its name in the catalog, which is read
    // as the same key for a type of pg_catalog, and for an extension's type in any schema when it is bare.  It wins
    // over the element's entry, which without it gives the array its element type.
    [Theory]
    [InlineData("int4", "int4=long int4[]=System.Collections.Generic.List<int>", "int4[]")]
    [InlineData("int4", "int4=long _int4=System.Collections.Generic.List<int>", "int4[]")]
    [InlineData("citext", "citext=System.Uri citext[]=System.Collections.Generic.List<int>", "citext[]")]
    [InlineData("citext", "citext=System.Uri _citext=System.Collections.Generic.List<int>", "citext[]")]
    public void Run_ArraysKey_WinsOverItsElements(string element, string overrides, string key)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var schema = element == "citext" ? "public" : "pg_catalog";
        var array = TestSidecars.Postgres(
            name: element + "[]",
            kind: SidecarValues.Kind.Array,
            schema: schema,
            internalName: "_" + element,
            element: TestSidecars.Postgres(name: element, schema: schema, internalName: element)
        );
        var (elementOverride, elementType) =
            element == "citext" ? ("citext=System.Uri", "global::System.Uri[]") : ("int4=long", "long[]");

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
                    ThePropertyPlace
                ),
            ]);
        ColumnsOf(ofTheElement, "GetUser")
            .ShouldBe([new PropertyType(elementType, false, true, NoWarnings, element, ThePropertyPlace)]);
    }

    // _int4 and int4[] are one key, so the query's entry replaces the property's whichever spelling each has, and
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

        var run = Run("_int4=System.Collections.Generic.List<int>", (file, [Entry(file, "GetUser", array)]));

        run.Diagnostics.ShouldBeEmpty();
        TextsOf(run)["GetUser"].ShouldBe(expected);
    }

    // The database names the array of public.user_status public._user_status, and an entry for that name maps
    // nothing at any level: the query is reported, with the key to write, and has no types.
    [Theory]
    [MemberData(nameof(Places))]
    public void Run_ArraysOwnKeyOutsideTheCatalog_IsSqlsrc261NamingTheKeyToWrite(string level, string place)
    {
        const string Value = "public._user_status=System.Guid";
        const string Marker = "-- type-override: public._user_status = System.Guid\n";
        var header = level == "header" ? Marker + "\n" : "";
        var marker = level == "query" ? Marker : "";
        var text = header + "-- name: GetUser\n" + marker + "SELECT 1;\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            text,
            Metadata: level == "metadata" ? new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = Value } : null
        );

        var run = Run(
            level == "property" ? Value : null,
            (file, [Entry(file, "GetUser", ArrayOf(EnumType("user_status"), "_user_status"))])
        );

        run.Diagnostics.ShouldBe([ArrayEntryError(text, "public._user_status", place, "public.user_status[]")]);
        TextsOf(run).ShouldBeEmpty();
    }

    // A default maps nothing either, so the entry is reported though it names no C# type.
    [Fact]
    public void Run_DefaultForAnArraysOwnKey_IsSqlsrc261()
    {
        const string Text = SqlTexts.GetUser;
        var file = new SqlFile(GeneratorHarness.UsersPath, Text);

        var run = Run(
            "public._user_status=default",
            (file, [Entry(file, "GetUser", ArrayOf(EnumType("user_status"), "_user_status"))])
        );

        run.Diagnostics.ShouldBe([
            ArrayEntryError(Text, "public._user_status", ThePropertyPlace, "public.user_status[]"),
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
            "public._user_status=System.Guid",
            (file, [Entry(file, "GetUser", ArrayOf(EnumType("user_status"), "_user_status"))])
        );

        run.Diagnostics.ShouldBe([
            ArrayEntryError(Text, "public._user_status", ThePropertyPlace, "public.user_status[]"),
        ]);
    }

    // The element's key with [] is the array's one key, so a query's default replaces the property's entry for it.
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
            "public.user_status[]=System.Collections.Generic.List<string>",
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
            "public._citext=System.Collections.Generic.List<int>",
            (file, [Entry(file, "GetUser", ArrayOf(citext, "_citext"))])
        );

        run.Diagnostics.ShouldBe([ArrayEntryError(Text, "public._citext", ThePropertyPlace, "public.citext[]")]);
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

        var run = Run("public._user_status=System.Guid", (file, [entry]));

        run.Diagnostics.ShouldBe([
            ArrayEntryError(
                Text,
                "public._user_status",
                ThePropertyPlace,
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
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var tag = Composite("public", "_tag");

        var run = Run("public._tag=System.Uri", (file, [Entry(file, "GetUser", tag, ArrayOf(tag, "__tag"))]));

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

        var run = Run("public._user_status=System.Guid", (file, [Entry(file, "GetUser", statuses)]));

        run.Diagnostics.ShouldBe([
            ArrayEntryError(Text, "public._user_status", ThePropertyPlace, "public.user_status[]"),
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
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var userId = TestSidecars.Postgres(
            name: "user_id",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "user_id",
            baseType: Int4
        );

        var own = Run("int4=long public.user_id=System.Guid", (file, [Entry(file, "GetUser", userId, Int4)]));
        var ofTheBase = Run("int4=long", (file, [Entry(file, "GetUser", userId)]));

        own.Diagnostics.ShouldBeEmpty();
        ColumnsOf(own, "GetUser")
            .ShouldBe([
                new PropertyType("global::System.Guid", true, true, NoWarnings, "public.user_id", ThePropertyPlace),
                new PropertyType("long", true, true, NoWarnings, "int4", ThePropertyPlace),
            ]);
        ColumnsOf(ofTheBase, "GetUser")
            .ShouldBe([new PropertyType("long", true, true, NoWarnings, "int4", ThePropertyPlace)]);
    }

    [Fact]
    public void Run_MappingOfASubtype_DoesNotReachItsRange()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var range = TestSidecars.Postgres(
            name: "int4range",
            kind: SidecarValues.Kind.Range,
            internalName: "int4range",
            subtype: Int4
        );

        var run = Run("int4=long", (file, [Entry(file, "GetUser", range)]));

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
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var inPublic = TestSidecars.Postgres(name: "citext", schema: "public", internalName: "citext");
        var inExt = TestSidecars.Postgres(name: "ext.citext", schema: "ext", internalName: "citext");

        var run = Run("citext=System.Uri public.citext=System.Guid", (file, [Entry(file, "GetUser", inPublic, inExt)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("global::System.Guid", true, true, NoWarnings, "public.citext", ThePropertyPlace),
                new PropertyType("global::System.Uri", false, true, NoWarnings, "citext", ThePropertyPlace),
            ]);
    }

    // Keys that differ only by case are two keys, each for its own type.
    [Fact]
    public void Run_KeysThatDifferOnlyByCase_EachMapsItsOwnType()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var upper = EnumType("Status");
        var lower = EnumType("status");

        var run = Run(
            "public.Status=System.Guid public.status=System.Uri",
            (file, [Entry(file, "GetUser", upper, lower)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("global::System.Guid", true, true, NoWarnings, "public.Status", ThePropertyPlace),
                new PropertyType("global::System.Uri", false, true, NoWarnings, "public.status", ThePropertyPlace),
            ]);
    }

    // The query's default of the type's key takes the type out of the property's entry whose key differs from it by
    // case, so the domain gets its base's C# type.
    [Fact]
    public void Run_DefaultOfAQueryThatDiffersByCase_HidesThePropertysEntry()
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

        var run = Run("public.Status=System.Guid", (file, [Entry(file, "GetUser", status)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("int", true, true, NoWarnings, "int4", null)]);
    }

    // The query's entry replaces the property's for the type, though the property's key is spelled exactly as the
    // type's and the query's differs from it by case.
    [Fact]
    public void Run_EntryOfAQueryThatDiffersByCase_ReplacesThePropertysExactOne()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- type-override: public.User_Status = System.Uri\nSELECT 1;\n"
        );

        var run = Run("public.user_status=System.Guid", (file, [Entry(file, "GetUser", EnumType("user_status"))]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType(
                    "global::System.Uri",
                    false,
                    true,
                    NoWarnings,
                    "public.User_Status",
                    "a -- type-override: marker of the query"
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

        var run = Run("public.\"Status\"=System.Guid", (file, [Entry(file, "GetUser", upper, lower)]));

        run.Diagnostics.ShouldBe([
            $"SQLSRC246 {NameOf(Text, "GetUser")}: The column 'c1' has the type 'status', "
                + "which SqlSource cannot give a C# type.  Map it with the type override, as "
                + "public.status=<a C# type>.",
        ]);
    }

    // The quotes let the key name a schema and a type that a plain key cannot, and the MSBuild value splits after
    // them.
    [Theory]
    [MemberData(nameof(Places))]
    public void Run_QuotedSchemaAndName_MapsTheType(string level, string place)
    {
        const string Key = "\"my schema\".\"order-status\"";
        var isMSBuild = level is "property" or "metadata";
        var value = isMSBuild ? Key + "=System.Guid int4=long" : Key + " = System.Guid";
        var header = level == "header" ? "-- type-override: " + value + "\n\n" : "";
        var marker = level == "query" ? "-- type-override: " + value + "\n" : "";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            header + "-- name: GetUser\n" + marker + "SELECT 1;\n",
            Metadata: level == "metadata" ? new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = value } : null
        );
        var status = Composite("my schema", "order-status");

        var run = Run(
            level == "property" ? value : null,
            (file, isMSBuild ? [Entry(file, "GetUser", status, Int4)] : [Entry(file, "GetUser", status)])
        );

        run.Diagnostics.ShouldBeEmpty();
        var guid = new PropertyType("global::System.Guid", true, true, NoWarnings, Key, place);
        ColumnsOf(run, "GetUser")
            .ShouldBe(isMSBuild ? [guid, new PropertyType("long", true, true, NoWarnings, "int4", place)] : [guid]);
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
                new PropertyType("global::System.Guid", true, true, NoWarnings, "public.Status", Place),
                new PropertyType("global::System.Uri", false, true, NoWarnings, "public.status", Place),
            ]);
    }

    // Both queries of int4 are reported, each at its name, and the query of text is typed.
    [Theory]
    [MemberData(nameof(Places))]
    public void Run_MappedTypeNotFound_IsSqlsrc250AtEachQueryThatUsesItNamingThePlace(string level, string place)
    {
        const string Marker = "-- type-override: int4 = MyApp.Missing\n";
        var header = level == "header" ? Marker + "\n" : "";
        var marker = level == "query" ? Marker : "";
        var text =
            header
            + "-- name: GetFirst\n"
            + marker
            + "SELECT 1;\n\n-- name: GetSecond\n"
            + marker
            + "SELECT 2;\n\n-- name: GetText\nSELECT 3;\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            text,
            Metadata: level == "metadata"
                ? new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = "int4=MyApp.Missing" }
                : null
        );

        var run = Run(
            level == "property" ? "int4=MyApp.Missing" : null,
            (
                file,
                [Entry(file, "GetFirst", Int4), Entry(file, "GetSecond", Int4), Entry(file, "GetText", Catalog("text"))]
            )
        );

        string[] reported = ["GetFirst", "GetSecond"];
        run.Diagnostics.ShouldBe(
            reported.Select(name =>
                $"SQLSRC250 {NameOf(text, name)}: The type override maps 'int4' to 'MyApp.Missing' for column "
                + $"'c0', in {place}, and no type of that name is found, or more than one reference has one that this "
                + "project can see.  Correct the name, with its namespace and type arguments, or for a name that two "
                + "references have, map to a type of this project."
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
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);

        var run = Run("int4=" + type, (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBe([
            $"{id} {GeneratorHarness.UsersPath}(1,10)-(1,17): The type override maps 'int4' to '{type}' for column "
                + $"'c0', in the property SqlSourceTypeOverrides, {reason}",
        ]);
    }

    // A parameter's type is reported as the parameter.
    [Fact]
    public void Run_MappedTypeNotFoundForAParameter_NamesTheParameter()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var entry = TestSidecars.Entry(
            "GetUser",
            Sidecars.HashOf(file, "GetUser", SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            SidecarResultKind.None,
            parameters: [TestSidecars.Parameter(0, "id", Int4)]
        );

        var run = Run("int4=MyApp.Missing", (file, [entry]));

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldStartWith(
                $"SQLSRC250 {GeneratorHarness.UsersPath}(1,10)-(1,17): The type override maps 'int4' to "
                    + "'MyApp.Missing' for parameter 'id', in the property SqlSourceTypeOverrides,"
            );
    }

    // A mapping that no query uses is never reported, so that a project can set its mappings before it has the
    // queries.
    [Fact]
    public void Run_BadMappingNoQueryUses_IsNotReported()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);

        var run = Run("uuid=MyApp.Missing int4=System.Math", (file, [Entry(file, "GetUser", Catalog("text"))]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("string", false, true, NoWarnings, "text", null)]);
    }

    [Fact]
    public void Run_MappedTypeMarkedObsolete_GivesTheObsoleteWarnings()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);

        var run = Run("int4=MyApp.OldThing", (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBeEmpty();
        var column = ColumnsOf(run, "GetUser").ShouldHaveSingleItem();
        column.WarningIds.ShouldBe(["CS0612", "CS0618"]);
        (column with { WarningIds = NoWarnings }).ShouldBe(
            new PropertyType("global::MyApp.OldThing", false, true, NoWarnings, "int4", ThePropertyPlace)
        );
    }

    // Claimed by an internal type, so that its model is internal and compiles.  In a public model the type is
    // SQLSRC251, which SharedModelTests holds.
    [Fact]
    public void Run_MappedTypeOfTheProjectThatIsInternal_IsNotPublic()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var source = Source.Replace(
            "public partial class Sample",
            "internal partial class Sample",
            StringComparison.Ordinal
        );

        var run = Run(source, "int4=MyApp.Inner", (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([new PropertyType("global::MyApp.Inner", true, false, NoWarnings, "int4", ThePropertyPlace)]);
    }

    // The files with their sidecars, under the property's type override, or none.
    private static GeneratorRun Run(string? property, params (SqlFile File, SidecarEntry[] Entries)[] files) =>
        Run(Source, property, files);

    private static GeneratorRun Run(
        string source,
        string? property,
        params (SqlFile File, SidecarEntry[] Entries)[] files
    ) =>
        ModelRuns.Run(
            source,
            [.. files.SelectMany(file => new[] { file.File, Sidecars.Of(file.File, file.Entries) })],
            new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = property }
        );

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

    // The array of the element, in its schema, under the name the database gives it.
    private static PostgresType ArrayOf(PostgresType element, string internalName) =>
        TestSidecars.Postgres(
            name: element.Name + "[]",
            kind: SidecarValues.Kind.Array,
            schema: element.Schema,
            internalName: internalName,
            element: element
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
