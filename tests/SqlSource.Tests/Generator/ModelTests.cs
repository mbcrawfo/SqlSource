// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation.Models;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The models of each type that claims a query: which a query has, their names, namespace, accessibility, shape, order
// and documentation, as the files the generator adds show them.  Each run is at Models under postgres on Npgsql 10,
// with an entry written for each query's current hash, and every model must compile without a warning.
public sealed class ModelTests
{
    private const string OtherPath = "/app/Repo/Other.sql";

    private const string OutputSealedRecord = $$"""
        {{GeneratedFile.ModelHeader}}
        namespace App
        {
            /// <summary>
            /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
            /// </summary>
            /// <param name="Name">The column <c>name</c>, <c>text</c>.</param>
            /// <param name="Count">The column <c>count</c>, <c>integer</c>.</param>
            /// <param name="Note">The column <c>note</c>, <c>text</c>.</param>
            public sealed record GetUserDto(string Name, int? Count, string? Note);
        }

        """;

    private const string OutputRecordWithArrays = $$"""
        {{GeneratedFile.ModelHeader}}
        namespace App
        {
            /// <summary>
            /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
            /// </summary>
            /// <param name="Id">The column <c>id</c>, <c>integer</c>.</param>
            /// <param name="Tags">The column <c>tags</c>, <c>text[]</c>.</param>
            /// <param name="Scores">The column <c>scores</c>, <c>integer[]</c>.</param>
            public sealed record GetUserDto(int Id, string[] Tags, int[]? Scores)
            {
                /// <summary>
                /// Creates the record for materializers such as Dapper, which set each property after construction.  It
                /// leaves each property at its default value, null for a reference type even where it cannot be null,
                /// until it is assigned.
                /// </summary>
                private GetUserDto() : this(default(int), default(string[])!, default(int[]?)) { }
            }
        }

        """;

    private const string OutputClass = $$"""
        {{GeneratedFile.ModelHeader}}
        namespace App
        {
            /// <summary>
            /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
            /// </summary>
            public class GetUserDto
            {
                /// <summary>
                /// The column <c>name</c>, <c>text</c>.
                /// </summary>
                public string Name { get; set; } = default!;

                /// <summary>
                /// The column <c>count</c>, <c>integer</c>.
                /// </summary>
                public int? Count { get; set; }

                /// <summary>
                /// The column <c>note</c>, <c>text</c>.
                /// </summary>
                public string? Note { get; set; }
            }
        }

        """;

    private const string InputSealedRecord = $$"""
        {{GeneratedFile.ModelHeader}}
        namespace App
        {
            /// <summary>
            /// The parameters of the query <c>GetUser</c> from <c>Users.sql</c>.
            /// </summary>
            /// <param name="Name">The parameter <c>name</c>, <c>text</c>.</param>
            /// <param name="Count">The parameter <c>count</c>, <c>integer</c>.</param>
            /// <param name="Note">The parameter <c>note</c>, <c>text</c>.</param>
            public sealed record GetUserParams(string Name, int? Count, string? Note);
        }

        """;

    private const string InputClass = $$"""
        {{GeneratedFile.ModelHeader}}
        namespace App
        {
            /// <summary>
            /// The parameters of the query <c>GetUser</c> from <c>Users.sql</c>.
            /// </summary>
            public class GetUserParams
            {
                /// <summary>
                /// The parameter <c>name</c>, <c>text</c>.
                /// </summary>
                public string Name { get; set; } = default!;

                /// <summary>
                /// The parameter <c>count</c>, <c>integer</c>.
                /// </summary>
                public int? Count { get; set; }

                /// <summary>
                /// The parameter <c>note</c>, <c>text</c>.
                /// </summary>
                public string? Note { get; set; }
            }
        }

        """;

    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id, name FROM users WHERE id = @id;\n"
    );

    private static readonly PostgresType IntArray = TestSidecars.Postgres(
        name: "integer[]",
        kind: SidecarValues.Kind.Array,
        internalName: "_int4",
        element: Int4
    );

    private static readonly PostgresType TextArray = TestSidecars.Postgres(
        name: "text[]",
        kind: SidecarValues.Kind.Array,
        internalName: "_text",
        element: Text
    );

    private static readonly string OutputRecord = OutputSealedRecord.Replace(
        "public sealed record",
        "public record",
        StringComparison.Ordinal
    );

    private static readonly string OutputSealedClass = OutputClass.Replace(
        "public class",
        "public sealed class",
        StringComparison.Ordinal
    );

    private static readonly string InputRecord = InputSealedRecord.Replace(
        "public sealed record",
        "public record",
        StringComparison.Ordinal
    );

    private static readonly string InputSealedClass = InputClass.Replace(
        "public class",
        "public sealed class",
        StringComparison.Ordinal
    );

    // Each kind of model, as a setting writes it.
    public static TheoryData<string> Kinds => ["record", "sealed record", "class", "sealed class"];

    // Each kind of output model, with its file.
    public static TheoryData<string, string> OutputShapes =>
        new()
        {
            { "record", OutputRecord },
            { "sealed record", OutputSealedRecord },
            { "class", OutputClass },
            { "sealed class", OutputSealedClass },
        };

    // Each kind of input model, with its file.
    public static TheoryData<string, string> InputShapes =>
        new()
        {
            { "record", InputRecord },
            { "sealed record", InputSealedRecord },
            { "class", InputClass },
            { "sealed class", InputSealedClass },
        };

    // The levels that give a setting of a model, from the least specific.
    public static TheoryData<SettingLevel> Levels =>
        [SettingLevel.ProjectFile, SettingLevel.FolderFile, SettingLevel.Attribute, SettingLevel.Header];

    [Fact]
    public void Run_RowsAndParameters_EmitsBothModels()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("id")], [Column("id")]));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe([GetUserDtoFile, "App.GetUserParams-Model.g.cs"]);
    }

    [Fact]
    public void Run_RowsWithoutParameters_EmitsTheOutputModelAlone()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id")]));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe([GetUserDtoFile]);
    }

    [Fact]
    public void Run_NoRowsWithParameters_EmitsTheInputModelAlone()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("id")], null));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserParams-Model.g.cs"]);
    }

    [Fact]
    public void Run_NoRowsWithoutParameters_EmitsNoModel()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], null));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(OutputShapes))]
    public void Run_EachModelType_WritesItsShape(string kind, string expected)
    {
        var run = Run(
            Users,
            [
                Entry(
                    Users,
                    "GetUser",
                    [],
                    [Column("name", Text), Column("count", nullable: true), Column("note", Text, nullable: true)]
                ),
            ],
            settings: $$"""{"outputModelType": "{{kind}}"}"""
        );

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile].ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(InputShapes))]
    public void Run_EachInputModelType_WritesItsShape(string kind, string expected)
    {
        var run = Run(
            Users,
            [
                Entry(
                    Users,
                    "GetUser",
                    [Parameter("name", Text), Parameter("count", nullable: true), Parameter("note", Text, true)],
                    null
                ),
            ],
            settings: $$"""{"inputModelType": "{{kind}}"}"""
        );

        run.ShouldBeClean();
        run.Sources["App.GetUserParams-Model.g.cs"].ShouldBe(expected);
    }

    // SELECT FROM users returns rows of no column.
    [Fact]
    public void Run_EmptyOutputModel_IsAnEmptyRecord()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], []));

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile]
            .ShouldBe(
                $$"""
                {{GeneratedFile.ModelHeader}}
                namespace App
                {
                    /// <summary>
                    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
                    /// </summary>
                    public sealed record GetUserDto();
                }

                """
            );
    }

    [Fact]
    public void Run_EmptyOutputModel_IsAnEmptyClass()
    {
        var run = Run(
            Users,
            [Entry(Users, "GetUser", [], [])],
            settings: /*lang=json,strict*/
            """{"outputModelType": "sealed class"}"""
        );

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile]
            .ShouldBe(
                $$"""
                {{GeneratedFile.ModelHeader}}
                namespace App
                {
                    /// <summary>
                    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
                    /// </summary>
                    public sealed class GetUserDto
                    {
                    }
                }

                """
            );
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Run_SuffixesAtEachLevel_NameTheModels(SettingLevel level)
    {
        var settings = new LevelSettings()
            .At(level, Setting.InputModelSuffix, LevelSettings.StringAt(level, "Key"))
            .At(level, Setting.OutputModelSuffix, LevelSettings.StringAt(level, "Row"));
        var files = settings.Files(Users);
        var file = files[0];

        var run = Run(
            settings.Source(Source),
            [.. files, Sidecars.Of(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")]))],
            settings: settings.ProjectSettings
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserKey-Model.g.cs", "App.GetUserRow-Model.g.cs"]);
    }

    // The markers give the names whole, without the suffixes.
    [Fact]
    public void Run_ModelMarkers_NameTheModels()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- input-model: UserKey\n-- output-model: UserRow\n"
                + "SELECT id FROM users WHERE id = @id;\n"
        );

        var run = Run(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")]));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.UserKey-Model.g.cs", "App.UserRow-Model.g.cs"]);
        run.Sources["App.UserRow-Model.g.cs"].ShouldContain("public sealed record UserRow(int Id);");
    }

    [Fact]
    public void Run_ModelMarkersWithAPeriod_AreFullNames()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- input-model: Data.Keys.UserKey\n-- output-model: Data.Rows.UserRow\n"
                + "SELECT id FROM users WHERE id = @id;\n"
        );

        var run = Run(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")]));

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["Data.Keys.UserKey-Model.g.cs", "Data.Rows.UserRow-Model.g.cs"]);
        run.Sources["Data.Rows.UserRow-Model.g.cs"].ShouldContain("namespace Data.Rows\n{\n");
        run.Sources["Data.Rows.UserRow-Model.g.cs"].ShouldContain("public sealed record UserRow(int Id);");
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Run_ModelNamespaceAtEachLevel_HoldsTheModels(SettingLevel level)
    {
        var settings = new LevelSettings().At(
            level,
            Setting.ModelNamespace,
            LevelSettings.StringAt(level, "App.Models")
        );
        var files = settings.Files(Users);
        var file = files[0];

        var run = Run(
            settings.Source(Source),
            [.. files, Sidecars.Of(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")]))],
            settings: settings.ProjectSettings
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.Models.GetUserDto-Model.g.cs", "App.Models.GetUserParams-Model.g.cs"]);
        run.Sources["App.Models.GetUserDto-Model.g.cs"].ShouldContain("namespace App.Models\n{\n");
    }

    [Fact]
    public void Run_TypeInTheGlobalNamespace_GivesModelsInIt()
    {
        var source = """
            using SqlSource;

            [SqlSourceGenerate]
            public partial class Sample;
            """;

        var run = Run(source, [Users, Sidecars.Of(Users, Entry(Users, "GetUser", [], [Column("id")]))]);

        run.ShouldBeClean();
        run.Sources["GetUserDto-Model.g.cs"]
            .ShouldBe(
                $$"""
                {{GeneratedFile.ModelHeader}}
                /// <summary>
                /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
                /// </summary>
                /// <param name="Id">The column <c>id</c>, <c>integer</c>.</param>
                public sealed record GetUserDto(int Id);

                """
            );
    }

    // A keyword in the type's namespace keeps its @ in code and loses it in the name of the file.
    [Fact]
    public void Run_TypeInANamespaceWithAKeyword_WritesItAsTheTypeDoes()
    {
        var source = """
            using SqlSource;

            namespace App.@class
            {
                [SqlSourceGenerate]
                public partial class Sample;
            }
            """;

        var run = Run(source, [Users, Sidecars.Of(Users, Entry(Users, "GetUser", [], [Column("id")]))]);

        run.ShouldBeClean();
        run.Sources["App.class.GetUserDto-Model.g.cs"].ShouldContain("namespace App.@class\n{\n");
    }

    // The input model has no marker, so it takes the namespace setting.
    [Fact]
    public void Run_MarkerWithAPeriod_IgnoresTheModelNamespace()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- output-model: Data.UserRow\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = Run(
            Source,
            [file, Sidecars.Of(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")]))],
            settings: /*lang=json,strict*/
            """{"modelNamespace": "App.Models"}"""
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.Models.GetUserParams-Model.g.cs", "Data.UserRow-Model.g.cs"]);
    }

    [Theory]
    [InlineData("public partial class Sample;", "public")]
    [InlineData("internal partial class Sample;", "internal")]
    [InlineData("internal partial class Outer { [SqlSourceGenerate] public partial class Sample; }", "internal")]
    public void Run_TypesAccessibility_IsTheModels(string declaration, string expected)
    {
        var source =
            "using SqlSource;\n\nnamespace App\n{\n"
            + (
                declaration.StartsWith("internal partial class Outer", StringComparison.Ordinal)
                    ? declaration
                    : "[SqlSourceGenerate]\n" + declaration
            )
            + "\n}\n";

        var run = Run(source, [Users, Sidecars.Of(Users, Entry(Users, "GetUser", [], [Column("id")]))]);

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile].ShouldContain($"    {expected} sealed record GetUserDto(int Id);\n");
    }

    // The parameters are in the order of the query's list, which puts one that only a -- param: marker declares last.
    [Fact]
    public void Run_DeclaredOnlyParameter_IsLast()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- param: @limit int\nSELECT id FROM users WHERE name = @name AND id = @id;\n"
        );

        var run = Run(
            file,
            Entry(file, "GetUser", [Parameter("name", Text), Parameter("id"), Parameter("limit")], null)
        );

        run.ShouldBeClean();
        run.Sources["App.GetUserParams-Model.g.cs"]
            .ShouldContain("public sealed record GetUserParams(string Name, int Id, int Limit);");
    }

    // An ordinal sort puts BX before Ba, which a sort by culture would not.  The other model keeps its order.
    [Fact]
    public void Run_SortInput_SortsTheInputModelAlone()
    {
        var run = Run(
            Users,
            [
                Entry(
                    Users,
                    "GetUser",
                    [Parameter("ba"), Parameter("b_x"), Parameter("a")],
                    [Column("ba"), Column("b_x"), Column("a")]
                ),
            ],
            settings: /*lang=json,strict*/
            """{"generatorParameters": ["sort-input"]}"""
        );

        run.ShouldBeClean();
        run.Sources["App.GetUserParams-Model.g.cs"].ShouldContain("record GetUserParams(int A, int BX, int Ba);");
        run.Sources[GetUserDtoFile].ShouldContain("record GetUserDto(int Ba, int BX, int A);");
    }

    [Fact]
    public void Run_SortOutput_SortsTheOutputModelAlone()
    {
        var run = Run(
            Users,
            [
                Entry(
                    Users,
                    "GetUser",
                    [Parameter("ba"), Parameter("b_x"), Parameter("a")],
                    [Column("ba"), Column("b_x"), Column("a")]
                ),
            ],
            settings: /*lang=json,strict*/
            """{"generatorParameters": ["sort-output"]}"""
        );

        run.ShouldBeClean();
        run.Sources["App.GetUserParams-Model.g.cs"].ShouldContain("record GetUserParams(int Ba, int BX, int A);");
        run.Sources[GetUserDtoFile].ShouldContain("record GetUserDto(int A, int BX, int Ba);");
    }

    // The summary comes first, escaped, then the sentence that names the query.
    [Fact]
    public void Run_ModelWithASummary_DocumentsItFirst()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- summary: Loads one user\n-- summary: by <id> & name.\n"
                + "SELECT id FROM users WHERE id = @id;\n"
        );

        var run = Run(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")]));

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile]
            .ShouldContain(
                "    /// <summary>\n"
                    + "    /// Loads one user by &lt;id&gt; &amp; name.\n"
                    + "    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.\n"
                    + "    /// </summary>\n"
            );
        run.Sources["App.GetUserParams-Model.g.cs"]
            .ShouldContain(
                "    /// <summary>\n"
                    + "    /// Loads one user by &lt;id&gt; &amp; name.\n"
                    + "    /// The parameters of the query <c>GetUser</c> from <c>Users.sql</c>.\n"
                    + "    /// </summary>\n"
            );
    }

    // A file name is escaped, and a query's name is an identifier, which needs nothing.
    [Fact]
    public void Run_ModelWithoutASummary_NamesItsQueryAndFile()
    {
        var file = new SqlFile("/app/Repo/A&B.sql", "-- name: GetUser\nSELECT id FROM users;\n");

        var run = Run(file, Entry(file, "GetUser", [], [Column("id")]));

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile]
            .ShouldContain(
                "    /// <summary>\n"
                    + "    /// A row of the query <c>GetUser</c> from <c>A&amp;B.sql</c>.\n"
                    + "    /// </summary>\n"
            );
    }

    // A line terminator in a quoted name the documentation repeats starts a new comment line.
    [Fact]
    public void Run_DocumentationWithALineTerminator_StaysInTheComment()
    {
        var run = Run(
            Users,
            Entry(Users, "GetUser", [], [Column("id", origin: new SidecarOrigin("public", "my\ntable", "id"))])
        );

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile]
            .ShouldContain(
                "    /// <param name=\"Id\">The column <c>id</c>, <c>integer</c>, from <c>public.my\n"
                    + "    /// table.id</c>.</param>\n"
            );
    }

    // Every name a record, a class or object declares, read from the compiler, as the PascalCase that a property
    // gets, except those the reserved list holds: a property of each name compiles in each kind of model without a
    // warning, so the list leaves out nothing the compiler objects to.
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Run_DeclaredNamesNotReserved_CompileInEachModel(string kind)
    {
        var names = DeclaredNames().Where(name => !PropertyNames.IsReserved(name)).ToArray();
        names.ShouldNotBeEmpty();

        var run = Run(
            Users,
            [
                Entry(
                    Users,
                    "GetUser",
                    [.. names.Select(name => Parameter(name))],
                    [.. names.Select(name => Column(name))]
                ),
            ],
            settings: $$"""{"inputModelType": "{{kind}}", "outputModelType": "{{kind}}"}"""
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe([GetUserDtoFile, "App.GetUserParams-Model.g.cs"]);
    }

    // The type that asks for the SQL alone gets no models, and the other type's are in its namespace.
    [Fact]
    public void Run_QueryClaimedAtSqlAndAtModels_GetsTheModelsTypesModels()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Output = GeneratorOutput.Sql)]
                public partial class First;
            }

            namespace Other
            {
                [SqlSourceGenerate]
                public partial class Second;
            }
            """;

        var run = Run(source, [Users, Sidecars.Of(Users, Entry(Users, "GetUser", [Parameter("id")], [Column("id")]))]);

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["Other.GetUserDto-Model.g.cs", "Other.GetUserParams-Model.g.cs"]);
    }

    // A type with a diagnostic of its own gets no models, and the other type claiming the file does.
    [Fact]
    public void Run_TypeWithADiagnostic_GetsNoModels()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;
            }

            namespace Broken
            {
                [SqlSourceGenerate]
                public class NotPartial;
            }
            """;

        var run = Run(source, [Users, Sidecars.Of(Users, Entry(Users, "GetUser", [], [Column("id")]))]);

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC001 ");
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe([GetUserDtoFile]);
    }

    // First claims both files, and loses Users.sql to SQLSRC008, since Other.sql comes first and has a query of the
    // same name.  Second claims Users.sql alone, and gets its models.
    [Fact]
    public void Run_FileExcludedBySqlsrc008_GetsNoModelsForThatType()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class First;
            }

            namespace Other
            {
                [SqlSourceGenerate(Path = "Users.sql")]
                public partial class Second;
            }
            """;
        var other = new SqlFile(OtherPath, SqlTexts.GetUser);

        var run = Run(
            source,
            [
                Users,
                Sidecars.Of(Users, Entry(Users, "GetUser", [], [Column("id")])),
                other,
                Sidecars.Of(other, Entry(other, "GetUser", [], [Column("one")])),
            ]
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC008 ");
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe([GetUserDtoFile, "Other.GetUserDto-Model.g.cs"]);
        run.Sources[GetUserDtoFile].ShouldContain("record GetUserDto(int One);");
        run.Sources["Other.GetUserDto-Model.g.cs"].ShouldContain("record GetUserDto(int Id);");
    }

    // A query named Sql is SQLSRC009 for First, whose members are nested in a class of that name, and not for
    // Second, whose members are its own.
    [Fact]
    public void Run_FileExcludedBySqlsrc009_GetsNoModelsForThatType()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class First;
            }

            namespace Other
            {
                [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
                public partial class Second;
            }
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Sql\nSELECT 1;\n");

        var run = Run(source, [file, Sidecars.Of(file, Entry(file, "Sql", [], [Column("one")]))]);

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC009 ");
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["Other.SqlDto-Model.g.cs"]);
    }

    // A keyword in braces in a comment is SQLSRC114 for First, which keeps the comments, and not for Second, which
    // does not.
    [Fact]
    public void Run_FileExcludedByAnErrorInAKeptComment_GetsNoModelsForThatType()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Parameters = "keep-comments")]
                public partial class First;
            }

            namespace Other
            {
                [SqlSourceGenerate]
                public partial class Second;
            }
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1 AS one; -- {{class}}\n");

        var run = Run(source, [file, Sidecars.Of(file, Entry(file, "GetUser", [], [Column("one")]))]);

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC114 ");
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        ModelFiles(run).ShouldBe(["Other.GetUserDto-Model.g.cs"]);
    }

    [Fact]
    public void Run_RecordWithAnArrayColumn_HasAPrivateConstructorWithoutParameters()
    {
        var run = Run(
            Users,
            Entry(
                Users,
                "GetUser",
                [],
                [Column("id"), Column("tags", TextArray), Column("scores", IntArray, nullable: true)]
            )
        );

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile].ShouldBe(OutputRecordWithArrays);
    }

    [Fact]
    public void Run_NonSealedRecordWithAnArrayColumn_HasAPrivateConstructorWithoutParameters()
    {
        var run = Run(
            Users,
            [Entry(Users, "GetUser", [], [Column("id"), Column("scores", IntArray, nullable: true)])],
            settings: /*lang=json,strict*/
            """{"outputModelType": "record"}"""
        );

        run.ShouldBeClean();
        var model = run.Sources[GetUserDtoFile];
        model.ShouldContain("public record GetUserDto(int Id, int[]? Scores)");
        model.ShouldContain("private GetUserDto() : this(default(int), default(int[]?)) { }");
    }

    [Fact]
    public void Run_RecordWithOnlyAnArrayColumn_Compiles()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("scores", IntArray, nullable: true)]));

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile].ShouldContain(": this(default(int[]?)) { }");
    }

    [Fact]
    public void Run_RecordWithADomainOverAnArray_HasTheConstructor()
    {
        var domain = TestSidecars.Postgres(
            name: "scores",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "scores",
            baseType: IntArray
        );

        var run = Run(Users, Entry(Users, "GetUser", [], [Column("scores", domain)]));

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile].ShouldContain("private GetUserDto() : this(");
    }

    [Fact]
    public void Run_RecordWithoutAnArrayColumn_HasNoConstructor()
    {
        var run = Run(Users, Entry(Users, "GetUser", [], [Column("id")]));

        run.ShouldBeClean();
        var model = run.Sources[GetUserDtoFile];
        model.ShouldContain("public sealed record GetUserDto(int Id);");
        model.ShouldNotContain("private GetUserDto()");
    }

    // A class has a constructor without parameters already, and an input model, a record by default, is never read by
    // Dapper.
    [Fact]
    public void Run_ClassOrInputModelWithAnArray_IsUnchanged()
    {
        var run = Run(
            Users,
            [
                Entry(
                    Users,
                    "GetUser",
                    [Parameter("tags", TextArray)],
                    [Column("id"), Column("scores", IntArray, nullable: true)]
                ),
            ],
            settings: /*lang=json,strict*/
            """{"outputModelType": "class"}"""
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe([GetUserDtoFile, "App.GetUserParams-Model.g.cs"]);
        run.Sources[GetUserDtoFile].ShouldContain("public class GetUserDto");
        run.Sources["App.GetUserParams-Model.g.cs"].ShouldContain("public sealed record GetUserParams(string[] Tags);");
        foreach (var name in ModelFiles(run))
        {
            run.Sources[name].ShouldNotContain("private ");
        }
    }

    // One of the two queries has the array in the database and the other a text that a type override reads as string[],
    // and the shared model has the constructor for the one.
    [Fact]
    public void Run_SharedModelWhereOneQueryHasAnArray_HasTheConstructor()
    {
        var a = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetA\n-- output-model: TagsRow\nSELECT tags FROM a;\n"
        );
        var b = new SqlFile(
            OtherPath,
            "-- name: GetB\n-- output-model: TagsRow\n-- type-override: text = string[]\nSELECT tag FROM b;\n"
        );

        var run = Run(
            Source,
            [
                a,
                Sidecars.Of(a, Entry(a, "GetA", [], [Column("tags", TextArray, nullable: true)])),
                b,
                Sidecars.Of(b, Entry(b, "GetB", [], [Column("tags", Text, nullable: true)])),
            ]
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.TagsRow-Model.g.cs"]);
        run.Sources["App.TagsRow-Model.g.cs"].ShouldContain("private TagsRow() : this(default(string[]?)) { }");
    }

    [Fact]
    public void Run_RecordWithAnArrayColumn_KeepsItsPositionalConstructorEqualityAndWith()
    {
        var entries = Sidecars.Of(Users, Entry(Users, "GetUser", [], [Column("id"), Column("scores", IntArray, true)]));
        const string Use = """
            namespace App
            {
                public static class Use
                {
                    public static bool Run()
                    {
                        var a = new GetUserDto(1, null);
                        var b = a with { Id = 2 };
                        return a == b with { Id = 1 };
                    }
                }
            }
            """;

        var run = Run(Source + Use, [Users, entries]);
        var withoutParameters = Run(
            Source + Use.Replace("new GetUserDto(1, null)", "new GetUserDto()", StringComparison.Ordinal),
            [Users, entries]
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        withoutParameters.CompilationErrors.ShouldContain(error => error.Contains("CS0122", StringComparison.Ordinal));
    }

    // A query at Sql needs no entry, and has no models.
    [Fact]
    public void Run_OutputSql_EmitsNoModel()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [Users],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.ShouldBeClean();
        run.Sources.ShouldContainKey(GeneratorHarness.SampleFile);
        ModelFiles(run).ShouldBeEmpty();
    }

    // The PascalCase of each member a record, a sealed record, a class, a sealed class and object declare, without the
    // positional property P and its accessors, once ignoring case, where it is an identifier.
    private static string[] DeclaredNames()
    {
        var compilation = GeneratorHarness.CreateCompilation([
            new SourceFile(
                "/app/Repo/Shapes.cs",
                "public record R(int P); public sealed record S(int P); public class C; public sealed class D;"
            ),
        ]);
        var names = new List<string>();
        foreach (var typeName in new[] { "R", "S", "C", "D" })
        {
            for (var type = compilation.GetTypeByMetadataName(typeName); type is not null; type = type.BaseType)
            {
                names.AddRange(
                    type.GetMembers()
                        .Select(member => member.Name)
                        .Where(name => name is not ("P" or "get_P" or "set_P"))
                        .Select(PropertyNames.PascalCase)
                        .Where(PropertyNames.IsIdentifier)
                );
            }
        }

        return [.. names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.Ordinal)];
    }
}
