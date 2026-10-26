using System;
using System.Linq;
using System.Threading;
using Shouldly;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Chains;

public sealed class SettingsFileSetTests
{
    private const string App = "/app/sqlsource.json";

    private const string Queries = "/app/Queries/sqlsource.json";

    private const string QueryPath = "app/Queries/a.sql";

    private const string NotJson = "{";

    [Fact]
    public void ChainOf_NearerFile_OverridesFartherSettingBySetting()
    {
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"output": "sql", "inputModelSuffix": "In"}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"output": "models"}"""
            )
        );

        var chain = set.ChainOf(QueryPath);

        chain.Level.Output.ShouldBe(OutputKind.Models);
        chain.Level.InputModelSuffix.ShouldBe("In");
    }

    [Fact]
    public void ChainOf_RootFile_EndsTheChain()
    {
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"output": "sql", "inputModelSuffix": "In"}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"root": true, "output": "models"}"""
            )
        );

        var chain = set.ChainOf(QueryPath);

        chain.Level.Output.ShouldBe(OutputKind.Models);
        chain.Level.InputModelSuffix.ShouldBeNull();
    }

    // A root file that sets nothing else ends the chain with nothing: the file above gives its folder neither the
    // output nor the dialect, and the folder shares the chain of a folder with no file.
    [Fact]
    public void ChainOf_RootFileThatSaysNothingElse_EndsTheChainWithNone()
    {
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"output": "sql", "dialect": "postgres"}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"root": true}"""
            )
        );

        set.ChainOf("app/Queries/a.sql").ShouldBeSameAs(SettingsChain.None);
    }

    [Fact]
    public void ChainOf_FileThatIsNotJson_IsPassedThrough()
    {
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"output": "sql"}"""
            ),
            (Queries, NotJson)
        );

        set.ChainOf(QueryPath).Level.Output.ShouldBe(OutputKind.Sql);
    }

    [Fact]
    public void ChainOf_RootFalse_GoesOn()
    {
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"output": "sql"}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"root": false, "inputModelSuffix": "In"}"""
            )
        );

        var chain = set.ChainOf(QueryPath);

        chain.Level.Output.ShouldBe(OutputKind.Sql);
        chain.Level.InputModelSuffix.ShouldBe("In");
    }

    [Fact]
    public void ChainOf_Dialect_IsTheNearestFilesThatSetsOne()
    {
        var set = Set(
            (
                "/sqlsource.json", /*lang=json,strict*/
                """{"dialect": "mssql"}"""
            ),
            (
                App, /*lang=json,strict*/
                """{"dialect": "postgres"}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"output": "sql"}"""
            )
        );

        set.ChainOf(QueryPath).Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.PostgreSql, SqlDialectOptions.None));
        set.ChainOf("other/a.sql").Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.SqlServer, SqlDialectOptions.None));
    }

    [Fact]
    public void ChainOf_FolderWithNoFileAbove_IsNone()
    {
        var set = Set(
            (
                Queries, /*lang=json,strict*/
                """{"output": "sql"}"""
            )
        );

        set.ChainOf("app/a.sql").ShouldBeSameAs(SettingsChain.None);
        set.ChainOf("other/Queries/a.sql").ShouldBeSameAs(SettingsChain.None);
        set.ChainOf("a.sql").ShouldBeSameAs(SettingsChain.None);
        SettingsFileSet.Empty.ChainOf(QueryPath).ShouldBeSameAs(SettingsChain.None);
    }

    [Fact]
    public void ChainOf_FilesOfOneFolder_ShareOneChain()
    {
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"output": "sql"}"""
            )
        );

        var first = set.ChainOf("app/Queries/a.sql");

        set.ChainOf("app/Queries/b.sql").ShouldBeSameAs(first);
        set.ChainOf("app/Repo/Deep/c.sql").ShouldBeSameAs(first);
        set.ChainOf("app/d.sql").ShouldBeSameAs(first);
    }

    [Fact]
    public void ChainOf_PathInAnotherCaseOrWithBackslashes_FindsTheChain()
    {
        var set = Set(
            (
                @"C:\App\sqlsource.json", /*lang=json,strict*/
                """{"output": "sql"}"""
            )
        );

        var path = SqlPath.Normalize(@"c:\app\Queries\a.sql").ShouldNotBeNull();

        set.ChainOf(path).Level.Output.ShouldBe(OutputKind.Sql);
    }

    [Fact]
    public void ChainOf_FileAtTheRoot_AppliesToEveryFolder()
    {
        var set = Set(
            (
                "/sqlsource.json", /*lang=json,strict*/
                """{"output": "sql", "inputModelSuffix": "In"}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"output": "models"}"""
            )
        );

        set.ChainOf("a.sql").Level.Output.ShouldBe(OutputKind.Sql);
        set.ChainOf("other/Deep/a.sql").Level.Output.ShouldBe(OutputKind.Sql);
        var queries = set.ChainOf(QueryPath);
        queries.Level.Output.ShouldBe(OutputKind.Models);
        queries.Level.InputModelSuffix.ShouldBe("In");
    }

    [Fact]
    public void ChainOf_FolderWhoseNameSortsBetweenItsParentAndItsSubfolders_FindsTheParent()
    {
        // '-' and '.' sort before the separator, so app-tests and app.old come between app and app/Queries.
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"inputModelSuffix": "In"}"""
            ),
            (
                "/app-tests/sqlsource.json", /*lang=json,strict*/
                """{"inputModelSuffix": "Tests"}"""
            ),
            (
                "/app.old/sqlsource.json", /*lang=json,strict*/
                """{"inputModelSuffix": "Old"}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"output": "sql"}"""
            )
        );

        var chain = set.ChainOf(QueryPath);

        chain.Level.Output.ShouldBe(OutputKind.Sql);
        chain.Level.InputModelSuffix.ShouldBe("In");
        set.ChainOf("app-tests/a.sql").Level.InputModelSuffix.ShouldBe("Tests");
    }

    [Fact]
    public void ChainOf_FoldersNotInAscii_AreFoundInTheOrderTheyAreSorted()
    {
        // Ä sorts after every ASCII letter, and äpp is Äpp's folder, ignoring case.
        var set = Set(
            (
                "/Äpp/sqlsource.json", /*lang=json,strict*/
                """{"inputModelSuffix": "Upper"}"""
            ),
            (
                "/äpp/Queries/sqlsource.json", /*lang=json,strict*/
                """{"output": "sql"}"""
            ),
            (
                "/Zed/sqlsource.json", /*lang=json,strict*/
                """{"inputModelSuffix": "Zed"}"""
            )
        );

        set.Files.Select(static file => file.NormalizedFolder).ShouldBe(["Zed", "Äpp", "äpp/Queries"]);
        set.Parents.ShouldBe([-1, -1, 1]);
        var queries = set.ChainOf("ÄPP/queries/a.sql");
        queries.Level.Output.ShouldBe(OutputKind.Sql);
        queries.Level.InputModelSuffix.ShouldBe("Upper");
        set.ChainOf("äpp/b.sql").Level.InputModelSuffix.ShouldBe("Upper");
        set.ChainOf("zed/c.sql").Level.InputModelSuffix.ShouldBe("Zed");
    }

    [Fact]
    public void ChainOf_PathFarBelowItsNearestFile_AllocatesNothing()
    {
        // The generator looks up the chain of every .sql file, and the nested class of every type, on each change.
        const int Iterations = 20;
        const string Path = "app/Queries/One/Two/Three/Four/a.sql";
        var set = Set(
            ("/sqlsource.json", "{}"),
            (
                App, /*lang=json,strict*/
                """{"output": "sql"}"""
            ),
            ("/app-tests/sqlsource.json", "{}"),
            ("/other/sqlsource.json", "{}")
        );
        set.ChainOf(Path).Level.Output.ShouldBe(OutputKind.Sql);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = set.ChainOf(Path);
            _ = set.ChainOf("elsewhere/Deep/b.sql");
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
    }

    [Fact]
    public void Of_FolderListedTwice_KeepsTheFirst()
    {
        var set = Set(
            (
                App, /*lang=json,strict*/
                """{"output": "sql"}"""
            ),
            (
                "/App/sqlsource.json", /*lang=json,strict*/
                """{"output": "models"}"""
            ),
            (
                App, /*lang=json,strict*/
                """{"output": "models"}"""
            )
        );

        set.Files.ShouldHaveSingleItem().Path.ShouldBe(App);
        set.ChainOf(QueryPath).Level.Output.ShouldBe(OutputKind.Sql);
    }

    [Fact]
    public void Of_Files_AreSortedByFolderWithTheirParents()
    {
        var set = Set(
            (Queries, "{}"),
            ("/app.old/sqlsource.json", "{}"),
            ("/sqlsource.json", "{}"),
            (App, "{}"),
            ("/other/sqlsource.json", "{}")
        );

        set.Files.Select(static file => file.NormalizedFolder).ShouldBe(["", "app", "app.old", "app/Queries", "other"]);
        set.Parents.ShouldBe([-1, 0, 0, 1, 0]);
        set.Chains.Count.ShouldBe(5);
    }

    [Fact]
    public void ChainOf_FileEditedToTextThatIsNotJson_PassesThrough()
    {
        const string Farther = /*lang=json,strict*/
            """{"output": "sql", "inputModelSuffix": "In"}""";
        var before = Set(
            (App, Farther),
            (
                Queries, /*lang=json,strict*/
                """{"root": true, "output": "models"}"""
            )
        );
        var after = Set((App, Farther), (Queries, """{"root": true, "output": "models" """));

        before.ChainOf(QueryPath).Level.Output.ShouldBe(OutputKind.Models);
        before.ChainOf(QueryPath).Level.InputModelSuffix.ShouldBeNull();
        after.ChainOf(QueryPath).Level.Output.ShouldBe(OutputKind.Sql);
        after.ChainOf(QueryPath).Level.InputModelSuffix.ShouldBe("In");
    }

    [Fact]
    public void Of_SameFiles_AreEqual()
    {
        (string, string)[] files =
        [
            (
                App, /*lang=json,strict*/
                """{"output": "sql", "typeOverrides": {"int4": "long"}}"""
            ),
            (
                Queries, /*lang=json,strict*/
                """{"dialekt": "postgres"}"""
            ),
        ];

        var first = Set(files);
        var second = Set(files);

        second.ShouldNotBeSameAs(first);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Of_NoFile_IsEmpty()
    {
        var set = SettingsFileSet.Of([]);

        set.ShouldBe(SettingsFileSet.Empty);
        set.Reached([QueryPath]).ShouldBeEmpty();
    }

    [Fact]
    public void Reached_FileBeyondEveryRoot_IsNotReached()
    {
        var set = Set(
            ("/sqlsource.json", NotJson),
            (
                App, /*lang=json,strict*/
                """{"root": true}"""
            )
        );

        set.Reached([QueryPath, "app/b.sql"]).ShouldBe(["app"]);
    }

    [Fact]
    public void Reached_FileReachedThroughASiblingFolder_IsReached()
    {
        var set = Set(
            ("/a/sqlsource.json", "{}"),
            (
                "/a/b/sqlsource.json", /*lang=json,strict*/
                """{"root": true}"""
            )
        );

        set.Reached(["a/b/x.sql"]).ShouldBe(["a/b"]);
        set.Reached(["a/b/x.sql", "a/c/y.sql"]).ShouldBe(["a", "a/b"]);
    }

    [Fact]
    public void Reached_FileWithNoPathBelow_IsNotReached()
    {
        var set = Set(("/a/sqlsource.json", "{}"), ("/b/sqlsource.json", "{}"));

        set.Reached(["a/x.sql", "c/y.sql"]).ShouldBe(["a"]);
    }

    [Fact]
    public void Reached_FileWhoseRootIsUnknown_LetsTheChainGoOn()
    {
        var set = Set(("/sqlsource.json", "{}"), (App, NotJson));

        set.Reached([QueryPath]).ShouldBe(["", "app"]);
    }

    [Fact]
    public void FilesIn_FoldersThatAreReached_AreTheirFilesInTheOrderOfTheSet()
    {
        var set = Set(
            ("/c/sqlsource.json", "{}"),
            (
                "/a/b/sqlsource.json", /*lang=json,strict*/
                """{"root": true}"""
            ),
            ("/b/sqlsource.json", "{}"),
            ("/a/sqlsource.json", "{}")
        );

        set.FilesIn(set.Reached(["c/y.sql", "a/b/x.sql"]))
            .Select(static file => file.Path)
            .ShouldBe(["/a/b/sqlsource.json", "/c/sqlsource.json"]);
    }

    [Fact]
    public void FilesIn_NoFolder_IsNoFile()
    {
        var set = Set(("/a/sqlsource.json", "{}"));

        set.FilesIn(EquatableArray<string>.Empty).ShouldBeEmpty();
    }

    private static SettingsFileSet Set(params (string Path, string Text)[] files) =>
        SettingsFileSet.Of(
            files.Select(static file =>
                ListedSettingsFile
                    .Read(
                        new InMemoryAdditionalText(file.Path, file.Text),
                        SettingsFileSide.Build,
                        CancellationToken.None
                    )
                    .ShouldNotBeNull()
            )
        );
}
