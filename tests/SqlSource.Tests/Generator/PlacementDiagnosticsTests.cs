// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The combinations of member location, visibility and nested class that are errors, through the whole generator:
// where each is reported, that its file gives the type no members, and that the type's other files keep theirs.  A
// query method refused for its models leaves out the method alone: its file keeps its other members and the query's
// models.  No run leaves an error in the compilation.  PlacementTests has the combinations that are allowed.
public sealed class PlacementDiagnosticsTests
{
    // The source of the internal type App.Sample, which claims every file of its folder.
    private const string InternalSample =
        "using SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate(Source = \".\")]\n"
        + "    internal partial class Sample;\n}\n";

    // A query at sql that ends a file, whose constant the file keeps when another query's method is refused for its
    // models.
    private const string GetName = "-- name: GetName\n-- output: sql\nSELECT name FROM users;\n";

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
                "SQLSRC123 /app/Repo/Users.sql(6,10)-(6,17): The query 'GetUser' of 'App.Sample' cannot be private, "
                    + "which the file header sets, at the member location extension-class, because an extension method "
                    + "is public or internal.  Write -- visibility: or -- member-location: on the query.",
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
                $"SQLSRC123 /app/Repo/Users.sql{span}: The query 'GetUser' of 'App.Sample' cannot be {visibility}, "
                    + $"which {place} sets, at the member location {location}, because {reason}.  Write "
                    + "-- visibility: or -- member-location: on the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources.Values.ShouldContain(source => source.Contains(" const string Kept = \"SELECT 9;\";"));
    }

    // A public extension method of a type that is not public would take or return its internal models (CS0051), so
    // the method is refused at the marker that wrote its visibility, whether it has an input model, an output model or
    // both.  Its file keeps its other members and the query's models.
    [Theory]
    [InlineData("SELECT id FROM users WHERE id = @id;", true, true, GetUserDtoFile)]
    [InlineData("DELETE FROM users WHERE id = @id;", true, false, "App.GetUserParams-Model.g.cs")]
    [InlineData("SELECT id FROM users;", false, true, GetUserDtoFile)]
    public void Run_PublicExtensionMethodWithAModelOfAnInternalType_ReportsSqlsrc123AndKeepsTheFile(
        string sql,
        bool parameters,
        bool rows,
        string modelsFile
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- visibility: public\n" + sql + "\n" + GetName
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetUser", parameters ? [Parameter("id")] : [], rows ? [Column("id")] : null)],
            InternalSample,
            otherFiles: [Kept]
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC123 /app/Repo/Users.sql(2,1)-(2,22): The query 'GetUser' of 'App.Sample' cannot be public, "
                    + "which its own marker sets, at the member location extension-class, because 'App.Sample' is not "
                    + "public, so its models are internal, which a public extension method cannot take or return.  "
                    + "Write -- visibility: or -- member-location: on the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources.Keys.ShouldContain(modelsFile);
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldContain(" const string GetName = ");
        type.ShouldContain(" const string Kept = \"SELECT 9;\";");
    }

    // A public extension method with no parameters and no rows takes no models, so it compiles on a type that is not
    // public, and makes the extension class public.
    [Fact]
    public void Run_PublicExtensionMethodWithNoModelsOfAnInternalType_Compiles()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- visibility: public\nDELETE FROM users;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "GetUser", [], null)], InternalSample);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("    public static partial class SampleExtensions\n");
        methods.ShouldContain(" public static async global::System.Threading.Tasks.Task<int> GetUser(");
    }

    // A derived type of another assembly reaches a type nested as protected or protected internal, with no level
    // around it internal, private or private protected, and so a public or protected method of it, which would take
    // the type's internal models (CS0051).  The type's own visibility, a nested class's and one written in the header
    // each give the error at the place that SQLSRC123 is reported at.
    [Theory]
    [InlineData("protected", "", "class", "public", "(3,1)-(3,22)")]
    [InlineData("protected", "", "class", "protected", "(3,1)-(3,25)")]
    [InlineData("protected", "", "nested-class", "public", "(3,1)-(3,22)")]
    [InlineData("protected", "", "nested-class", "protected", "(3,1)-(3,25)")]
    [InlineData("protected internal", "", "class", "public", "(3,1)-(3,22)")]
    [InlineData("protected internal", "", "class", "protected", "(3,1)-(3,25)")]
    [InlineData("protected internal", "", "nested-class", "public", "(3,1)-(3,22)")]
    [InlineData("protected internal", "", "nested-class", "protected", "(3,1)-(3,25)")]
    [InlineData("protected", "protected", "class", "public", "(3,1)-(3,22)")]
    [InlineData("public", "protected", "nested-class", "public", "(3,1)-(3,22)")]
    [InlineData("protected", "protected internal", "nested-class", "protected", "(3,1)-(3,25)")]
    public void Run_MethodOfATypeReachableAsProtected_ReportsSqlsrc123(
        string accessibility,
        string middle,
        string location,
        string visibility,
        string span
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            $"-- name: GetUser\n-- member-location: {location}\n-- visibility: {visibility}\n"
                + "SELECT id FROM users WHERE id = @id;\n"
                + GetName
        );

        var run = MethodRuns.Run(
            file,
            [UserEntry(file)],
            GeneratorHarness.NestedSampleSource("public", middle, accessibility),
            otherFiles: [Kept]
        );

        run.Problems().ShouldBe([Sqlsrc123AsProtected(span, middle, visibility, "its own marker", location)]);
        run.Sources.Keys.ShouldNotContain(source => source.EndsWith("-Methods.g.cs", StringComparison.Ordinal));
        run.Sources.Keys.ShouldContain(GetUserDtoFile);
        var type = run.Sources[middle.Length > 0 ? "App.Outer.Middle.Sample.g.cs" : "App.Outer.Sample.g.cs"];
        type.ShouldContain(" const string GetName = ");
        type.ShouldContain(" const string Kept = \"SELECT 9;\";");
    }

    // A query method with no parameters and no rows takes no models, so it compiles wherever a derived type of another
    // assembly reaches it.
    [Theory]
    [InlineData("class", "public", "")]
    [InlineData("nested-class", "public", "public static partial class Sql\n")]
    [InlineData("class", "protected", "")]
    public void Run_MethodWithNoModelsOfATypeReachableAsProtected_Compiles(
        string location,
        string visibility,
        string host
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            $"-- name: GetUser\n-- member-location: {location}\n-- visibility: {visibility}\nDELETE FROM users;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetUser", [], null)],
            GeneratorHarness.NestedSampleSource("public", "", "protected")
        );

        run.ShouldBeClean();
        var methods = run.Sources["App.Outer.Sample-Methods.g.cs"];
        methods.ShouldContain(host);
        // In a nested class the method is public, and the class has the visibility.
        var modifier = location == "class" ? visibility : "public";
        methods.ShouldContain(" " + modifier + " static async global::System.Threading.Tasks.Task<int> GetUser(");
    }

    // A query with models but no entry yet has the missing entry's error alone: its models are not known, so its
    // method is not refused for them until it is described.
    [Fact]
    public void Run_MethodOfATypeReachableAsProtectedWithoutAnEntry_ReportsNoSqlsrc123()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- member-location: class\n-- visibility: public\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(file, [], GeneratorHarness.NestedSampleSource("public", "", "protected"));

        run.Problems()
            .ShouldBe([
                "SQLSRC241 /app/Repo/Users.sql(1,10)-(1,17): The query 'GetUser' has no entry in the sidecar of its "
                    + "file, and its output 'codegen' needs one.  Run 'sqlsource describe'.",
            ]);
    }

    // The header's visibility reaches the query, whose own member location is where the error is; with neither on
    // the query, the error is at its name.
    [Theory]
    [InlineData(
        "-- visibility: public\n\n-- name: GetUser\n-- member-location: class\n",
        "(4,1)-(4,26)",
        "class",
        "public"
    )]
    [InlineData(
        "-- visibility: protected\n-- member-location: nested-class\n\n-- name: GetUser\n",
        "(4,10)-(4,17)",
        "nested-class",
        "protected"
    )]
    public void Run_HeaderVisibilityOfATypeReachableAsProtected_ReportsSqlsrc123AtTheQuery(
        string markers,
        string span,
        string location,
        string visibility
    )
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, markers + "SELECT id FROM users WHERE id = @id;\n");

        var run = MethodRuns.Run(
            file,
            [UserEntry(file)],
            GeneratorHarness.NestedSampleSource("public", "", "protected"),
            otherFiles: [Kept]
        );

        run.Problems().ShouldBe([Sqlsrc123AsProtected(span, "", visibility, "the file header", location)]);
        run.Sources.Keys.ShouldNotContain(source => source.EndsWith("-Methods.g.cs", StringComparison.Ordinal));
    }

    // Another file's header makes the class Sql public, which its constant can be, and the method of the query that
    // writes no visibility would be public in it too.  The error is at that query, whose file alone is left out, and
    // names the query that wrote the visibility.
    [Fact]
    public void Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- member-location: nested-class\n\n-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
        );
        var constants = new SqlFile(
            "/app/Repo/A.sql",
            "-- visibility: public\n-- output: sql\n\n-- name: GetName\nSELECT 1;\n"
        );

        var run = MethodRuns.Run(
            file,
            [UserEntry(file)],
            GeneratorHarness.NestedSampleSource("public", "", "protected"),
            otherFiles: [constants]
        );

        run.Problems()
            .ShouldBe([
                Sqlsrc123AsProtected(
                    "(3,10)-(3,17)",
                    "",
                    "public",
                    "the file header for the query 'GetName'",
                    "nested-class"
                ),
            ]);
        run.Sources.Keys.ShouldNotContain(source => source.EndsWith("-Methods.g.cs", StringComparison.Ordinal));
        run.Sources.Keys.ShouldContain(GetUserDtoFile);
        run.Sources["App.Outer.Sample.g.cs"].ShouldContain("            public static partial class Sql\n");
        run.Sources["App.Outer.Sample.g.cs"].ShouldContain("                public const string GetName = ");
    }

    // The marker that makes a nested class public is on the query that wrote it, so the refusal of another query's
    // method names that marker.
    [Fact]
    public void Run_NestedClassMadePublicByAMarkerOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMarker()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- member-location: nested-class\nSELECT id FROM users WHERE id = @id;\n"
        );
        var constants = new SqlFile(
            "/app/Repo/A.sql",
            "-- visibility: public\n-- output: sql\n\n-- name: GetName\nSELECT 1;\n"
        );

        var run = MethodRuns.Run(
            file,
            [UserEntry(file)],
            GeneratorHarness.NestedSampleSource("public", "", "protected"),
            otherFiles: [constants]
        );

        run.Problems()
            .ShouldBe([
                Sqlsrc123AsProtected(
                    "(2,1)-(2,33)",
                    "",
                    "public",
                    "the file header for the query 'GetName'",
                    "nested-class"
                ),
            ]);
    }

    // The same method with no parameters and no rows takes no models, so it is public in the public class Sql.
    [Fact]
    public void Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtectedWithNoModels_Compiles()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- member-location: nested-class\n\n-- name: GetUser\nDELETE FROM users;\n"
        );
        var constants = new SqlFile(
            "/app/Repo/A.sql",
            "-- visibility: public\n-- output: sql\n\n-- name: GetName\nSELECT 1;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetUser", [], null)],
            GeneratorHarness.NestedSampleSource("public", "", "protected"),
            otherFiles: [constants]
        );

        run.ShouldBeClean();
        var methods = run.Sources["App.Outer.Sample-Methods.g.cs"];
        methods.ShouldContain("            public static partial class Sql\n");
        methods.ShouldContain(" public static async global::System.Threading.Tasks.Task<int> GetUser(");
        run.Sources["App.Outer.Sample.g.cs"].ShouldContain("                public const string GetName = ");
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
                    + "-- member-location: on it.",
            ]);
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldNotContain("const string Users");
        type.ShouldContain("        private static partial class Users\n");
        type.ShouldContain("            public const string GetUser = \"SELECT 2;\";\n");
    }

    // A constant or a token method on the type cannot take a name the type declares: a method, which a token method
    // would otherwise overload, a nested class or a type parameter.  The file gives the type no members, and the
    // type's other file keeps its constant.  The last row gives the location in the settings file of the files'
    // folder, over the project's nested class.
    [Theory]
    [InlineData("Get", "-- member-location: class\n", "(2,10)-(2,13)", false)]
    [InlineData("Get", "-- member-location: class\n", "(2,10)-(2,13)", true)]
    [InlineData("Inner", "-- member-location: class\n", "(2,10)-(2,15)", false)]
    [InlineData("T", "-- member-location: class\n", "(2,10)-(2,11)", false)]
    [InlineData("Get", "", "(1,10)-(1,13)", false)]
    public void Run_MemberOnTheTypeNamedLikeWhatTheTypeDeclares_ReportsSqlsrc125(
        string name,
        string marker,
        string span,
        bool withToken
    )
    {
        var text = marker + "-- name: " + name + "\nSELECT 1" + (withToken ? " {{t}}" : string.Empty) + ";\n";
        SqlFile[] files =
            marker.Length > 0
                ? [new SqlFile("/app/Repo/A.sql", text), Kept]
                :
                [
                    new SqlFile("/app/Repo/A.sql", text),
                    Kept,
                    GeneratorHarness.SettingsFile(
                        "/app/Repo/sqlsource.json", /*lang=json,strict*/
                        """{"memberLocation": "class"}"""
                    ),
                ];

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;
                    namespace App;
                    [SqlSourceGenerate(Source = ".")]
                    public partial class Sample<T>
                    {
                        public static string Get() => "";

                        public static class Inner { }
                    }
                    """
                ),
            ],
            files,
            settings: GeneratorHarness.InNestedClass
        );

        run.Problems()
            .ShouldBe([
                $"SQLSRC125 /app/Repo/A.sql{span}: The member of the query '{name}' cannot go on 'App.Sample<T>', "
                    + "because the type declares a member, a nested type or a type parameter of that name.  Rename "
                    + "the query, or write -- member-location: on it.",
            ]);
        var type = run.Sources["App.Sample-1.g.cs"];
        type.ShouldNotContain(" " + name + " =");
        type.ShouldNotContain(" " + name + "(");
        type.ShouldContain(" const string Kept = \"SELECT 9;\";\n");
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

    // Two types claim one file, so each reports the file's one error at one place.  The messages name the type, which
    // is all that tells the two lines apart.
    [Fact]
    public void Run_TwoTypesClaimingAFileWithSqlsrc122_NamesEachType()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = "Users.sql")]
                public partial class Sample;

                [SqlSourceGenerate(Source = "Users.sql")]
                public partial class Other;
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- member-location: extension-class\n\n-- name: CountUsers\n-- output: sql\nSELECT count(*) FROM users;\n"
        );

        var run = GeneratorHarness.Run(OutputKind.Sql, Source, file);

        var problems = run.Problems();
        problems.Distinct().Count().ShouldBe(2);
        problems
            .Order(StringComparer.Ordinal)
            .ShouldBe([
                "SQLSRC122 /app/Repo/Users.sql(3,10)-(3,20): The query 'CountUsers' of 'App.Other' cannot go in the "
                    + "extension class, which the file header sets, because its output is sql and only a query method "
                    + "extends the connection.  Use class or nested-class.  Write -- visibility: or "
                    + "-- member-location: on the query.",
                "SQLSRC122 /app/Repo/Users.sql(3,10)-(3,20): The query 'CountUsers' of 'App.Sample' cannot go in the "
                    + "extension class, which the file header sets, because its output is sql and only a query method "
                    + "extends the connection.  Use class or nested-class.  Write -- visibility: or "
                    + "-- member-location: on the query.",
            ]);
    }

    // The same for SQLSRC123, which two types report at one place too.
    [Fact]
    public void Run_TwoTypesClaimingAFileWithSqlsrc123_NamesEachType()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = "Users.sql")]
                public partial class Sample;

                [SqlSourceGenerate(Source = "Users.sql")]
                public partial class Other;
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- visibility: private\n\n-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(Source, file, Sidecars.Of(file, UserEntry(file)));

        var problems = run.Problems();
        problems.Distinct().Count().ShouldBe(2);
        problems
            .Order(StringComparer.Ordinal)
            .ShouldBe([
                "SQLSRC123 /app/Repo/Users.sql(3,10)-(3,17): The query 'GetUser' of 'App.Other' cannot be private, "
                    + "which the file header sets, at the member location extension-class, because an extension method "
                    + "is public or internal.  Write -- visibility: or -- member-location: on the query.",
                "SQLSRC123 /app/Repo/Users.sql(3,10)-(3,17): The query 'GetUser' of 'App.Sample' cannot be private, "
                    + "which the file header sets, at the member location extension-class, because an extension method "
                    + "is public or internal.  Write -- visibility: or -- member-location: on the query.",
            ]);
    }

    // Two types that are not public claim one file, so the model of its public extension method is internal for both,
    // and each type reports the refusal at the one place.
    [Fact]
    public void Run_TwoInternalTypesClaimingAFileWithAPublicMethod_NamesEachType()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = "Users.sql")]
                internal partial class Sample;

                [SqlSourceGenerate(Source = "Users.sql")]
                internal partial class Other;
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- visibility: public\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = MethodRuns.Run(Source, file, Sidecars.Of(file, UserEntry(file)));

        var problems = run.Problems();
        problems.Distinct().Count().ShouldBe(2);
        problems
            .Order(StringComparer.Ordinal)
            .ShouldBe([
                "SQLSRC123 /app/Repo/Users.sql(2,1)-(2,22): The query 'GetUser' of 'App.Other' cannot be public, "
                    + "which its own marker sets, at the member location extension-class, because 'App.Other' is not "
                    + "public, so its models are internal, which a public extension method cannot take or return.  "
                    + "Write -- visibility: or -- member-location: on the query.",
                "SQLSRC123 /app/Repo/Users.sql(2,1)-(2,22): The query 'GetUser' of 'App.Sample' cannot be public, "
                    + "which its own marker sets, at the member location extension-class, because 'App.Sample' is not "
                    + "public, so its models are internal, which a public extension method cannot take or return.  "
                    + "Write -- visibility: or -- member-location: on the query.",
            ]);
    }

    // SQLSRC123 at GetUser of the type that GeneratorHarness.NestedSampleSource declares in a public Outer, whose
    // method would be reachable as protected.
    private static string Sqlsrc123AsProtected(
        string span,
        string middle,
        string visibility,
        string place,
        string location
    )
    {
        var type = middle.Length > 0 ? "App.Outer.Middle.Sample" : "App.Outer.Sample";
        return $"SQLSRC123 {GeneratorHarness.UsersPath}{span}: The query 'GetUser' of '{type}' cannot be {visibility}, "
            + $"which {place} sets, at the member location {location}, because '{type}' is reachable as protected, so "
            + "its models are internal, which a method reachable from another assembly cannot take or return.  Write "
            + "-- visibility: or -- member-location: on the query.";
    }

    // SQLSRC122 at a query at sql, set by the place.
    private static string Sqlsrc122(string span, string query, string place) =>
        $"SQLSRC122 {GeneratorHarness.UsersPath}{span}: The query '{query}' of 'App.Sample' cannot go in the "
        + $"extension class, which {place} sets, because its output is sql and only a query method extends the "
        + "connection.  Use class or nested-class.  Write -- visibility: or -- member-location: on the query.";

    // The entry of GetUser, which takes an id and reads it, written for the file's hash.
    private static SidecarEntry UserEntry(SqlFile file) => Entry(file, "GetUser", [Parameter("id")], [Column("id")]);
}
