using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlSource.Generation.Exceptions;
using SqlSource.Snapshot;

namespace SqlSource.Testing.Generator;

// The cases of the golden corpus of query methods: for each engine of CorpusEngine.All, the shared scenarios and then
// the engine's own, each named `<engine>/<scenario>`, the path of its golden file under GoldenFiles.Folder.  See "The
// golden corpus of query methods" in tests/README.md.  The shared scenarios are here and, by theme, in the other parts
// of this class: CorpusTokenScenarios.cs, CorpusTypeScenarios.cs, CorpusConverterScenarios.cs and
// CorpusLibraryScenarios.cs.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // The settings of collections-no-pool, over the project's settings file: no row buffer.
    private const string NoArrayPool = /*lang=json,strict*/
        """{"generatorParameters": ["no-array-pool"]}""";

    // The scenarios that every engine runs.  Cases reads it, so both stay in this part, where their order is kept.
    public static IReadOnlyList<CorpusScenario> Shared { get; } =
    [
        new("shapes", engine => Shapes(engine, properties: null)),
        new("collections", engine => Collections(engine, settings: null)),
        new("collections-no-pool", engine => Collections(engine, NoArrayPool)),
        new("tokens", Tokens),
        new("placement", Placement),
        new("converters", Converters),
        new("nodatime", NodaTime),
        new("json", Json),
        new("diagnostics", Diagnostics),
        new(
            "public-exceptions",
            engine =>
                Shapes(
                    engine,
                    new Dictionary<string, string?>(StringComparer.Ordinal) { [PublicExceptions.PropertyName] = "true" }
                )
        ),
    ];

    public static IReadOnlyList<(string Name, CorpusEngine Engine, CorpusScenario Scenario)> Cases { get; } =
        ListCases();

    public static GeneratorRun RunCase(string name)
    {
        foreach (var (caseName, engine, scenario) in Cases)
        {
            if (string.Equals(caseName, name, StringComparison.Ordinal))
            {
                return scenario.Run(engine);
            }
        }

        throw new ArgumentException($"The corpus has no case {name}.", nameof(name));
    }

    private static List<(string Name, CorpusEngine Engine, CorpusScenario Scenario)> ListCases()
    {
        var cases = new List<(string, CorpusEngine, CorpusScenario)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var engine in CorpusEngine.All)
        {
            foreach (var scenario in Shared.Concat(engine.OwnScenarios))
            {
                var name = engine.Name + "/" + scenario.Name;
                if (!names.Add(name))
                {
                    throw new InvalidOperationException($"The corpus has two cases named {name}.");
                }

                cases.Add((name, engine, scenario));
            }
        }

        return cases;
    }

    // A query of each shape a method returns: none, a row count, one row, one row or none, and many rows, with and
    // without parameters, and many rows in the default collection.  An entry lists its parameters in the order the SQL
    // has them, as the tool writes it.  The properties are what MSBuild tells the generator: public-exceptions runs
    // these queries with the exceptions public.
    private static GeneratorRun Shapes(CorpusEngine engine, IReadOnlyDictionary<string, string?>? properties)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            """
            -- name: Touch -> none
            UPDATE users SET seen = 1;
            -- name: Rename -> none
            UPDATE users SET name = @name WHERE id = @id;
            -- name: CountUsers -> rowcount
            DELETE FROM users WHERE id = @id;
            -- name: GetUser -> one
            SELECT id, name FROM users WHERE id = @id;
            -- name: FindUser -> one-optional
            SELECT id, name FROM users WHERE id = @id;
            -- name: FirstUser -> one
            SELECT id, name FROM users ORDER BY id;
            -- name: ListUsers
            SELECT id, name FROM users WHERE id > @id;
            -- name: ListAll -> many
            SELECT id, name FROM users;

            """
        );
        var id = IdParameter(engine);
        SidecarParameter[] none = [];
        var columns = IdAndName(engine);
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [
                file,
                Sidecars.Of(
                    file,
                    engine.Entry(file, "Touch", none, null),
                    engine.Entry(
                        file,
                        "Rename",
                        [CorpusEngine.Parameter("name", engine.Text, nullable: true), id],
                        null
                    ),
                    engine.Entry(file, "CountUsers", [id], null),
                    engine.Entry(file, "GetUser", [id], columns),
                    engine.Entry(file, "FindUser", [id], columns),
                    engine.Entry(file, "FirstUser", none, columns),
                    engine.Entry(file, "ListUsers", [id], columns),
                    engine.Entry(file, "ListAll", none, columns)
                ),
            ],
            properties: properties
        );
    }

    // A many query in each collection a method returns, by its -- collection-type: marker, and a second stream with no
    // parameters.  The settings, a JSON object, go over the project's settings file: collections-no-pool turns off the
    // row buffer that the arrays, the lists, the immutable arrays and their interfaces read their rows into.
    private static GeneratorRun Collections(CorpusEngine engine, string? settings)
    {
        string[] kinds =
        [
            "Array",
            "IEnumerable",
            "ICollection",
            "IReadOnlyCollection",
            "IList",
            "IReadOnlyList",
            "List",
            "ImmutableArray",
            "ImmutableList",
            "IImmutableList",
            "IAsyncEnumerable",
        ];
        var text = new StringBuilder();
        foreach (var kind in kinds)
        {
            _ = text.Append("-- name: ListAs")
                .Append(kind)
                .Append("\n-- collection-type: ")
                .Append(kind)
                .Append("\nSELECT id, name FROM users WHERE id > @id;\n");
        }

        _ = text.Append("-- name: StreamAll\n-- collection-type: IAsyncEnumerable\nSELECT id, name FROM users;\n");
        var file = new SqlFile(GeneratorHarness.UsersPath, text.ToString());
        SidecarParameter[] id = [IdParameter(engine)];
        var columns = IdAndName(engine);
        var entries = kinds
            .Select(kind => engine.Entry(file, "ListAs" + kind, id, columns))
            .Append(engine.Entry(file, "StreamAll", [], columns))
            .ToArray();
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [file, Sidecars.Of(file, entries)],
            settings: settings
        );
    }

    // The parameter most queries take: id, an Int that cannot be null.
    private static SidecarParameter IdParameter(CorpusEngine engine) =>
        CorpusEngine.Parameter("id", engine.Int, nullable: false);

    // The columns most queries read: id, an Int that cannot be null, and name, a Text that can.
    private static SidecarColumn[] IdAndName(CorpusEngine engine) =>
        [CorpusEngine.Column("id", engine.Int), CorpusEngine.Column("name", engine.Text, nullable: true)];
}
