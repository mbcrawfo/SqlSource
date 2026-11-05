using SqlSource.Snapshot;

namespace SqlSource.Testing.Generator;

// The part of the golden corpus of query methods that holds the scenarios lists and lists-no-padding: the methods of
// queries whose parameters are lists, which send one parameter for each value.  QueryMethodCorpus.cs lists the cases.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // A list after IN and after NOT IN; two lists; one list used twice; a list with a null test beside a parameter
    // that is no list; a list beside a token; a list beside a parameter named as the list's first value would be; a
    // list whose comments are kept; a stream; lists of a value type and of a class each bound through a converter; and
    // a list in a one-optional query.  Each list's entry describes its element, as the tool writes it, with the name
    // a PostgreSQL binding sets.
    private static GeneratorRun Lists(CorpusEngine engine)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListIds\nSELECT id FROM accounts WHERE id IN @ids;\n"
                + "-- name: ListExcept\nSELECT id FROM accounts WHERE id NOT IN @ids;\n"
                + "-- name: ListTwoLists\nSELECT id FROM accounts WHERE id IN @ids AND owner_id IN @owners;\n"
                + "-- name: ListUsedTwice\nSELECT id FROM accounts WHERE id IN @ids OR owner_id IN @ids;\n"
                + "-- name: ListOptional\n"
                + "SELECT id FROM accounts WHERE (@ids IS NULL OR id IN @ids) AND owner_id = @owner;\n"
                + "-- name: ListWithToken\n-- token: {{table:accounts}}\n"
                + "SELECT id FROM {{table}} WHERE id IN @ids;\n"
                + "-- name: ListBesideItsExpansionName\n"
                + "SELECT id FROM accounts WHERE id IN @ids OR id = @ids_0;\n"
                + "-- name: ListKeepsComments\n-- generator: keep-comments\n"
                + "SELECT id FROM accounts WHERE id IN /* c */ @ids;\n"
                + "-- name: StreamIds\n-- collection-type: IAsyncEnumerable\n"
                + "SELECT id FROM accounts WHERE id IN @ids;\n"
                + "-- name: ListConverted\n-- type-override: "
                + engine.IntOverrideKey
                + "=App.UserId\n-- type-override: "
                + engine.TextOverrideKey
                + "=App.Email\nSELECT id FROM accounts WHERE id IN @ids AND email IN @emails;\n"
                + "-- name: ListOneShape -> one-optional\nSELECT id FROM accounts WHERE id IN @ids;\n"
        );
        var ids = CorpusEngine.Parameter("ids", engine.BoundInt);
        SidecarParameter[] justIds = [ids];
        SidecarColumn[] columns = [CorpusEngine.Column("id", engine.Int)];
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, ConvertersSource)],
            [
                file,
                Sidecars.Of(
                    file,
                    engine.Entry(file, "ListIds", justIds, columns),
                    engine.Entry(file, "ListExcept", justIds, columns),
                    engine.Entry(
                        file,
                        "ListTwoLists",
                        [ids, CorpusEngine.Parameter("owners", engine.BoundInt)],
                        columns
                    ),
                    engine.Entry(file, "ListUsedTwice", justIds, columns),
                    engine.Entry(
                        file,
                        "ListOptional",
                        [ids, CorpusEngine.Parameter("owner", engine.BoundInt, nullable: false)],
                        columns
                    ),
                    engine.Entry(file, "ListWithToken", justIds, columns),
                    engine.Entry(
                        file,
                        "ListBesideItsExpansionName",
                        [ids, CorpusEngine.Parameter("ids_0", engine.BoundInt, nullable: false)],
                        columns
                    ),
                    engine.Entry(file, "ListKeepsComments", justIds, columns),
                    engine.Entry(file, "StreamIds", justIds, columns),
                    engine.Entry(
                        file,
                        "ListConverted",
                        [ids, CorpusEngine.Parameter("emails", engine.BoundText)],
                        columns
                    ),
                    engine.Entry(file, "ListOneShape", justIds, columns)
                ),
            ]
        );
    }

    // ListIds under no-list-padding, which sends each list's values without padding them.
    private static GeneratorRun ListsWithoutPadding(CorpusEngine engine)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListIds\n-- generator: no-list-padding\nSELECT id FROM accounts WHERE id IN @ids;\n"
        );
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [
                file,
                Sidecars.Of(
                    file,
                    engine.Entry(
                        file,
                        "ListIds",
                        [CorpusEngine.Parameter("ids", engine.BoundInt)],
                        [CorpusEngine.Column("id", engine.Int)]
                    )
                ),
            ]
        );
    }
}
