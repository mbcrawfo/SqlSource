// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The combinations of member location, visibility and nested class that are errors, through the whole generator:
// where each is reported, that its file gives the type no members, and that the type's other files keep theirs.  No
// run leaves an error in the compilation.  PlacementTests has the combinations that are allowed.
public sealed class PlacementDiagnosticsTests
{
    // A file of the type that no error reaches, whose constant the type keeps.
    private static readonly SqlFile Kept = new("/app/Repo/Kept.sql", "-- name: Kept\n-- output: sql\nSELECT 9;\n");

    // The sql constant goes to a private nested class, and the codegen query, whose method goes to the extension
    // class by default, cannot be private there.  The message names the header and suggests a marker on the query.
    [Fact]
    public void Run_HeaderPrivateWithACodeGenQuery_ReportsSqlsrc123NamingTheHeader()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- visibility: private\n\n"
                + "-- name: GetName\n-- output: sql\nSELECT name FROM users;\n"
                + "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(file, [UserEntry(file)], otherFiles: [Kept]);

        run.Problems()
            .ShouldBe([
                "SQLSRC123 /app/Repo/Users.sql(6,10)-(6,17): The query 'GetUser' cannot be private, which the file "
                    + "header sets, at the member location extension-class, because an extension method is public or "
                    + "internal.  Write -- visibility: or -- member-location: on the query.",
            ]);
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldNotContain("GetName");
        type.ShouldContain("            public const string Kept = \"SELECT 9;\";\n");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // The fix the message suggests: the query's own visibility, which the header's does not reach.
    [Fact]
    public void Run_HeaderPrivateWithAVisibilityOnTheCodeGenQuery_PlacesBoth()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- visibility: private\n\n"
                + "-- name: GetName\n-- output: sql\nSELECT name FROM users;\n"
                + "-- name: GetUser\n-- visibility: internal\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(file, [UserEntry(file)]);

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain("        private static partial class Sql\n        {\n            /// <summary>\n");
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("            public const string GetName = ");
        run.Sources[MethodRuns.MethodsFile].ShouldContain("    internal static partial class SampleExtensions\n");
    }

    // A folder's settings file reaches every query below it, so each query at sql gets the error at its name, which
    // names the settings file.
    [Fact]
    public void Run_SettingsFileExtensionClassOverSqlQueries_ReportsSqlsrc122AtEachQuery()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.SampleSource("Source = \"../Repo\""),
            new SqlFile(GeneratorHarness.UsersPath, SqlTexts.TwoUsers),
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"memberLocation": "ExtensionClass"}"""
            )
        );

        run.Problems()
            .ShouldBe([
                Sqlsrc122("(1,10)-(1,17)", "GetUser", "the settings file '/app/Repo/sqlsource.json'"),
                Sqlsrc122("(4,10)-(4,19)", "ListUsers", "the settings file '/app/Repo/sqlsource.json'"),
            ]);
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser");
    }

    // A query's own marker is where the error is, and the type keeps the members of its other file.
    [Fact]
    public void Run_ExtensionClassMarkerOnASqlQuery_ReportsSqlsrc122AtTheMarker()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- name: GetUser\n-- member-location: extension-class\nSELECT 1;\n"
            ),
            Kept
        );

        run.Problems().ShouldBe([Sqlsrc122("(2,1)-(2,36)", "GetUser", "its own marker")]);
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldNotContain("GetUser");
        type.ShouldContain("        public const string Kept = \"SELECT 9;\";\n");
    }

    // Each combination that C# cannot write, at the marker that wrote the visibility, else at the location's marker,
    // else at the query's name.
    [Theory]
    [InlineData(
        "internal partial class Sample;",
        "-- name: GetUser\n-- visibility: public\nSELECT id FROM users WHERE id = @id;\n",
        "(2,1)-(2,22)",
        "public",
        "its own marker",
        "extension-class",
        "'App.Sample' is not public, so the models a public extension method takes are internal"
    )]
    [InlineData(
        "public partial class Sample;",
        "-- name: GetUser\n-- member-location: extension-class\n-- visibility: protected\n"
            + "SELECT id FROM users WHERE id = @id;\n",
        "(3,1)-(3,25)",
        "protected",
        "its own marker",
        "extension-class",
        "an extension method is public or internal"
    )]
    [InlineData(
        "public static partial class Sample;",
        "-- visibility: protected\n\n-- name: GetUser\n-- member-location: class\n"
            + "SELECT id FROM users WHERE id = @id;\n",
        "(4,1)-(4,26)",
        "protected",
        "the file header",
        "class",
        "'App.Sample' is a static class, which cannot have protected members"
    )]
    [InlineData(
        "public partial struct Sample;",
        "-- member-location: class\n-- visibility: protected\n\n-- name: GetUser\n"
            + "SELECT id FROM users WHERE id = @id;\n",
        "(4,10)-(4,17)",
        "protected",
        "the file header",
        "class",
        "'App.Sample' is a struct, which cannot have protected members"
    )]
    [InlineData(
        "public partial record struct Sample;",
        "-- name: GetUser\n-- member-location: nested-class\n-- visibility: protected\n"
            + "SELECT id FROM users WHERE id = @id;\n",
        "(3,1)-(3,25)",
        "protected",
        "its own marker",
        "nested-class",
        "'App.Sample' is a struct, which cannot have protected members"
    )]
    public void Run_VisibilityThatDoesNotFit_ReportsSqlsrc123(
        string declaration,
        string text,
        string span,
        string visibility,
        string place,
        string location,
        string reason
    )
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);

        var run = MethodRuns.Run(
            file,
            [UserEntry(file)],
            "using SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate(Source = \".\")]\n    "
                + declaration
                + "\n}\n",
            otherFiles: [Kept]
        );

        run.Problems()
            .ShouldBe([
                $"SQLSRC123 /app/Repo/Users.sql{span}: The query 'GetUser' cannot be {visibility}, which {place} sets, "
                    + $"at the member location {location}, because {reason}.  Write -- visibility: or "
                    + "-- member-location: on the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources.Values.ShouldContain(source => source.Contains(" const string Kept = \"SELECT 9;\";"));
    }

    // Both files write a visibility for the class Sql, and they differ, so neither file gives the type its members.
    // The class Other of a third file is not involved, and keeps its member.
    [Fact]
    public void Run_SharedNestedClassWithTwoWrittenVisibilities_ReportsSqlsrc124AndDropsBothFiles()
    {
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/A.sql", "-- visibility: public\n\n-- name: First\nSELECT 1;\n"),
            new SqlFile("/app/Repo/B.sql", "-- visibility: internal\n\n-- name: Second\nSELECT 2;\n"),
            new SqlFile("/app/Repo/C.sql", "-- nested-class: Other\n\n-- name: Third\nSELECT 3;\n")
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC124 /app/Repo/B.sql(3,10)-(3,16): The nested class 'Sql' of 'App.Sample' cannot be internal, "
                    + "which the file header sets for the query 'Second', since the file header sets it public for the "
                    + "query 'First'.  Write one visibility for the queries of the class, or write -- nested-class: or "
                    + "-- member-location: on the query.",
            ]);
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldNotContain("First");
        type.ShouldNotContain("Second");
        type.ShouldNotContain("class Sql");
        type.ShouldContain("        private static partial class Other\n");
        type.ShouldContain("            public const string Third = \"SELECT 3;\";\n");
    }

    // A constant on the type named like the nested class of another file's queries: the file of the constant gives
    // the type no members, and the other file keeps its class.
    [Fact]
    public void Run_MemberOnTheTypeNamedLikeANestedClass_ReportsSqlsrc125()
    {
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/A.sql", "-- name: Users\n-- member-location: class\nSELECT 1;\n"),
            new SqlFile("/app/Repo/B.sql", "-- nested-class: Users\n\n-- name: GetUser\nSELECT 2;\n")
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC125 /app/Repo/A.sql(1,10)-(1,15): The member of the query 'Users' cannot go on 'App.Sample', "
                    + "because a nested class of the type's queries has that name.  Rename the query, or write "
                    + "-- nested-class: or -- member-location: on the query.",
            ]);
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldNotContain("const string Users");
        type.ShouldContain("        private static partial class Users\n");
        type.ShouldContain("            public const string GetUser = \"SELECT 2;\";\n");
    }

    // A query method in a nested class cannot have the class's name, with Async after it under async-method-suffix,
    // since a member cannot have the name of the class that holds it.
    [Theory]
    [InlineData("-- name: Sql\n-- member-location: nested-class\nSELECT id FROM users;\n", "Sql", "(1,10)-(1,13)")]
    [InlineData(
        "-- name: Get\n-- member-location: nested-class\n-- nested-class: GetAsync\n"
            + "-- generator: async-method-suffix\nSELECT id FROM users;\n",
        "Get",
        "(1,10)-(1,13)"
    )]
    public void Run_NestedMethodNamedLikeItsClass_ReportsSqlsrc009(string text, string name, string span)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);

        var run = MethodRuns.Run(file, [Entry(file, name, [], [Column("id")])], otherFiles: [Kept]);

        run.Problems()
            .ShouldBe([
                $"SQLSRC009 /app/Repo/Users.sql{span}: A query of 'Sample' cannot be named '{name}', because its "
                    + "member would have the name of the type that contains it",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("            public const string Kept = ");
    }

    // The method of Get in the class Sql under async-method-suffix would take the name of the constant GetAsync,
    // which another file of the type puts in that class.
    [Fact]
    public void Run_NestedMethodNamedLikeAConstantOfItsClass_ReportsSqlsrc274()
    {
        var methods = new SqlFile(
            "/app/Repo/A.sql",
            "-- generator: async-method-suffix\n-- member-location: nested-class\n\n"
                + "-- name: Get\nSELECT id FROM users;\n"
        );
        var constants = new SqlFile("/app/Repo/B.sql", "-- name: GetAsync\n-- output: sql\nSELECT 1;\n");

        var run = MethodRuns.Run(
            MethodRuns.Source,
            methods,
            Sidecars.Of(methods, Entry(methods, "Get", [], [Column("id")])),
            constants
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/A.sql(4,10)-(4,13): The method of the query 'Get' cannot be named 'GetAsync', "
                    + "because the class of the queries 'Sql' of 'App.Sample' has a constant or method of that name.  "
                    + "Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("            public const string GetAsync = ");
    }

    // A file's header names its nested class, which a member of the type takes: that file gives the type no members,
    // and the file whose queries use the class Sql keeps its own.
    [Fact]
    public void Run_HeaderNestedClassTakenByAMember_ReportsSqlsrc007AndKeepsTheOtherFiles()
    {
        var run = GeneratorHarness.RunInNestedClass(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                public void Statements() { }
            }
            """,
            new SqlFile("/app/Repo/A.sql", "-- nested-class: Statements\n\n-- name: Taken\nSELECT 1;\n"),
            new SqlFile("/app/Repo/B.sql", "-- name: Free\nSELECT 2;\n")
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,33): 'Sample' already has a member named 'Statements', which a "
                    + "file of it names as its nested class.  Rename the member, or set -- nested-class: or "
                    + "-- member-location: in the file.",
            ]);
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldNotContain("Taken");
        type.ShouldContain("        private static partial class Sql\n");
        type.ShouldContain("            public const string Free = \"SELECT 2;\";\n");
    }

    // SQLSRC122 at a query at sql, set by the place.
    private static string Sqlsrc122(string span, string query, string place) =>
        $"SQLSRC122 {GeneratorHarness.UsersPath}{span}: The query '{query}' cannot go in the extension class, which "
        + $"{place} sets, because its output is sql and only a query method extends the connection.  Use class or "
        + "nested-class.  Write -- visibility: or -- member-location: on the query.";

    // The entry of GetUser, which takes an id and reads it, written for the file's hash.
    private static SidecarEntry UserEntry(SqlFile file) => Entry(file, "GetUser", [Parameter("id")], [Column("id")]);
}
