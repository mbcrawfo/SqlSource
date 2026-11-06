using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using SqlSource.Snapshot;

namespace SqlSource.Testing.Generator.Corpus;

// The part of the golden corpus of query methods that holds the scenarios of the libraries and of the drivers:
// nodatime, NodaTime's types read and written; json, classes read and written through System.Text.Json; and
// PostgreSQL's own driver-npgsql8 and driver-npgsql10, a JsonNode and the dates and times that Npgsql 8 and 10 read
// differently.
// QueryMethodCorpus.cs lists the shared cases, and CorpusEngine.cs PostgreSQL's own.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // App.Sample, with the classes json reads and writes: a class, a record and a value type.
    private const string JsonSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample;

            public sealed class OrderMetadata
            {
                public string? Note { get; set; }
            }

            public sealed record Tag(string Name);

            public readonly record struct Point(int X, int Y);
        }
        """;

    // The run of driver-npgsql8 and driver-npgsql10 on the driver: jsonb overridden to JsonNode, which Npgsql 8
    // converts from and to a string and Npgsql 10 reads and writes itself, and date and time, which Npgsql 8 reads as
    // DateTime and TimeSpan and Npgsql 10 as DateOnly and TimeOnly, each in a column and a parameter that can be null
    // and that cannot, in a many, a one and a stream query.
    public static GeneratorRun Driver(CorpusEngine engine, MetadataReference driver)
    {
        (string Name, SidecarType Type)[] types =
        [
            ("doc", engine.Json),
            ("day", ModelRuns.Catalog("date")),
            ("clock", ModelRuns.Catalog("time") with { Name = "time without time zone" }),
        ];
        return LibraryQueries(
            engine,
            MethodRuns.Source,
            "-- type-override: jsonb=System.Text.Json.Nodes.JsonNode\n\n",
            "Doc",
            "docs",
            "",
            Parameters(types),
            Columns(types),
            [driver],
            settings: null
        );
    }

    // NodaTime's types in each column type of the engine that has them, in a column and a parameter that can be null
    // and that cannot, in a many, a one and a stream query, on the engine's references and settings of NodaTime.
    private static GeneratorRun NodaTime(CorpusEngine engine) =>
        LibraryQueries(
            engine,
            MethodRuns.Source,
            "",
            "Event",
            "events",
            "",
            Parameters(engine.NodaTimeTypes),
            Columns(engine.NodaTimeTypes),
            engine.NodaTimeReferences,
            engine.NodaTimeSettings
        );

    // Classes of the project in the engine's JSON, in a many, a one and a stream query: App.OrderMetadata read whole
    // from a column that cannot be null, which throws on a JSON null, and from one that can, and written whole to a
    // parameter of each; a JsonObject that cannot be null; and App.Tag read from a string column that cannot be null
    // and from one that can, each opted in to JSON by its marker.  An engine with arrays of its JSON also reads and
    // writes App.Tag[] by element, from a column that cannot be null, which throws on a JSON null element, and
    // App.Point?[] from a column that can.
    private static GeneratorRun Json(CorpusEngine engine)
    {
        var markers =
            "-- column: meta -> App.OrderMetadata\n-- column: maybe_meta -> App.OrderMetadata\n"
            + "-- column: doc -> System.Text.Json.Nodes.JsonObject\n"
            + "-- column: notes -> json App.Tag\n-- column: maybe_notes -> json App.Tag\n"
            + "-- param: @meta -> App.OrderMetadata\n-- param: @maybe_meta -> App.OrderMetadata\n";
        var parameters = new List<SidecarParameter>
        {
            CorpusEngine.Parameter("meta", engine.Json, nullable: false),
            CorpusEngine.Parameter("maybe_meta", engine.Json, nullable: true),
        };
        var columns = new List<SidecarColumn>
        {
            CorpusEngine.Column("meta", engine.Json),
            CorpusEngine.Column("maybe_meta", engine.Json, nullable: true),
            CorpusEngine.Column("doc", engine.Json),
            CorpusEngine.Column("notes", engine.UnboundedText),
            CorpusEngine.Column("maybe_notes", engine.UnboundedText, nullable: true),
        };
        if (engine.JsonArray is { } array)
        {
            markers +=
                "-- column: tags -> App.Tag[]\n-- column: points -> App.Point?[]\n"
                + "-- param: @tags -> App.Tag[]\n-- param: @points -> App.Point?[]\n";
            parameters.Add(CorpusEngine.Parameter("tags", array, nullable: false));
            parameters.Add(CorpusEngine.Parameter("points", array, nullable: true));
            columns.Add(CorpusEngine.Column("tags", array));
            columns.Add(CorpusEngine.Column("points", array, nullable: true));
        }

        return LibraryQueries(
            engine,
            JsonSource,
            "",
            "Order",
            "orders",
            markers,
            [.. parameters],
            [.. columns],
            engine.Driver,
            settings: null
        );
    }

    // List<Noun>s, a many query, Get<Noun>, a one, and Stream<Noun>s, a stream, each with the markers after its name,
    // reading the columns from the table and finding by the parameters, each named as a column, in their order.  The
    // header starts the file.
    private static GeneratorRun LibraryQueries(
        CorpusEngine engine,
        string source,
        string header,
        string noun,
        string table,
        string markers,
        SidecarParameter[] parameters,
        SidecarColumn[] columns,
        MetadataReference[] references,
        string? settings
    )
    {
        var select =
            "SELECT "
            + string.Join(", ", columns.Select(column => column.Name))
            + " FROM "
            + table
            + "\nWHERE "
            + string.Join(" AND ", parameters.Select(parameter => parameter.Name + " = @" + parameter.Name))
            + ";\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            header
                + "-- name: List"
                + noun
                + "s\n"
                + markers
                + select
                + "-- name: Get"
                + noun
                + " -> one\n"
                + markers
                + select
                + "-- name: Stream"
                + noun
                + "s\n-- collection-type: IAsyncEnumerable\n"
                + markers
                + select
        );
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [
                file,
                Sidecars.Of(
                    file,
                    engine.Entry(file, "List" + noun + "s", parameters, columns),
                    engine.Entry(file, "Get" + noun, parameters, columns),
                    engine.Entry(file, "Stream" + noun + "s", parameters, columns)
                ),
            ],
            references,
            settings
        );
    }

    // For each type, a parameter of its name that cannot be null and one named maybe_<name> that can.
    private static SidecarParameter[] Parameters(IEnumerable<(string Name, SidecarType Type)> types) =>
        [
            .. types.SelectMany(type =>
                new[]
                {
                    CorpusEngine.Parameter(type.Name, type.Type, nullable: false),
                    CorpusEngine.Parameter("maybe_" + type.Name, type.Type, nullable: true),
                }
            ),
        ];

    // For each type, a column of its name that cannot be null and one named maybe_<name> that can.
    private static SidecarColumn[] Columns(IEnumerable<(string Name, SidecarType Type)> types) =>
        [
            .. types.SelectMany(type =>
                new[]
                {
                    CorpusEngine.Column(type.Name, type.Type),
                    CorpusEngine.Column("maybe_" + type.Name, type.Type, nullable: true),
                }
            ),
        ];
}
