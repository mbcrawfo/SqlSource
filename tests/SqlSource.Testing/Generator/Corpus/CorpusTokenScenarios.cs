using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Testing.Generator.Corpus;

// The part of the golden corpus of query methods that holds the scenario tokens: the methods of queries with tokens,
// in each shape, with a token named as a keyword that is not reserved, as each local of a method, and with and without
// the check of their text.  QueryMethodCorpus.cs lists the cases.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // A token named as each local and local function a method or its iterator has, which the method renames, each with
    // the default that a query whose output is codegen needs.
    private const string LocalTokens =
        "{{sql:a}} {{command:b}} {{reader:c}} {{row:d}} {{rows:e}} {{wasClosed:f}} {{exception:g}} {{atRow:h}} "
        + "{{ReadRows:i}} {{value:j}} {{span:k}} {{state:l}}";

    // A token's method in each shape and as a stream; the token await, which is written @await, alone and after
    // another; two tokens, and a token with no -- token: marker, whose default is in the SQL; a stream and a one query
    // whose tokens are not checked; and a stream and a one query with a token named as each of a method's locals.  Each
    // takes @id.
    private static GeneratorRun Tokens(CorpusEngine engine)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            """
            -- name: ListFrom
            -- token: {{table:users}}
            SELECT id, name FROM {{table}} WHERE id > @id;
            -- name: FindFrom -> one-optional
            -- token: {{table:users}}
            SELECT id, name FROM {{table}} WHERE id = @id;
            -- name: CountFrom -> rowcount
            -- token: {{table:users}}
            DELETE FROM {{table}} WHERE id = @id;
            -- name: TouchFrom -> none
            -- token: {{table:users}}
            UPDATE {{table}} SET seen = 1 WHERE id = @id;
            -- name: StreamFrom
            -- collection-type: IAsyncEnumerable
            -- token: {{table:users}}
            SELECT id, name FROM {{table}} WHERE id > @id;
            -- name: GetAwait -> one
            -- token: {{await:users}}
            SELECT id, name FROM {{await}} WHERE id = @id;
            -- name: StreamAwait
            -- collection-type: IAsyncEnumerable
            -- token: {{await:users}}
            SELECT id, name FROM {{await}} WHERE id > @id;
            -- name: ListAwaitFromSchema
            -- token: {{schema:app}}
            -- token: {{await:users}}
            SELECT id, name FROM {{schema}}.{{await}} WHERE id > @id;
            -- name: ListFromSchema
            -- token: {{schema:app}}
            -- token: {{table:users}}
            SELECT id, name FROM {{schema}}.{{table}} WHERE id > @id;
            -- name: ListFromInline
            SELECT id, name FROM {{table:users}} WHERE id > @id;
            -- name: StreamUnchecked
            -- collection-type: IAsyncEnumerable
            -- generator: token-validation=off
            -- token: {{table:users}}
            SELECT id, name FROM {{table}} WHERE id > @id;
            -- name: GetUnchecked -> one
            -- generator: token-validation=off
            -- token: {{table:users}}
            SELECT id, name FROM {{table}} WHERE id = @id;

            """
                + "-- name: StreamLocals\n-- collection-type: IAsyncEnumerable\n"
                + "SELECT id, name FROM users WHERE id > @id "
                + LocalTokens
                + ";\n-- name: GetLocals -> one\nSELECT id, name FROM users WHERE id = @id "
                + LocalTokens
                + ";\n"
        );
        SidecarParameter[] id = [IdParameter(engine)];
        var columns = IdAndName(engine);
        string[] withRows =
        [
            "ListFrom",
            "FindFrom",
            "StreamFrom",
            "GetAwait",
            "StreamAwait",
            "ListAwaitFromSchema",
            "ListFromSchema",
            "ListFromInline",
            "StreamUnchecked",
            "GetUnchecked",
            "StreamLocals",
            "GetLocals",
        ];
        var entries = withRows
            .Select(name => engine.Entry(file, name, id, columns))
            .Append(engine.Entry(file, "CountFrom", id, null))
            .Append(engine.Entry(file, "TouchFrom", id, null))
            .ToArray();
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [file, Sidecars.Of(file, entries)]
        );
    }
}
