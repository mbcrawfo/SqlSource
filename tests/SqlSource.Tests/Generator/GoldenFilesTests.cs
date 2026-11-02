// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.IO;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// How the golden corpus renders a run and checks it against its file, without running the generator.  The checks use
// a temporary folder and never touch Golden/.
public sealed class GoldenFilesTests
{
    private const string Header =
        "// The golden output of the query-method corpus case postgres/shapes.  Written by SQLSOURCE_UPDATE_GOLDEN=1: "
        + "see \"The golden corpus of query methods\" in tests/README.md.\n";

    [Fact]
    public void Render_RunWithFilesAndDiagnostics_WritesTheSectionsInOrder() =>
        GoldenFiles
            .Render(
                "postgres/shapes",
                new Dictionary<string, string> { ["b.g.cs"] = "B\n", ["a.g.cs"] = "A\n" },
                ["SQLSRC275 /app/Repo/Users.sql(1,10)-(1,14): m"],
                [],
                []
            )
            .ShouldBe(
                Header
                    + "\n==== Generator diagnostics\nSQLSRC275 /app/Repo/Users.sql(1,10)-(1,14): m\n"
                    + "\n==== Compilation errors\n(none)\n"
                    + "\n==== Warnings in generated code\n(none)\n"
                    + "\n==== File: a.g.cs\nA\n"
                    + "\n==== File: b.g.cs\nB\n"
            );

    // A file that ends without a newline must not render as the same file with one.
    [Fact]
    public void Render_FileWithoutFinalNewline_MarksIt() =>
        GoldenFiles
            .Render("postgres/shapes", new Dictionary<string, string> { ["a.g.cs"] = "A" }, [], [], [])
            .ShouldEndWith("\n==== File: a.g.cs\nA\n\\ No newline at end of file\n");

    // The compiler writes the path of a diagnostic with the separator of the machine that runs it.
    [Fact]
    public void Render_DiagnosticWithBackslashPath_WritesSlashes() =>
        GoldenFiles
            .Render(
                "postgres/shapes",
                new Dictionary<string, string>(),
                [],
                ["CS0001 SqlSource\\Gen\\a.g.cs(1,1)-(1,2): x"],
                []
            )
            .ShouldContain("\n==== Compilation errors\nCS0001 SqlSource/Gen/a.g.cs(1,1)-(1,2): x\n");

    [Fact]
    public void Check_SameText_Passes() =>
        InTemporaryFolder(folder =>
        {
            var path = Path.Combine(folder, "case.txt");
            File.WriteAllText(path, "x\ny\n");

            GoldenFiles.Check(path, "x\ny\n", updating: false);
        });

    // A checkout that writes CRLF still compares equal.
    [Fact]
    public void Check_GoldenWithCrlf_Passes() =>
        InTemporaryFolder(folder =>
        {
            var path = Path.Combine(folder, "case.txt");
            File.WriteAllText(path, "x\r\ny\r\n");

            GoldenFiles.Check(path, "x\ny\n", updating: false);
        });

    [Fact]
    public void Check_DifferentText_FailsNamingTheFirstDifferentLine() =>
        InTemporaryFolder(folder =>
        {
            var path = Path.Combine(folder, "case.txt");
            File.WriteAllText(path, "a\nb\nc\n");

            var exception = Should.Throw<ShouldAssertException>(() =>
                GoldenFiles.Check(path, "a\nB\nc\n", updating: false)
            );

            exception.Message.ShouldContain("line 2");
            exception.Message.ShouldContain("b");
            exception.Message.ShouldContain("B");
            exception.Message.ShouldContain(GoldenFiles.UpdateVariable);
            File.ReadAllText(path).ShouldBe("a\nb\nc\n");
        });

    [Fact]
    public void Check_MissingGoldenWithoutOptIn_FailsAndWritesNothing() =>
        InTemporaryFolder(folder =>
        {
            var path = Path.Combine(folder, "case.txt");

            var exception = Should.Throw<ShouldAssertException>(() => GoldenFiles.Check(path, "x\n", updating: false));

            exception.Message.ShouldContain(path);
            exception.Message.ShouldContain(GoldenFiles.UpdateVariable);
            File.Exists(path).ShouldBeFalse();
        });

    [Fact]
    public void Check_UpdatingWithDifferentText_WritesTheGolden() =>
        InTemporaryFolder(folder =>
        {
            var path = Path.Combine(folder, "case.txt");
            File.WriteAllText(path, "old\n");

            GoldenFiles.Check(path, "new\n", updating: true);

            File.ReadAllText(path).ShouldBe("new\n");
        });

    [Fact]
    public void Check_UpdatingWithMissingFolder_CreatesIt() =>
        InTemporaryFolder(folder =>
        {
            var path = Path.Combine(folder, "postgres", "case.txt");

            GoldenFiles.Check(path, "new\n", updating: true);

            File.ReadAllText(path).ShouldBe("new\n");
        });

    private static void InTemporaryFolder(System.Action<string> test)
    {
        var folder = Directory.CreateTempSubdirectory("sqlsource-golden-").FullName;
        try
        {
            test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
