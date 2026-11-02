using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Testing.Generator;

// The cases of the golden corpus of query methods: for each engine of CorpusEngine.All, the shared scenarios and then
// the engine's own, each named `<engine>/<scenario>`, the path of its golden file under GoldenFiles.Folder.  See "The
// golden corpus of query methods" in tests/README.md.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class QueryMethodCorpus
{
    // The scenarios that every engine runs.
    public static IReadOnlyList<CorpusScenario> Shared { get; } = [new("shapes", Shapes)];

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
    // has them, as the tool writes it.
    private static GeneratorRun Shapes(CorpusEngine engine)
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
        var id = CorpusEngine.Parameter("id", engine.Int, nullable: false);
        SidecarParameter[] none = [];
        SidecarColumn[] columns =
        [
            CorpusEngine.Column("id", engine.Int),
            CorpusEngine.Column("name", engine.Text, nullable: true),
        ];
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
            ]
        );
    }
}
