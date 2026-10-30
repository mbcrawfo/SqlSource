// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// A project's own class in a JSON column, json or jsonb on PostgreSQL and json on SQL Server, read and sent through
// System.Text.Json's serializer with its default options, whether an override, a column type or a marker chose it,
// unless the project has a converter of the class.  Each run is at CodeGen, with an entry written for each query's
// current hash, on the driver it names: Npgsql 8 and 10 give the same text.
public sealed class JsonSerializerMethodTests
{
    private const string Deserialize = "global::System.Text.Json.JsonSerializer.Deserialize<";

    private const string Serialize = "global::System.Text.Json.JsonSerializer.Serialize<";

    private const string OrderMetadata = "global::App.OrderMetadata";

    // System.Text.Json's class of generated conversions, which the serializer never needs.
    private const string JsonClassFile = "SqlSource-JsonConversions.g.cs";

    // The place a parameter's opt-in is written, as SQLSRC283 names it.
    private const string ParamPlace = "the -- param: marker of the parameter";

    // App.Sample, which claims the queries, with the classes the tests map: a class, a record and a value type.
    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public sealed class OrderMetadata
            {
                public string? Note { get; set; }
            }

            public sealed record Tag(string Name);

            public readonly record struct Point(int X, int Y);
        }
        """;

    // A query that reads meta, not null, and maybe_meta, nullable, and finds by parameters of the same names, with the
    // markers after its name.
    private const string SaveMeta =
        "SELECT meta, maybe_meta FROM orders WHERE meta = @meta AND maybe_meta = @maybe_meta;\n";

    // The column types that give the class to the columns meta and maybe_meta of orders.
    private const string ColumnTypes = /*lang=json,strict*/
        """{"columnTypes": {"orders.meta": "App.OrderMetadata", "orders.maybe_meta": "App.OrderMetadata"}}""";

    private static readonly PostgresType Jsonb = Catalog("jsonb") with { BindName = "jsonb" };

    private static readonly PostgresType JsonbArray = ArrayOf(Jsonb, "_jsonb") with { BindName = "jsonb[]" };

    private static readonly SqlServerType SqlServerJson = TestSidecars.SqlServer("json", -1, 0);

    private static readonly SqlServerType NVarCharMax = TestSidecars.SqlServer("nvarchar(max)", -1, 0);

    private static readonly PostgresType Text = Catalog("text") with { BindName = "text" };

    private static readonly PostgresType TextArray = ArrayOf(Text, "_text") with { BindName = "text[]" };

    // The columns and parameters of SaveMeta.
    private static readonly string[] MetaNames = ["meta", "maybe_meta"];

    // The serializer reads and sends the class, whichever place chose it, and the method throws on a JSON null for the
    // column that is not null; no class of conversions is emitted.
    [Theory]
    [InlineData("Npgsql 8", "marker")]
    [InlineData("Npgsql 8", "column type")]
    [InlineData("Npgsql 8", "override")]
    [InlineData("Npgsql 10", "marker")]
    [InlineData("Npgsql 10", "column type")]
    [InlineData("Npgsql 10", "override")]
    [InlineData("SqlClient 7", "marker")]
    [InlineData("SqlClient 7", "column type")]
    [InlineData("SqlClient 7", "override")]
    public void Run_ClassOnJsonb_ReadsAndWritesThroughTheSerializer(string driver, string place)
    {
        var isSqlServer = IsSqlServer(driver);
        var (text, settings) = place switch
        {
            "marker" => ("-- name: SaveMeta\n" + Markers("App.OrderMetadata") + SaveMeta, null),
            "column type" => ("-- name: SaveMeta\n" + ParamMarkers("App.OrderMetadata") + SaveMeta, ColumnTypes),
            _ => (
                "-- type-override: "
                    + (isSqlServer ? "json" : "jsonb")
                    + "=App.OrderMetadata\n\n-- name: SaveMeta\n"
                    + SaveMeta,
                null
            ),
        };

        var run = RunMeta(driver, text, settings: settings);

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(JsonClassFile);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "("
                + Deserialize
                + OrderMetadata
                + ">(reader.GetFieldValue<string>(0)) ?? throw new global::System.InvalidCastException("
                + "\"Column 'meta' holds a JSON null.\"), "
        );
        methods.ShouldContain(
            "reader.IsDBNull(1) ? default("
                + OrderMetadata
                + "?) : "
                + Deserialize
                + OrderMetadata
                + ">(reader.GetFieldValue<string>(1)))"
        );
        methods.ShouldNotContain("Column 'maybe_meta' holds a JSON null.");
        if (isSqlServer)
        {
            methods.ShouldContain("parameter.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n");
            methods.ShouldContain("parameter.Size = -1;\n");
            methods.ShouldContain(
                "parameter.Value = parameters.Meta is { } value ? (object)"
                    + Serialize
                    + OrderMetadata
                    + ">(value) : global::System.DBNull.Value;\n"
            );
        }
        else
        {
            methods.ShouldContain("var parameter = new global::Npgsql.NpgsqlParameter<string>();\n");
            methods.ShouldContain("parameter.DataTypeName = \"pg_catalog.jsonb\";\n");
            methods.ShouldContain(
                "parameter.TypedValue = parameters.Meta is { } value ? "
                    + Serialize
                    + OrderMetadata
                    + ">(value) : null;\n"
            );
        }
    }

    // jsonb[] as an array of the class is converted element by element: a JSON null element of a not-null element
    // throws, naming the column, and a NULL element stays null; a nullable element gives null for both.
    [Theory]
    [InlineData(
        "Npgsql 8",
        "App.OrderMetadata[]",
        "global::System.Array.ConvertAll<string, global::App.OrderMetadata>(reader.GetFieldValue<string[]>(0), static "
            + "element => element is { } item ? ("
            + Deserialize
            + OrderMetadata
            + ">(item) ?? throw new global::System.InvalidCastException(\"Column 'meta' holds a JSON null.\")) : null!)"
    )]
    [InlineData(
        "Npgsql 10",
        "App.OrderMetadata?[]",
        "global::System.Array.ConvertAll<string, global::App.OrderMetadata?>(reader.GetFieldValue<string[]>(0), "
            + "static element => element is { } item ? "
            + Deserialize
            + OrderMetadata
            + ">(item) : null)"
    )]
    [InlineData(
        "Npgsql 10",
        "App.OrderMetadata[]",
        "global::System.Array.ConvertAll<string, global::App.OrderMetadata>(reader.GetFieldValue<string[]>(0), static "
            + "element => element is { } item ? ("
            + Deserialize
            + OrderMetadata
            + ">(item) ?? throw new global::System.InvalidCastException(\"Column 'meta' holds a JSON null.\")) : null!)"
    )]
    public void Run_ClassArrayOnJsonbArray_IsConvertedByElement(string driver, string type, string read)
    {
        var run = RunMeta(driver, "-- name: SaveMeta\n" + Markers(type) + SaveMeta, type: JsonbArray);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(read);
        methods.ShouldContain("var parameter = new global::Npgsql.NpgsqlParameter<string?[]>();\n");
        methods.ShouldContain(
            "parameter.TypedValue = parameters.Meta is { } value ? global::System.Array.ConvertAll<"
                + OrderMetadata
                + "?, string?>(value, static element => element is { } item ? "
                + Serialize
                + OrderMetadata
                + ">(item) : null) : null;\n"
        );
    }

    // An array of a value type is converted by element too: a NULL element of a not-null element throws, and the
    // serializer's read of a value type is never null.
    [Fact]
    public void Run_ValueTypeArrayOnJsonbArray_IsConvertedByElement()
    {
        var run = RunMeta("Npgsql 10", "-- name: SaveMeta\n" + Markers("App.Point[]") + SaveMeta, type: JsonbArray);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "global::System.Array.ConvertAll<string, global::App.Point>(reader.GetFieldValue<string[]>(0), static "
                + "element => element is { } item ? "
                + Deserialize
                + "global::App.Point>(item) : throw new global::System.InvalidCastException("
                + "\"Column 'meta' holds a null element.\"))"
        );
        methods.ShouldContain(
            "parameter.TypedValue = parameters.Meta is { } value ? global::System.Array.ConvertAll<global::App.Point, "
                + "string>(value, static element => "
                + Serialize
                + "global::App.Point>(element)) : null;\n"
        );
        methods.ShouldContain("var parameter = new global::Npgsql.NpgsqlParameter<string[]>();\n");
        methods.ShouldNotContain("holds a JSON null");
    }

    // A collection, a record or an array given to a plain JSON column is a class, serialized whole.
    [Theory]
    [InlineData(
        "Npgsql 10",
        "System.Collections.Generic.List<App.Tag>",
        "global::System.Collections.Generic.List<global::App.Tag>"
    )]
    [InlineData("Npgsql 10", "App.OrderMetadata[]", "global::App.OrderMetadata[]")]
    [InlineData("Npgsql 10", "App.Tag", "global::App.Tag")]
    [InlineData(
        "SqlClient 7",
        "System.Collections.Generic.List<App.Tag>",
        "global::System.Collections.Generic.List<global::App.Tag>"
    )]
    [InlineData("SqlClient 7", "App.OrderMetadata[]", "global::App.OrderMetadata[]")]
    public void Run_ListOnJsonb_IsSerializedWhole(string driver, string type, string written)
    {
        var run = RunMeta(driver, "-- name: SaveMeta\n" + Markers(type) + SaveMeta);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "("
                + Deserialize
                + written
                + ">(reader.GetFieldValue<string>(0)) ?? throw new global::System.InvalidCastException("
                + "\"Column 'meta' holds a JSON null.\"), "
        );
        methods.ShouldContain(Serialize + written + ">(value)");
        methods.ShouldNotContain("ConvertAll");
    }

    // The serializer's read of a value type is never null, so it has no check; a NULL of the nullable one is null.
    [Theory]
    [InlineData("Npgsql 8")]
    [InlineData("Npgsql 10")]
    [InlineData("SqlClient 7")]
    public void Run_ValueTypeClassOnJsonb_HasNoJsonNullCheck(string driver)
    {
        var run = RunMeta(driver, "-- name: SaveMeta\n" + Markers("App.Point") + SaveMeta);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(" + Deserialize + "global::App.Point>(reader.GetFieldValue<string>(0)), ");
        methods.ShouldContain(
            "reader.IsDBNull(1) ? default(global::App.Point?) : "
                + Deserialize
                + "global::App.Point>(reader.GetFieldValue<string>(1)))"
        );
        methods.ShouldNotContain("holds a JSON null");
        methods.ShouldContain(
            IsSqlServer(driver)
                ? "parameter.Value = " + Serialize + "global::App.Point>(parameters.Meta);\n"
                : "parameter.TypedValue = " + Serialize + "global::App.Point>(parameters.Meta);\n"
        );
        methods.ShouldContain("parameters.MaybeMeta is { } value ? ");
    }

    // A domain over jsonb, and an array of one, hold JSON as jsonb does; so does an alias type of SQL Server over json.
    [Theory]
    [InlineData("Npgsql 10", false)]
    [InlineData("Npgsql 10", true)]
    [InlineData("SqlClient 7", false)]
    public void Run_DomainOverJsonb_IsSerialized(string driver, bool isArray)
    {
        var domain = TestSidecars.Postgres(
            name: "order_doc",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "order_doc",
            baseType: Jsonb,
            bindName: "order_doc"
        );
        SidecarType type = isArray ? ArrayOf(domain, "_order_doc") with { BindName = "order_doc[]" } : domain;
        if (IsSqlServer(driver))
        {
            type = TestSidecars.SqlServer("json", -1, 0, userType: new SqlServerUserType("dbo", "OrderDoc", null));
        }

        var run = RunMeta(
            driver,
            "-- name: SaveMeta\n" + Markers(isArray ? "App.OrderMetadata[]" : "App.OrderMetadata") + SaveMeta,
            type: type
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            isArray
                ? "global::System.Array.ConvertAll<string, global::App.OrderMetadata>(reader.GetFieldValue<string[]>"
                    + "(0), static element => element is { } item ? ("
                    + Deserialize
                    + OrderMetadata
                    + ">(item) ?? throw"
                : "(" + Deserialize + OrderMetadata + ">(reader.GetFieldValue<string>(0)) ?? throw"
        );
        methods.ShouldContain(Serialize + OrderMetadata + ">(");
    }

    // A converter of the project between string and the class replaces the serializer, both ways.
    [Theory]
    [InlineData("Npgsql 10")]
    [InlineData("SqlClient 7")]
    public void Run_ProjectConverterOfTheClass_ReplacesTheSerializer(string driver)
    {
        var run = RunMeta(
            driver,
            "-- name: SaveMeta\n" + Markers("App.OrderMetadata") + SaveMeta,
            ConvertersSource(
                "[SqlSourceConverter] public static OrderMetadata FromJson(string value) => new() { Note = value };\n"
                    + "[SqlSourceConverter] public static string ToJson(OrderMetadata value) => value.Note ?? \"\";"
            )
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::App.OrderMetadata.FromJson(reader.GetFieldValue<string>(0)), ");
        methods.ShouldContain("global::App.OrderMetadata.ToJson(value)");
        methods.ShouldNotContain("JsonSerializer");
        methods.ShouldNotContain("holds a JSON null");
    }

    // A converter of the one direction reads the column by it; the parameter, which needs the other, is SQLSRC280,
    // and the serializer never fills in for the missing direction.
    [Theory]
    [InlineData("Npgsql 10")]
    [InlineData("SqlClient 7")]
    public void Run_ConverterOfOneDirection_ReadsByItAndIsSQLSRC280ForTheParameter(string driver)
    {
        var source = ConvertersSource(
            "[SqlSourceConverter] public static OrderMetadata FromJson(string value) => new() { Note = value };"
        );
        var read = RunMeta(driver, "-- name: SaveMeta\n" + ColumnMarkers("App.OrderMetadata") + SaveMeta, source);

        read.ShouldBeClean();
        var methods = read.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::App.OrderMetadata.FromJson(reader.GetFieldValue<string>(0)), ");
        methods.ShouldNotContain("JsonSerializer");

        var write = RunMeta(driver, "-- name: SaveMeta\n" + ParamMarkers("App.OrderMetadata") + SaveMeta, source);

        var problems = write.Problems();
        problems.Count.ShouldBe(2);
        problems.ShouldContain(problem =>
            problem.EndsWith(
                ": The parameter 'meta' has type 'App.OrderMetadata', which has a converter from 'string' but none to "
                    + "it, which a parameter needs.  Add a static method marked [SqlSourceConverter] that takes "
                    + "'App.OrderMetadata' and returns 'string'.",
                System.StringComparison.Ordinal
            )
        );
        problems.ShouldAllBe(problem => problem.StartsWith("SQLSRC280 ", System.StringComparison.Ordinal));
        write.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // A class whose converters are of another type keeps 8.2's rule: SQLSRC279, and no serializer.
    [Theory]
    [InlineData("Npgsql 10", "jsonb")]
    [InlineData("SqlClient 7", "json")]
    public void Run_ClassWithConvertersOfAnotherType_IsSQLSRC279(string driver, string key)
    {
        var run = RunMeta(
            driver,
            "-- name: SaveMeta\n" + ColumnMarkers("App.OrderMetadata") + SaveMeta,
            ConvertersSource(
                "[SqlSourceConverter] public static OrderMetadata FromBytes(byte[] value) => new();\n"
                    + "[SqlSourceConverter] public static byte[] ToBytes(OrderMetadata value) => [];"
            )
        );

        var problems = run.Problems();
        problems.Count.ShouldBe(2);
        problems.ShouldContain(problem =>
            problem.EndsWith(
                ": The column 'meta' has type 'App.OrderMetadata', whose converters convert from and to 'byte[]', and "
                    + "not from and to 'string', the type the database type '"
                    + key
                    + "' is read as.  Add a converter between 'App.OrderMetadata' and 'string'.",
                System.StringComparison.Ordinal
            )
        );
        problems.ShouldAllBe(problem => problem.StartsWith("SQLSRC279 ", System.StringComparison.Ordinal));
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // A class on a string column that does not hold JSON, without the opt-in or a converter, is read as 8.2 reads it,
    // by the driver, with no diagnostic.
    [Theory]
    [InlineData("Npgsql 10")]
    [InlineData("SqlClient 7")]
    public void Run_ClassOnText_KeepsTheDriversRead(string driver)
    {
        SidecarType type = IsSqlServer(driver)
            ? TestSidecars.SqlServer("nvarchar(50)", 100, 0)
            : Catalog("text") with
            {
                BindName = "text",
            };

        var run = RunMeta(driver, "-- name: SaveMeta\n" + Markers("App.OrderMetadata") + SaveMeta, type: type);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("reader.GetFieldValue<" + OrderMetadata + ">(0)");
        methods.ShouldNotContain("JsonSerializer");
    }

    // A type of System.Text.Json and a type the base map gives are never a class, so the serializer never takes them.
    [Theory]
    [InlineData("string")]
    [InlineData("System.Text.Json.JsonElement")]
    [InlineData("System.Text.Json.Nodes.JsonNode")]
    [InlineData("long")]
    [InlineData("string[]")]
    public void Run_JsonTypesAndBaseTypes_AreNeverSerialized(string type)
    {
        var run = RunMeta(
            "Npgsql 10",
            "-- name: SaveMeta\n" + Markers(type) + SaveMeta,
            type: type.EndsWith("[]", System.StringComparison.Ordinal) ? JsonbArray : Jsonb
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldNotContain("JsonSerializer");
    }

    // Every shape of the serializer, read and written, nullable and not, whole and by element, compiles with no error
    // and no warning in what is generated, against each driver, with the reference assemblies of .NET 8 and with the
    // runtime's, of .NET 10.
    [Theory]
    [InlineData("SqlClient 6.1", false)]
    [InlineData("SqlClient 6.1", true)]
    [InlineData("SqlClient 7", false)]
    [InlineData("SqlClient 7", true)]
    [InlineData("Npgsql 8", false)]
    [InlineData("Npgsql 8", true)]
    [InlineData("Npgsql 10", false)]
    [InlineData("Npgsql 10", true)]
    public void Run_SerializedMethods_Compile(string driver, bool net8)
    {
        var isSqlServer = IsSqlServer(driver);
        string[] whole =
        [
            "App.OrderMetadata",
            "App.Tag",
            "App.Point",
            "System.Collections.Generic.List<App.Tag>",
            "App.OrderMetadata[]",
        ];
        string[] byElement = ["App.OrderMetadata[]", "App.OrderMetadata?[]", "App.Point[]", "App.Point?[]"];
        var types = whole.Select(type => (type, false)).ToList();
        if (!isSqlServer)
        {
            types.AddRange(byElement.Select(type => (type, true)));
        }

        var markers = new System.Text.StringBuilder();
        var parameters = new System.Collections.Generic.List<SidecarParameter>();
        var columns = new System.Collections.Generic.List<SidecarColumn>();
        var names = new System.Collections.Generic.List<string>();
        for (var index = 0; index < types.Count; index++)
        {
            var (type, isArray) = types[index];
            SidecarType database = isArray ? JsonbArray : Jsonb;
            if (isSqlServer)
            {
                database = SqlServerJson;
            }
            foreach (var name in new[] { "v" + index, "maybe_v" + index })
            {
                names.Add(name);
                _ = markers.Append("-- column: ").Append(name).Append(" -> ").Append(type).Append('\n');
                _ = markers.Append("-- param: @").Append(name).Append(" -> ").Append(type).Append('\n');
                var nullable = name.StartsWith("maybe_", System.StringComparison.Ordinal);
                parameters.Add(TestSidecars.Parameter(0, name, database, nullable));
                columns.Add(TestSidecars.Column(0, name, database, nullable: nullable));
            }
        }

        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: Everything\n"
                + markers
                + "SELECT "
                + string.Join(", ", names)
                + " FROM everything WHERE "
                + string.Join(" AND ", names.Select(name => name + " = @" + name))
                + ";\n"
        );
        var entry = isSqlServer
            ? MethodRuns.SqlServerEntry(file, "Everything", [.. parameters], [.. columns])
            : Entry(file, "Everything", [.. parameters], [.. columns]);
        var driverReference = driver switch
        {
            "SqlClient 6.1" => SqlClientReferences.SqlClient61,
            "SqlClient 7" => SqlClientReferences.SqlClient7,
            "Npgsql 8" => NpgsqlReferences.Npgsql8,
            _ => NpgsqlReferences.Npgsql10,
        };

        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, entry)],
            supportedFramework: !net8,
            references: net8 ? [.. LibraryReferences.Net8Framework, driverReference] : [driverReference],
            dialect: isSqlServer ? "mssql" : "postgres"
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("holds a JSON null.");
        methods.ShouldContain(Deserialize);
        methods.ShouldContain(Serialize);
        run.Sources.Keys.ShouldNotContain(JsonClassFile);
    }

    // The JSON opt-in on nvarchar(max) reads and sends the class through the serializer, whichever of the four places
    // says it, and the parameter is bound as the column's type, NVarChar at -1.  The -- param: marker gives the
    // parameters; the place gives the columns, or for the -- param: marker alone none, and they stay strings.
    [Theory]
    [InlineData("param marker")]
    [InlineData("query marker")]
    [InlineData("header")]
    [InlineData("settings file")]
    public void Run_OptInOnNvarcharMax_ReadsAndWritesThroughTheSerializer(string place)
    {
        var run = RunOptIn("SqlClient 7", place, NVarCharMax);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        ShouldReadThroughTheSerializer(methods, place);
        methods.ShouldContain("parameter.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n");
        methods.ShouldContain("parameter.Size = -1;\n");
        methods.ShouldContain(
            "parameter.Value = parameters.Meta is { } value ? (object)"
                + Serialize
                + OrderMetadata
                + ">(value) : global::System.DBNull.Value;\n"
        );
        methods.ShouldNotContain("SqlDbType.Json");
    }

    // The same on text, bound as text.
    [Theory]
    [InlineData("param marker")]
    [InlineData("query marker")]
    [InlineData("header")]
    [InlineData("settings file")]
    public void Run_OptInOnText_ReadsAndWritesThroughTheSerializer(string place)
    {
        var run = RunOptIn("Npgsql 10", place, Text);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        ShouldReadThroughTheSerializer(methods, place);
        methods.ShouldContain("var parameter = new global::Npgsql.NpgsqlParameter<string>();\n");
        methods.ShouldContain("parameter.DataTypeName = \"pg_catalog.text\";\n");
        methods.ShouldContain(
            "parameter.TypedValue = parameters.Meta is { } value ? " + Serialize + OrderMetadata + ">(value) : null;\n"
        );
        methods.ShouldNotContain("jsonb");
    }

    // On a type that holds JSON the opt-in is allowed and changes nothing: the generated text is the same with and
    // without the word, on each of the four places.
    [Theory]
    [InlineData("Npgsql 10", "param marker")]
    [InlineData("Npgsql 10", "query marker")]
    [InlineData("Npgsql 10", "header")]
    [InlineData("Npgsql 10", "settings file")]
    [InlineData("SqlClient 7", "query marker")]
    [InlineData("SqlClient 7", "settings file")]
    public void Run_OptInOnJsonb_ChangesNothing(string driver, string place)
    {
        SidecarType database = IsSqlServer(driver) ? SqlServerJson : Jsonb;

        var withWord = RunOptIn(driver, place, database);
        var without = RunOptIn(driver, place, database, json: false);

        withWord.ShouldBeClean();
        without.ShouldBeClean();
        withWord.Sources.Keys.ShouldBe(without.Sources.Keys, ignoreOrder: true);
        foreach (var (name, text) in without.Sources)
        {
            withWord.Sources[name].ShouldBe(text);
        }

        withWord.Sources[MethodRuns.MethodsFile].ShouldContain(Serialize + OrderMetadata + ">(");
    }

    // The opt-in on a type that the base map does not read as a string, and that holds no JSON, is SQLSRC283 at the
    // query, once for each column and parameter that says it, and the query gets no method.
    [Theory]
    [InlineData("Npgsql 10", "param marker", ParamPlace)]
    [InlineData("Npgsql 10", "query marker", "a -- column: marker of the query")]
    [InlineData("Npgsql 10", "header", "a -- column: marker in the file header")]
    [InlineData("Npgsql 10", "settings file", "the columnTypes of the settings file /sqlsource.json")]
    [InlineData("SqlClient 7", "query marker", "a -- column: marker of the query")]
    public void Run_OptInOnInt_IsSQLSRC283(string driver, string place, string written)
    {
        var isSqlServer = IsSqlServer(driver);
        var database = isSqlServer
            ? TestSidecars.SqlServer()
            : (SidecarType)(Catalog("int4") with { BindName = "int4" });

        var run = RunOptIn(driver, place, database);

        string[] subjects = place == "param marker" ? ["parameter"] : ["column", "parameter"];
        ShouldBeSQLSRC283(
            run,
            subjects,
            "App.OrderMetadata",
            written,
            "the database type '" + (isSqlServer ? "int" : "int4") + "' is read as 'int', not as a string",
            line: place == "header" ? 4 : 1
        );
    }

    // A type of System.Text.Json is read without the serializer, so the opt-in with it is SQLSRC283, on a string type
    // and on one that holds JSON.
    [Theory]
    [InlineData("SqlClient 7", "System.Text.Json.JsonElement")]
    [InlineData("Npgsql 10", "System.Text.Json.Nodes.JsonNode")]
    public void Run_OptInWithJsonElement_IsSQLSRC283(string driver, string type)
    {
        var isSqlServer = IsSqlServer(driver);

        var run = RunOptIn(driver, "query marker", isSqlServer ? NVarCharMax : Jsonb, type: type);

        ShouldBeSQLSRC283(
            run,
            ["column", "parameter"],
            type,
            "a -- column: marker of the query",
            "'" + type + "' is a type of System.Text.Json, which is read without the serializer"
        );
    }

    // A type the base map gives, as string, is never serialized, so the opt-in with it is SQLSRC283.
    [Theory]
    [InlineData("SqlClient 7", "string")]
    [InlineData("Npgsql 10", "string")]
    [InlineData("Npgsql 10", "long")]
    public void Run_OptInWithString_IsSQLSRC283(string driver, string type)
    {
        var run = RunOptIn(driver, "query marker", IsSqlServer(driver) ? NVarCharMax : Text, type: type);

        ShouldBeSQLSRC283(
            run,
            ["column", "parameter"],
            type,
            "a -- column: marker of the query",
            "'" + type + "' is a type that the driver reads, which the serializer never takes"
        );
    }

    // An array of text is read as string[], not as a string, so the opt-in on it is SQLSRC283, whole or by element.
    [Theory]
    [InlineData("App.OrderMetadata[]")]
    [InlineData("App.OrderMetadata")]
    public void Run_OptInOnTextArray_IsSQLSRC283(string type)
    {
        var run = RunOptIn("Npgsql 10", "query marker", TextArray, type: type);

        ShouldBeSQLSRC283(
            run,
            ["column", "parameter"],
            type,
            "a -- column: marker of the query",
            "the database type 'text[]' is read as 'string[]', not as a string"
        );
    }

    // A type that the base map gives no C# type, as a composite, is not read as a string either.
    [Fact]
    public void Run_OptInOnAComposite_IsSQLSRC283()
    {
        var composite = TestSidecars.Postgres(
            name: "address",
            kind: SidecarValues.Kind.Composite,
            schema: "public",
            internalName: "address",
            bindName: "address"
        );

        var run = RunOptIn("Npgsql 10", "query marker", composite);

        ShouldBeSQLSRC283(
            run,
            ["column", "parameter"],
            "App.OrderMetadata",
            "a -- column: marker of the query",
            "the database type 'public.address' is not read as a string"
        );
    }

    // A query whose output is models uses no conversion: the opt-in that does not apply reports nothing, and the
    // model has the class.
    [Theory]
    [InlineData("Npgsql 10")]
    [InlineData("SqlClient 7")]
    public void Run_OptInInAModelsQuery_ReportsNothing(string driver)
    {
        var isSqlServer = IsSqlServer(driver);
        var database = isSqlServer
            ? TestSidecars.SqlServer()
            : (SidecarType)(Catalog("int4") with { BindName = "int4" });

        var run = RunMeta(
            driver,
            "-- name: SaveMeta\n-- output: models\n" + Markers("json App.OrderMetadata") + SaveMeta,
            type: database
        );

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources["App.SaveMetaDto-Model.g.cs"].ShouldContain(OrderMetadata + " Meta");
        run.Sources["App.SaveMetaParams-Model.g.cs"].ShouldContain(OrderMetadata + " Meta");
    }

    // A type override of jsonb to the class reaches a models query and a codegen query of one file: the models
    // query's model keeps the class, with nothing reported, and the codegen query's method serializes it.
    [Theory]
    [InlineData("Npgsql 10", "jsonb")]
    [InlineData("SqlClient 7", "json")]
    public void Run_OverrideToAClassSharedByModelsAndCodeGen(string driver, string key)
    {
        var isSqlServer = IsSqlServer(driver);
        SidecarType database = isSqlServer ? SqlServerJson : Jsonb;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: "
                + key
                + "=App.OrderMetadata\n\n-- name: SaveMeta\n"
                + SaveMeta
                + "\n-- name: ReadMeta\n-- output: models\nSELECT meta FROM orders WHERE meta = @meta;\n"
        );
        SidecarParameter[] parameters =
        [
            TestSidecars.Parameter(0, "meta", database, false),
            TestSidecars.Parameter(0, "maybe_meta", database, true),
        ];
        SidecarColumn[] columns =
        [
            TestSidecars.Column(0, "meta", database, nullable: false),
            TestSidecars.Column(0, "maybe_meta", database, nullable: true),
        ];
        SidecarEntry[] entries = isSqlServer
            ?
            [
                MethodRuns.SqlServerEntry(file, "SaveMeta", parameters, columns),
                MethodRuns.SqlServerEntry(file, "ReadMeta", parameters[..1], columns[..1]),
            ]
            : [Entry(file, "SaveMeta", parameters, columns), Entry(file, "ReadMeta", parameters[..1], columns[..1])];

        var run = isSqlServer ? MethodRuns.RunSqlServer(file, entries, Source) : MethodRuns.Run(file, entries, Source);

        run.ShouldBeClean();
        run.Sources["App.ReadMetaDto-Model.g.cs"].ShouldContain(OrderMetadata + " Meta");
        run.Sources["App.ReadMetaParams-Model.g.cs"].ShouldContain(OrderMetadata + " Meta");
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(Deserialize + OrderMetadata + ">(reader.GetFieldValue<string>(0))");
        methods.ShouldContain(Serialize + OrderMetadata + ">(value)");
        methods.ShouldNotContain("ReadMeta");
    }

    // A converter of the project between string and the class replaces the serializer on an opt-in column too.
    [Theory]
    [InlineData("Npgsql 10")]
    [InlineData("SqlClient 7")]
    public void Run_ProjectConverterOfTheClass_ReplacesTheSerializerOnAnOptIn(string driver)
    {
        var run = RunMeta(
            driver,
            "-- name: SaveMeta\n" + Markers("json App.OrderMetadata") + SaveMeta,
            ConvertersSource(
                "[SqlSourceConverter] public static OrderMetadata FromJson(string value) => new() { Note = value };\n"
                    + "[SqlSourceConverter] public static string ToJson(OrderMetadata value) => value.Note ?? \"\";"
            ),
            type: IsSqlServer(driver) ? NVarCharMax : Text
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::App.OrderMetadata.FromJson(reader.GetFieldValue<string>(0)), ");
        methods.ShouldContain("global::App.OrderMetadata.ToJson(value)");
        methods.ShouldNotContain("JsonSerializer");
        methods.ShouldNotContain("holds a JSON null");
    }

    // SaveMeta with the opt-in, or with the plain mapping when json is false, to the type at the place: the -- param:
    // markers give the parameters, and the place the columns, but for the -- param: marker alone.
    private static GeneratorRun RunOptIn(
        string driver,
        string place,
        SidecarType database,
        bool json = true,
        string type = "App.OrderMetadata"
    )
    {
        var marked = (json ? "json " : "") + type;
        var flag = json ? "true" : "false";
        var settings =
            place == "settings file"
                ? "{\"columnTypes\": {\"orders.meta\": {\"type\": \""
                    + type
                    + "\", \"json\": "
                    + flag
                    + "}, \"orders.maybe_meta\": {\"type\": \""
                    + type
                    + "\", \"json\": "
                    + flag
                    + "}}}"
                : null;
        var text = place switch
        {
            "param marker" => "-- name: SaveMeta\n" + ParamMarkers(marked) + SaveMeta,
            "query marker" => "-- name: SaveMeta\n" + Markers(marked) + SaveMeta,
            "header" => "-- column: orders.meta -> "
                + marked
                + "\n-- column: orders.maybe_meta -> "
                + marked
                + "\n\n-- name: SaveMeta\n"
                + ParamMarkers(marked)
                + SaveMeta,
            _ => "-- name: SaveMeta\n" + ParamMarkers(marked) + SaveMeta,
        };
        return RunMeta(driver, text, settings: settings, type: database);
    }

    // The serializer's read of both columns, when the place gave them the class, and the driver's of a string when it
    // did not.
    private static void ShouldReadThroughTheSerializer(string methods, string place)
    {
        if (place == "param marker")
        {
            methods.ShouldContain("reader.GetFieldValue<string>(0), ");
            methods.ShouldNotContain(Deserialize);
            return;
        }

        methods.ShouldContain(
            "("
                + Deserialize
                + OrderMetadata
                + ">(reader.GetFieldValue<string>(0)) ?? throw new global::System.InvalidCastException("
                + "\"Column 'meta' holds a JSON null.\"), "
        );
        methods.ShouldContain(
            "reader.IsDBNull(1) ? default("
                + OrderMetadata
                + "?) : "
                + Deserialize
                + OrderMetadata
                + ">(reader.GetFieldValue<string>(1)))"
        );
    }

    // SQLSRC283 for meta and maybe_meta as each subject, at SaveMeta's name on the line given, and no method: a column
    // names the place given, and a parameter its -- param: marker.
    private static void ShouldBeSQLSRC283(
        GeneratorRun run,
        string[] subjects,
        string type,
        string place,
        string reason,
        int line = 1
    )
    {
        var at = "SQLSRC283 " + GeneratorHarness.UsersPath + "(" + line + ",10)-(" + line + ",18): ";
        var expected = subjects
            .SelectMany(subject =>
                MetaNames.Select(name =>
                    (Subject: subject + " '" + name + "'", Place: subject == "parameter" ? ParamPlace : place)
                )
            )
            .Select(named =>
                at
                + "The "
                + named.Subject
                + " is mapped to json '"
                + type
                + "' in "
                + named.Place
                + ", but "
                + reason
                + ".  Remove json, or use it only to map a column or parameter of a string or JSON type to a class "
                + "of this project."
            );
        run.Problems().ShouldBe(expected, ignoreOrder: true);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    private static bool IsSqlServer(string driver) => driver.StartsWith("SqlClient", System.StringComparison.Ordinal);

    // The markers that give the type to the columns and the parameters meta and maybe_meta.
    private static string Markers(string type) => ColumnMarkers(type) + ParamMarkers(type);

    private static string ColumnMarkers(string type) =>
        "-- column: meta -> " + type + "\n-- column: maybe_meta -> " + type + "\n";

    private static string ParamMarkers(string type) =>
        "-- param: @meta -> " + type + "\n-- param: @maybe_meta -> " + type + "\n";

    // Source with the members given in App.OrderMetadata.
    private static string ConvertersSource(string members) =>
        Source.Replace(
            "public string? Note { get; set; }",
            "public string? Note { get; set; }\n" + members,
            System.StringComparison.Ordinal
        );

    // SaveMeta of the text on the driver: meta and maybe_meta, as columns of orders and as parameters, of the type,
    // jsonb on PostgreSQL and json on SQL Server unless another is given, meta not null and maybe_meta nullable.
    private static GeneratorRun RunMeta(
        string driver,
        string text,
        string source = Source,
        string? settings = null,
        SidecarType? type = null
    )
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        var isSqlServer = IsSqlServer(driver);
        var database = type ?? (isSqlServer ? SqlServerJson : Jsonb);
        var schema = isSqlServer ? "dbo" : "public";
        SidecarParameter[] parameters =
        [
            TestSidecars.Parameter(0, "meta", database, false),
            TestSidecars.Parameter(0, "maybe_meta", database, true),
        ];
        SidecarColumn[] columns =
        [
            TestSidecars.Column(
                0,
                "meta",
                database,
                nullable: false,
                origin: new SidecarOrigin(schema, "orders", "meta")
            ),
            TestSidecars.Column(
                0,
                "maybe_meta",
                database,
                nullable: true,
                origin: new SidecarOrigin(schema, "orders", "maybe_meta")
            ),
        ];
        MetadataReference[] references = driver switch
        {
            "SqlClient 7" => [SqlClientReferences.SqlClient7],
            "Npgsql 8" => [NpgsqlReferences.Npgsql8],
            _ => [NpgsqlReferences.Npgsql10],
        };
        return isSqlServer
            ? MethodRuns.RunSqlServer(
                file,
                [MethodRuns.SqlServerEntry(file, "SaveMeta", parameters, columns)],
                source,
                references,
                settings
            )
            : MethodRuns.Run(
                file,
                [Entry(file, "SaveMeta", parameters, columns)],
                source,
                references,
                settings: settings
            );
    }
}
