// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
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

    private static readonly PostgresType Timestamptz = Catalog("timestamptz");

    private static readonly PostgresType Int4 = Catalog("int4");

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
            "/app/Repo/Users.sql",
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
            .ShouldBe([new PropertyType("global::System.DateTime", true, true, false, "timestamptz", null)]);
    }

    [Fact]
    public void Run_QueryThatMapsOneKey_KeepsTheOthersOfTheProperty()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\n-- type-override: int4 = short\nSELECT 1;\n");

        var run = Run(
            "int4=long timestamptz=System.DateTimeOffset",
            (file, [Entry(file, "GetUser", Int4, Timestamptz)])
        );

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("short", true, true, false, "int4", "a -- type-override: marker of the query"),
                new PropertyType(DateTimeOffset, true, true, false, "timestamptz", ThePropertyPlace),
            ]);
    }

    [Theory]
    [InlineData("property")]
    [InlineData("metadata")]
    public void Run_KeyTwiceInOneMSBuildValue_TheLastWins(string level)
    {
        const string Value = "int4=long int4=short";
        var file = new SqlFile(
            "/app/Repo/Users.sql",
            "-- name: GetUser\nSELECT 1;\n",
            Metadata: level == "metadata" ? new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = Value } : null
        );

        var run = Run(level == "property" ? Value : null, (file, [Entry(file, "GetUser", Int4)]));
        var place = level == "property" ? ThePropertyPlace : "the metadata SqlSourceTypeOverrides of the file";

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("short", true, true, false, "int4", place)]);
    }

    // An array's own key is written as its element's with [], or as its name in the catalog, which is read as the
    // same key, and for an array of an extension's type in any schema, bare or qualified.  Without either, the
    // element's entry gives the array its element type.
    [Theory]
    [InlineData("int4", "int4=long int4[]=System.Collections.Generic.List<int>", "int4[]")]
    [InlineData("int4", "int4=long _int4=System.Collections.Generic.List<int>", "int4[]")]
    [InlineData("citext", "citext=System.Uri citext[]=System.Collections.Generic.List<int>", "citext[]")]
    [InlineData("citext", "citext=System.Uri _citext=System.Collections.Generic.List<int>", "citext[]")]
    [InlineData("citext", "citext=System.Uri public._citext=System.Collections.Generic.List<int>", "public._citext")]
    public void Run_ArraysOwnKey_WinsOverItsElements(string element, string overrides, string key)
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");
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

        var own = Run(overrides, (file, [Entry(file, "GetUser", array)]));
        var ofTheElement = Run(elementOverride, (file, [Entry(file, "GetUser", array)]));

        own.Diagnostics.ShouldBeEmpty();
        ColumnsOf(own, "GetUser")
            .ShouldBe([
                new PropertyType(
                    "global::System.Collections.Generic.List<int>",
                    false,
                    true,
                    false,
                    key,
                    ThePropertyPlace
                ),
            ]);
        ColumnsOf(ofTheElement, "GetUser")
            .ShouldBe([new PropertyType(elementType, false, true, false, element, ThePropertyPlace)]);
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
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\n" + marker + "SELECT 1;\n");
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

    // A domain's key wins over its base's, and the base's entry reaches every domain over it.
    [Fact]
    public void Run_DomainsKey_WinsOverItsBases()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");
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
                new PropertyType("global::System.Guid", true, true, false, "public.user_id", ThePropertyPlace),
                new PropertyType("long", true, true, false, "int4", ThePropertyPlace),
            ]);
        ColumnsOf(ofTheBase, "GetUser")
            .ShouldBe([new PropertyType("long", true, true, false, "int4", ThePropertyPlace)]);
    }

    [Fact]
    public void Run_MappingOfASubtype_DoesNotReachItsRange()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");
        var range = TestSidecars.Postgres(
            name: "int4range",
            kind: SidecarValues.Kind.Range,
            internalName: "int4range",
            subtype: Int4
        );

        var run = Run("int4=long", (file, [Entry(file, "GetUser", range)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([new PropertyType("global::NpgsqlTypes.NpgsqlRange<int>", true, true, false, "int4range", null)]);
    }

    // The bare key of an extension's type matches it in any schema, and the qualified one in its schema alone.
    [Fact]
    public void Run_QualifiedKeyOfAnExtensionsType_WinsForItsSchemaAlone()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");
        var inPublic = TestSidecars.Postgres(name: "citext", schema: "public", internalName: "citext");
        var inExt = TestSidecars.Postgres(name: "ext.citext", schema: "ext", internalName: "citext");

        var run = Run("citext=System.Uri public.citext=System.Guid", (file, [Entry(file, "GetUser", inPublic, inExt)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([
                new PropertyType("global::System.Guid", true, true, false, "public.citext", ThePropertyPlace),
                new PropertyType("global::System.Uri", false, true, false, "citext", ThePropertyPlace),
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
            "/app/Repo/Users.sql",
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
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");

        var run = Run("int4=" + type, (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBe([
            $"{id} /app/Repo/Users.sql(1,10)-(1,17): The type override maps 'int4' to '{type}' for column "
                + $"'c0', in the property SqlSourceTypeOverrides, {reason}",
        ]);
    }

    // A parameter's type is reported as the parameter.
    [Fact]
    public void Run_MappedTypeNotFoundForAParameter_NamesTheParameter()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");
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
                "SQLSRC250 /app/Repo/Users.sql(1,10)-(1,17): The type override maps 'int4' to 'MyApp.Missing' "
                    + "for parameter 'id', in the property SqlSourceTypeOverrides,"
            );
    }

    // A mapping that no query uses is never reported, so that a project can set its mappings before it has the
    // queries.
    [Fact]
    public void Run_BadMappingNoQueryUses_IsNotReported()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");

        var run = Run("uuid=MyApp.Missing int4=System.Math", (file, [Entry(file, "GetUser", Catalog("text"))]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser").ShouldBe([new PropertyType("string", false, true, false, "text", null)]);
    }

    [Fact]
    public void Run_MappedTypeMarkedObsolete_IsObsolete()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");

        var run = Run("int4=MyApp.OldThing", (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([new PropertyType("global::MyApp.OldThing", false, true, true, "int4", ThePropertyPlace)]);
    }

    [Fact]
    public void Run_MappedTypeOfTheProjectThatIsInternal_IsNotPublic()
    {
        var file = new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");

        var run = Run("int4=MyApp.Inner", (file, [Entry(file, "GetUser", Int4)]));

        run.Diagnostics.ShouldBeEmpty();
        ColumnsOf(run, "GetUser")
            .ShouldBe([new PropertyType("global::MyApp.Inner", true, false, false, "int4", ThePropertyPlace)]);
    }

    // The files with their sidecars, under the property's type override, or none.
    private static GeneratorRun Run(string? property, params (SqlFile File, SidecarEntry[] Entries)[] files) =>
        GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [.. files.SelectMany(file => new[] { file.File, Sidecars.Of(file.File, file.Entries) })],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres",
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = property }
        );

    // An entry of the query for its current hash, with a column of each type, named c0, c1 and so on.
    private static SidecarEntry Entry(SqlFile file, string name, params PostgresType[] columns) =>
        TestSidecars.Entry(
            name,
            Sidecars.HashOf(file, name, SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            SidecarResultKind.Rows,
            columns: [.. columns.Select((type, ordinal) => TestSidecars.Column(ordinal, "c" + ordinal, type))]
        );

    private static PostgresType Catalog(string internalName) =>
        TestSidecars.Postgres(name: internalName, internalName: internalName);

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
        return $"/app/Repo/Users.sql({line},10)-({line},{10 + queryName.Length})";
    }
}
