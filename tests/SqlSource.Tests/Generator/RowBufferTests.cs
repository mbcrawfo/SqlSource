// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The buffer a many method reads its rows into when it returns an array, a list, an immutable array or one of their
// interfaces: written once for the project, in a file of its own, when a method it keeps uses it, and named from
// global:: in each method.  Each run is at Methods under postgres on Npgsql 10, and every method must compile without
// a warning.
public sealed class RowBufferTests
{
    private const string Struct = "struct SqlSourceRowBuffer";

    private const string NewBuffer = "new global::SqlSource.SqlSourceRowBuffer<";

    private const string Select = "SELECT id, name FROM users;\n";

    private const string APath = "/app/Repo/A.sql";

    private const string BPath = "/app/Repo/B.sql";

    // Two types of one namespace, each claiming a file of its own.
    private const string TwoTypesSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = "A.sql")]
            public partial class First;

            [SqlSourceGenerate(Source = "B.sql")]
            public partial class Second;
        }
        """;

    private static readonly SqlFile ListUsers = new(GeneratorHarness.UsersPath, "-- name: ListUsers\n" + Select);

    [Theory]
    [InlineData("Array", "ToArray")]
    [InlineData("IEnumerable", "ToArray")]
    [InlineData("IReadOnlyCollection", "ToArray")]
    [InlineData("IReadOnlyList", "ToArray")]
    [InlineData("List", "ToList")]
    [InlineData("IList", "ToList")]
    [InlineData("ICollection", "ToList")]
    [InlineData("ImmutableArray", "ToImmutableArray")]
    public void Run_ManyMethodOfEachPooledType_ReadsItsRowsIntoTheBuffer(string collection, string result)
    {
        var run = RunListUsers("-- collection-type: " + collection + "\n");

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("var rows = new global::SqlSource.SqlSourceRowBuffer<global::App.ListUsersDto>();\n");
        methods.ShouldContain("return rows." + result + "();\n");
        methods.ShouldNotContain(Struct);
        run.Sources[RowBuffer.HintName].ShouldBe(RowBuffer.Source);
    }

    [Theory]
    [InlineData("Array", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows.ToArray()")]
    [InlineData(
        "IEnumerable",
        "new global::System.Collections.Generic.List<global::App.ListUsersDto>()",
        "rows.ToArray()"
    )]
    [InlineData(
        "IReadOnlyCollection",
        "new global::System.Collections.Generic.List<global::App.ListUsersDto>()",
        "rows.ToArray()"
    )]
    [InlineData(
        "IReadOnlyList",
        "new global::System.Collections.Generic.List<global::App.ListUsersDto>()",
        "rows.ToArray()"
    )]
    [InlineData("List", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows")]
    [InlineData("IList", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows")]
    [InlineData("ICollection", "new global::System.Collections.Generic.List<global::App.ListUsersDto>()", "rows")]
    [InlineData(
        "ImmutableArray",
        "global::System.Collections.Immutable.ImmutableArray.CreateBuilder<global::App.ListUsersDto>()",
        "rows.DrainToImmutable()"
    )]
    public void Run_ManyMethodOfEachPooledTypeUnderArrayPoolOff_ReadsItsRowsAsBeforeThePool(
        string collection,
        string buffer,
        string result
    )
    {
        var run = RunListUsers("-- collection-type: " + collection + "\n-- generator: array-pool=off\n");

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("var rows = " + buffer + ";\n");
        methods.ShouldContain("return " + result + ";\n");
        methods.ShouldNotContain("SqlSourceRowBuffer");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    // One of each pooled type in one file: the buffer is written once, and every member of it, ToList and
    // ToImmutableArray among them, compiles beside a method of each pooled type.
    [Fact]
    public void Run_MethodOfEveryPooledType_CompilesWithOneBufferAndNoWarning()
    {
        string[] collections =
        [
            "Array",
            "IEnumerable",
            "IReadOnlyCollection",
            "IReadOnlyList",
            "List",
            "IList",
            "ICollection",
            "ImmutableArray",
        ];
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            string.Join(
                "\n",
                collections.Select(collection =>
                    "-- name: List" + collection + "\n-- collection-type: " + collection + "\n" + Select
                )
            )
        );

        var run = MethodRuns.Run(file, [.. collections.Select(collection => Users(file, "List" + collection))]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldNotContain(Struct);
        Count(methods, NewBuffer).ShouldBe(collections.Length);
        run.Sources[RowBuffer.HintName].ShouldBe(RowBuffer.Source);
    }

    // Two types, each with a pooled method in a file of methods of its own, share the project's one buffer.
    [Fact]
    public void Run_PooledMethodsInSeveralFiles_WriteOneBufferFile()
    {
        var a = new SqlFile(APath, "-- name: ListFirst\n" + Select);
        var b = new SqlFile(BPath, "-- name: ListSecond\n-- collection-type: List\n" + Select);

        var run = MethodRuns.Run(
            TwoTypesSource,
            a,
            Sidecars.Of(a, Users(a, "ListFirst")),
            b,
            Sidecars.Of(b, Users(b, "ListSecond"))
        );

        run.ShouldBeClean();
        run.Sources.Keys.Count(key => key == RowBuffer.HintName).ShouldBe(1);
        run.Sources[RowBuffer.HintName].ShouldBe(RowBuffer.Source);
        foreach (var name in new[] { "App.First-Methods.g.cs", "App.Second-Methods.g.cs" })
        {
            run.Sources[name].ShouldNotContain(Struct);
            Count(run.Sources[name], NewBuffer).ShouldBe(1);
        }
    }

    [Fact]
    public void Run_ProjectWithNoPooledMethod_HasNoBufferFile()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser -> one\n"
                + Select
                + "\n-- name: RenameUsers -> rowcount\nUPDATE users SET name = name;\n"
                + "\n-- name: ListUsers\n-- collection-type: ImmutableList\n"
                + Select
        );

        var run = MethodRuns.Run(
            file,
            [Users(file, "GetUser"), Entry(file, "RenameUsers", [], null), Users(file, "ListUsers")]
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldNotContain("SqlSourceRowBuffer");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    // A many method of IAsyncEnumerable yields its rows one at a time, so it uses no buffer.
    [Fact]
    public void Run_EveryManyMethodStreams_WritesNoRowBuffer()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: StreamUsers\n-- collection-type: IAsyncEnumerable\n" + Select
        );

        var run = MethodRuns.Run(file, [Users(file, "StreamUsers")]);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldNotContain("SqlSourceRowBuffer");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    [Fact]
    public void Run_StreamingBesideAnArrayMethod_WritesTheRowBufferOnce()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: StreamUsers\n-- collection-type: IAsyncEnumerable\n" + Select + "\n-- name: ListUsers\n" + Select
        );

        var run = MethodRuns.Run(file, [Users(file, "StreamUsers"), Users(file, "ListUsers")]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
        Count(methods, NewBuffer).ShouldBe(1);
        // The one buffer is read by the array method, which follows the streaming method in the file.
        var streaming = methods.IndexOf(" StreamUsers(\n", StringComparison.Ordinal);
        var array = methods.IndexOf(" ListUsers(\n", StringComparison.Ordinal);
        streaming.ShouldBeGreaterThan(-1);
        array.ShouldBeGreaterThan(streaming);
        methods[streaming..array].ShouldNotContain("SqlSourceRowBuffer");
        methods[array..].ShouldContain(NewBuffer);
    }

    [Fact]
    public void Run_FileMixingPooledAndArrayPoolOffMethods_PoolsOnlyThePooled()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListUsers\n" + Select + "\n-- name: ListOthers\n-- generator: array-pool=off\n" + Select
        );

        var run = MethodRuns.Run(file, [Users(file, "ListUsers"), Users(file, "ListOthers")]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
        Count(methods, "var rows = " + NewBuffer).ShouldBe(1);
        Count(methods, "var rows = new global::System.Collections.Generic.List<").ShouldBe(1);
    }

    // The one pooled method clashes with the second type's and is dropped, so no method the project keeps uses the
    // buffer, though the first type keeps a method that reads one row.
    [Fact]
    public void Run_PooledMethodDroppedForAConflict_IsNoReasonForTheBuffer()
    {
        var a = new SqlFile(APath, "-- name: Get\n" + Select + "\n-- name: Find -> one\n" + Select);
        var b = new SqlFile(BPath, "-- name: Get -> one\n" + Select);

        var run = MethodRuns.Run(
            TwoTypesSource,
            a,
            Sidecars.Of(a, Users(a, "Get"), Users(a, "Find")),
            b,
            Sidecars.Of(b, Users(b, "Get"))
        );

        run.Problems().Count(problem => problem.StartsWith("SQLSRC274 ", StringComparison.Ordinal)).ShouldBe(2);
        run.Sources["App.First-Methods.g.cs"].ShouldContain(" Find(\n");
        run.Sources["App.First-Methods.g.cs"].ShouldNotContain(" Get(\n");
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    // A project on a framework older than .NET 8, or a language older than C# 12, gets no type's methods, so its pooled
    // many query is no reason for the buffer.
    [Theory]
    [InlineData(false, LanguageVersion.CSharp12, "SQLSRC003")]
    [InlineData(true, LanguageVersion.CSharp11, "SQLSRC012")]
    public void Run_UnsupportedFrameworkWithAPooledManyQuery_HasNoBufferFile(
        bool supportedFramework,
        LanguageVersion languageVersion,
        string id
    )
    {
        // Without the runtime's references, as in the first row, the compiler cannot read a string argument, so the
        // type takes the file of its name.
        var source = MethodRuns.Source.Replace("(Source = \".\")", string.Empty, StringComparison.Ordinal);
        var listUsers = ListUsers with { Path = "/app/Repo/Sample.sql" };
        var run = GeneratorHarness.Run(
            OutputKind.Methods,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [listUsers, Sidecars.Of(listUsers, Users(listUsers, "ListUsers"))],
            supportedFramework: supportedFramework,
            languageVersion: languageVersion,
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith(id + " ");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    [Theory]
    [InlineData("ExtensionClass", false)]
    [InlineData("Public", false)]
    [InlineData("Internal", false)]
    [InlineData("Private", false)]
    [InlineData("ExtensionClass", true)]
    [InlineData("Public", true)]
    [InlineData("Internal", true)]
    [InlineData("Private", true)]
    public void Run_EachPlacement_CompilesWithTheBuffer(string placement, bool nested)
    {
        const string Attribute = "[SqlSourceGenerate(Source = \".\")]";
        var source = nested
            ? "using SqlSource;\n\nnamespace App\n{\n    public partial class Outer<TKey>\n    {\n        "
                + Attribute
                + "\n        public partial class Sample;\n    }\n}\n"
            : "using SqlSource;\n\nnamespace App\n{\n    " + Attribute + "\n    public partial class Sample;\n}\n";

        // Every visibility but the extension class's puts the method on the type.
        var file =
            placement == "ExtensionClass"
                ? ListUsers
                : ListUsers with
                {
                    Text = "-- member-location: class\n-- visibility: " + placement + "\n\n" + ListUsers.Text,
                };
        var run = MethodRuns.Run(file, [Users(file, "ListUsers")], source);

        run.ShouldBeClean();
        MethodsOf(run).ShouldContain(NewBuffer);
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
    }

    // The buffer is in the namespace SqlSource, so a type of the user's with its name, the methods on it, is no clash.
    [Fact]
    public void Run_TypeNamedLikeTheBuffer_CompilesBesideIt()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class SqlSourceRowBuffer<T>;
            }
            """;
        var file = ListUsers with { Text = "-- member-location: class\n-- visibility: public\n\n" + ListUsers.Text };

        var run = MethodRuns.Run(file, [Users(file, "ListUsers")], source);

        run.ShouldBeClean();
        MethodsOf(run).ShouldContain(NewBuffer);
        run.Sources.Keys.ShouldContain(RowBuffer.HintName);
    }

    // The global namespace, one under an escaped keyword, and one of the user's named SqlSource, below App, which would
    // hide the buffer's from a name that did not start at global::.
    [Theory]
    [InlineData("")]
    [InlineData("namespace @class.Rows;\n\n")]
    [InlineData("namespace App.SqlSource;\n\n")]
    public void Run_TypeInAnyNamespace_NamesTheBufferFromGlobal(string namespaceLine)
    {
        var source =
            "using SqlSource;\n\n"
            + namespaceLine
            + "[SqlSourceGenerate(Source = \".\")]\npublic partial class Sample;\n";

        var run = MethodRuns.Run(ListUsers, [Users(ListUsers, "ListUsers")], source);

        run.ShouldBeClean();
        MethodsOf(run).ShouldContain(NewBuffer);
    }

    // ListUsers on App.Sample, with these markers in its file header, before a blank line.
    private static GeneratorRun RunListUsers(string header)
    {
        var file = ListUsers with { Text = header + "\n" + ListUsers.Text };
        return MethodRuns.Run(file, [Users(file, "ListUsers")]);
    }

    // The entry of a query of the rows id and name.
    private static SidecarEntry Users(SqlFile file, string name) =>
        Entry(file, name, [], [Column("id"), Column("name", Text, nullable: true)]);

    // The one file of methods of the run.
    private static string MethodsOf(GeneratorRun run) =>
        run.Sources.Single(source => source.Key.EndsWith("-Methods.g.cs", StringComparison.Ordinal)).Value;

    private static int Count(string text, string part) =>
        Regex.Count(text, Regex.Escape(part), RegexOptions.None, TimeSpan.FromSeconds(1));
}
