using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Testing.Generator;

// The part of the golden corpus of query methods that holds the scenarios of the types that claim the queries:
// placement, where each member location and visibility and each kind of type puts the methods, and diagnostics, the
// problems that cost a query its method or a type its extension class.  QueryMethodCorpus.cs lists the cases.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // The types of placement, each claiming the file of its name: a public and an internal type whose methods go into
    // an extension class, a type of each visibility that its file puts the methods on the type with, a type nested in
    // another, and a type of the global namespace.  Headers gives each file's markers.
    private const string PlacementSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = "Sample.sql")]
            public partial class Sample;

            [SqlSourceGenerate(Source = "InternalSample.sql")]
            internal partial class InternalSample;

            [SqlSourceGenerate(Source = "PublicSample.sql")]
            public partial class PublicSample;

            [SqlSourceGenerate(Source = "InternalPlacement.sql")]
            public partial class InternalPlacement;

            [SqlSourceGenerate(Source = "PrivatePlacement.sql")]
            public partial class PrivatePlacement;

            public partial class Outer
            {
                [SqlSourceGenerate(Source = "Inner.sql")]
                public partial class Inner;
            }
        }

        [SqlSourceGenerate(Source = "GlobalSample.sql")]
        public partial class GlobalSample;
        """;

    // The types of diagnostics: App.Sample, whose queries have the problems of one query; App.Blocked.Sample, whose
    // extension class a type of the project takes; and two types named Queries nested in two types, which share the
    // extension class App.QueriesExtensions.
    private const string DiagnosticsSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = "Users.sql")]
            public partial class Sample;

            public partial class First
            {
                [SqlSourceGenerate(Source = "A.sql")]
                public partial class Queries;
            }

            public partial class Second
            {
                [SqlSourceGenerate(Source = "B.sql")]
                public partial class Queries;
            }
        }

        namespace App.Blocked
        {
            [SqlSourceGenerate(Source = "Blocked.sql")]
            public partial class Sample;

            public static class SampleExtensions;
        }
        """;

    // The header of each placement type's file: none for the extension class, the default, or the markers that put
    // the methods on the type with a visibility.
    private static readonly (string Type, string Header)[] Headers =
    [
        ("Sample", ""),
        ("InternalSample", ""),
        ("PublicSample", "-- member-location: class\n-- visibility: public\n\n"),
        ("InternalPlacement", "-- member-location: class\n-- visibility: internal\n\n"),
        ("PrivatePlacement", "-- member-location: class\n-- visibility: private\n\n"),
        ("Inner", "-- member-location: class\n-- visibility: public\n\n"),
        ("GlobalSample", ""),
    ];

    // A one query and a stream of each placement's type, each claiming its file, named after the type so that no two
    // extension classes of one namespace have a method of one signature.
    private static GeneratorRun Placement(CorpusEngine engine)
    {
        SidecarParameter[] id = [IdParameter(engine)];
        var columns = IdAndName(engine);
        var files = new List<SqlFile>();
        foreach (var (type, header) in Headers)
        {
            var file = new SqlFile(
                "/app/Repo/" + type + ".sql",
                header
                    + "-- name: Get"
                    + type
                    + " -> one\nSELECT id, name FROM users WHERE id = @id;\n-- name: Stream"
                    + type
                    + "\n-- collection-type: IAsyncEnumerable\nSELECT id, name FROM users WHERE id > @id;\n"
            );
            files.Add(file);
            files.Add(
                Sidecars.Of(
                    file,
                    engine.Entry(file, "Get" + type, id, columns),
                    engine.Entry(file, "Stream" + type, id, columns)
                )
            );
        }

        return engine.Run([new SourceFile(GeneratorHarness.SourcePath, PlacementSource)], [.. files]);
    }

    // Users.sql has a token named as a fixed argument (SQLSRC275), a shape of rows on an UPDATE (SQLSRC272), a query
    // named as a member of the connection and one named as the extension class (SQLSRC274), and a query with no
    // problem, whose method is still written.  A.sql and B.sql each have a Get of the one extension class
    // App.QueriesExtensions, so that neither is written (SQLSRC274), and A.sql's other method is.  Blocked.sql's type
    // has its extension class taken by a type of the project (SQLSRC276).
    private static GeneratorRun Diagnostics(CorpusEngine engine)
    {
        var users = new SqlFile(
            GeneratorHarness.UsersPath,
            """
            -- name: ListFromTransaction
            -- token: {{transaction:users}}
            SELECT id, name FROM {{transaction}} WHERE id > @id;
            -- name: SetName -> one
            UPDATE users SET name = @name WHERE id = @id;
            -- name: Open
            SELECT id, name FROM users WHERE id > @id;
            -- name: SampleExtensions
            SELECT id, name FROM users WHERE id > @id;
            -- name: GetUser -> one
            SELECT id, name FROM users WHERE id = @id;

            """
        );
        var a = new SqlFile(
            "/app/Repo/A.sql",
            """
            -- name: Get
            SELECT id, name FROM users WHERE id = @id;
            -- name: ListA
            SELECT id, name FROM users WHERE id > @id;

            """
        );
        var b = new SqlFile("/app/Repo/B.sql", "-- name: Get\nSELECT id, name FROM users;\n");
        var blocked = new SqlFile(
            "/app/Repo/Blocked.sql",
            "-- name: GetBlocked -> one\nSELECT id, name FROM users WHERE id = @id;\n"
        );
        var idParameter = IdParameter(engine);
        SidecarParameter[] id = [idParameter];
        var columns = IdAndName(engine);
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, DiagnosticsSource)],
            [
                users,
                Sidecars.Of(
                    users,
                    engine.Entry(users, "ListFromTransaction", id, columns),
                    engine.Entry(
                        users,
                        "SetName",
                        [CorpusEngine.Parameter("name", engine.Text, nullable: true), idParameter],
                        null
                    ),
                    engine.Entry(users, "Open", id, columns),
                    engine.Entry(users, "SampleExtensions", id, columns),
                    engine.Entry(users, "GetUser", id, columns)
                ),
                a,
                Sidecars.Of(a, engine.Entry(a, "Get", id, columns), engine.Entry(a, "ListA", id, columns)),
                b,
                Sidecars.Of(b, engine.Entry(b, "Get", [], columns)),
                blocked,
                Sidecars.Of(blocked, engine.Entry(blocked, "GetBlocked", id, columns)),
            ]
        );
    }
}
