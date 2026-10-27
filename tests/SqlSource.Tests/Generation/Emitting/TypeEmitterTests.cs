using System;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class TypeEmitterTests
{
    [Fact]
    public void Emit_NestedPlacement_PutsTheConstantsInAPrivateSqlClass()
    {
        var users = File(
            "Users.sql",
            TestModels.Query("GetUser", "SELECT id, name\nFROM users\nWHERE id = @id;", "Loads one user by id."),
            TestModels.Query("ListUsers", "SELECT id, name FROM users;")
        );

        var output = Emit(OutputKind.Sql, TestModels.Type(), users);

        output.HintName.ShouldBe("App.UserRepository.g.cs");
        output.Diagnostics.ShouldBeEmpty();
        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            namespace App
            {
                partial class UserRepository
                {
                    private static class Sql
                    {
                        /// <summary>
                        /// Loads one user by id.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT id, name
                        /// FROM users
                        /// WHERE id = @id;
                        /// </code>
                        /// </remarks>
                        public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";

                        /// <summary>
                        /// The <c>ListUsers</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT id, name FROM users;
                        /// </code>
                        /// </remarks>
                        public const string ListUsers = "SELECT id, name FROM users;";
                    }
                }
            }

            """
        );
    }

    [Fact]
    public void Emit_DirectPlacement_PutsTheConstantsOnTheType()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(placement: MemberPlacement.Direct),
            File("Users.sql", TestModels.Query("GetUser", "SELECT 1;"))
        );

        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            namespace App
            {
                partial class UserRepository
                {
                    /// <summary>
                    /// The <c>GetUser</c> query from <c>Users.sql</c>.
                    /// </summary>
                    /// <remarks>
                    /// <code>
                    /// SELECT 1;
                    /// </code>
                    /// </remarks>
                    public const string GetUser = "SELECT 1;";
                }
            }

            """
        );
    }

    [Fact]
    public void Emit_NestedGenericTypeInTheGlobalNamespace_RepeatsEachDeclarationAsWritten()
    {
        var type = TestModels.Type(
            @namespace: "",
            types:
            [
                new TypeDeclaration("interface", "IOuter", "IOuter", "<TKey>", 1),
                new TypeDeclaration("record struct", "@class", "class", "<T1, T2>", 2),
                new TypeDeclaration("record", "Inner", "Inner", "", 0),
            ]
        );

        var output = Emit(OutputKind.Sql, type, File("Users.sql", TestModels.Query("GetUser", "SELECT 1;")));

        output.HintName.ShouldBe("IOuter-1.class-2.Inner.g.cs");
        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            partial interface IOuter<TKey>
            {
                partial record struct @class<T1, T2>
                {
                    partial record Inner
                    {
                        private static class Sql
                        {
                            /// <summary>
                            /// The <c>GetUser</c> query from <c>Users.sql</c>.
                            /// </summary>
                            /// <remarks>
                            /// <code>
                            /// SELECT 1;
                            /// </code>
                            /// </remarks>
                            public const string GetUser = "SELECT 1;";
                        }
                    }
                }
            }

            """
        );
    }

    [Fact]
    public void Emit_NamespaceWithAnEscapedKeyword_KeepsItInCodeAndDropsItFromTheHintName()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(@namespace: "App.@event.Data"),
            File("Users.sql", TestModels.Query("GetUser", "SELECT 1;"))
        );

        output.HintName.ShouldBe("App.event.Data.UserRepository.g.cs");
        output.Source.ShouldNotBeNull().ShouldContain("namespace App.@event.Data\n{\n");
    }

    [Fact]
    public void Emit_SqlWithCharactersThatNeedEscaping_EscapesTheLiteralAndTheCommentSeparately()
    {
        const string Sql = "SELECT \"a\", 'b\\c', '\t' WHERE x < 1 && y > 2;";

        var output = Emit(OutputKind.Sql, TestModels.Type(), File("A&B.sql", TestModels.Query("GetUser", Sql)));

        var source = output.Source.ShouldNotBeNull();
        source.ShouldContain("/// The <c>GetUser</c> query from <c>A&amp;B.sql</c>.\n");
        source.ShouldContain("/// SELECT \"a\", 'b\\c', '\t' WHERE x &lt; 1 &amp;&amp; y &gt; 2;\n");
        source.ShouldContain(
            "public const string GetUser = \"SELECT \\\"a\\\", 'b\\\\c', '\\t' WHERE x < 1 && y > 2;\";\n"
        );
    }

    [Fact]
    public void Emit_SummaryWithCharactersXmlReserves_EscapesThem()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(),
            File("Users.sql", TestModels.Query("GetUser", "SELECT 1;", "Users <b>with</b> a & b."))
        );

        output.Source.ShouldNotBeNull().ShouldContain("/// Users &lt;b&gt;with&lt;/b&gt; a &amp; b.\n");
    }

    [Fact]
    public void Emit_SeveralFiles_KeepsTheOrderOfTheFilesAndOfTheQueriesInEach()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(placement: MemberPlacement.Direct),
            File("B.sql", TestModels.Query("Zeta", "SELECT 1;"), TestModels.Query("Alpha", "SELECT 2;")),
            File("A.sql", TestModels.Query("Mid", "SELECT 3;"))
        );

        var source = output.Source.ShouldNotBeNull();
        source.IndexOf("const string Zeta", StringComparison.Ordinal).ShouldBeGreaterThan(0);
        source
            .IndexOf("const string Alpha", StringComparison.Ordinal)
            .ShouldBeGreaterThan(source.IndexOf("const string Zeta", StringComparison.Ordinal));
        source
            .IndexOf("const string Mid", StringComparison.Ordinal)
            .ShouldBeGreaterThan(source.IndexOf("const string Alpha", StringComparison.Ordinal));
    }

    [Fact]
    public void Emit_TypeWithAProblem_GetsNoSourceAndKeepsTheProblem()
    {
        var problem = DiagnosticInfo.Create(
            SqlDiagnostics.TypeNotPartial,
            TestModels.AttributeLocation,
            "UserRepository"
        );
        var typeFiles = new TypeFiles(
            TestModels.Type(),
            TestModels.Array(GeneratorHarness.UsersNormalizedPath),
            TestModels.Array(problem)
        );

        var output = TypeEmitter.Emit(
            new TypeQueries(
                typeFiles,
                TestModels.Array(File("Users.sql", TestModels.Query("GetUser", "SELECT 1;"))),
                TestModels.Array(new SettingsLevel { Output = OutputKind.Sql }),
                "App.UserRepository.g.cs"
            )
        );

        output.ShouldBe(new TypeOutput("App.UserRepository.g.cs", null, TestModels.Array(problem)));
    }

    [Fact]
    public void Emit_QueryNameAlreadyUsedByAnEarlierFile_IsAnErrorAndTheLaterFileGivesNoMembers()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(),
            File("A.sql", TestModels.Query("GetUser", "SELECT 1;")),
            File(
                "B.sql",
                TestModels.Query("Other", "SELECT 2;"),
                TestModels.Query("GetUser", "SELECT 3;", nameLocation: TestModels.NameLocation("B.sql"))
            ),
            File("C.sql", TestModels.Query("Other", "SELECT 4;"))
        );

        output.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.DuplicateQueryName,
                TestModels.NameLocation("B.sql"),
                "GetUser",
                "A.sql",
                "UserRepository"
            ),
        ]);
        var source = output.Source.ShouldNotBeNull();
        source.ShouldContain("public const string GetUser = \"SELECT 1;\";");
        source.ShouldContain("public const string Other = \"SELECT 4;\";");
        source.ShouldNotContain("SELECT 2;");
        source.ShouldNotContain("SELECT 3;");
    }

    [Theory]
    [InlineData(false, "Sql")]
    [InlineData(true, "UserRepository")]
    public void Emit_QueryNamedLikeTheTypeItsMemberGoesIn_IsAnErrorAndItsFileGivesNoMembers(bool direct, string name)
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(placement: Placement(direct)),
            File("A.sql", TestModels.Query(name, "SELECT 1;"), TestModels.Query("Other", "SELECT 2;")),
            File("B.sql", TestModels.Query("Kept", "SELECT 3;"))
        );

        output.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.QueryNamedLikeContainingType,
                TestModels.NameLocation("Users.sql"),
                "UserRepository",
                name
            ),
        ]);
        var source = output.Source.ShouldNotBeNull();
        source.ShouldContain("public const string Kept = \"SELECT 3;\";");
        source.ShouldNotContain("SELECT 1;");
        source.ShouldNotContain("SELECT 2;");
    }

    // A CodeGen query has no member in the type its member would go in, so its file is usable, and a query at sql of
    // the file keeps its constant.
    [Theory]
    [InlineData(false, "Sql")]
    [InlineData(true, "UserRepository")]
    public void Emit_CodeGenQueryNamedLikeTheTypeItsMemberWouldGoIn_IsAllowed(bool direct, string name)
    {
        var output = Emit(
            OutputKind.CodeGen,
            TestModels.Type(placement: Placement(direct)),
            File(
                "A.sql",
                TestModels.Query(name, "SELECT 1;"),
                TestModels.Query("Other", "SELECT 2;", markers: new SettingsLevel { Output = OutputKind.Sql })
            )
        );

        output.Diagnostics.ShouldBeEmpty();
        var source = output.Source.ShouldNotBeNull();
        source.ShouldContain("public const string Other = \"SELECT 2;\";");
        source.ShouldNotContain("SELECT 1;");
    }

    [Theory]
    [InlineData(false, "UserRepository")]
    [InlineData(true, "Sql")]
    [InlineData(false, "sql")]
    public void Emit_QueryNamedLikeTheOtherPlacementsType_IsAllowed(bool direct, string name)
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(placement: Placement(direct)),
            File("A.sql", TestModels.Query(name, "SELECT 1;"))
        );

        output.Diagnostics.ShouldBeEmpty();
        output.Source.ShouldNotBeNull().ShouldContain("public const string " + name + " = \"SELECT 1;\";");
    }

    [Fact]
    public void Emit_QueryWithAToken_IsAMethodInTheSamePlaceAndOrderAsTheConstants()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(),
            File(
                "Users.sql",
                TestModels.Query("GetUser", "SELECT 1;"),
                TokenQuery("ListFrom"),
                TestModels.Query("Count", "SELECT 3;")
            )
        );

        output.Diagnostics.ShouldBeEmpty();
        output.Source.ShouldBe(
            $$$"""
            {{{GeneratedFile.Header}}}
            namespace App
            {
                partial class UserRepository
                {
                    private static class Sql
                    {
                        /// <summary>
                        /// The <c>GetUser</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT 1;
                        /// </code>
                        /// </remarks>
                        public const string GetUser = "SELECT 1;";

                        /// <summary>
                        /// The <c>ListFrom</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT * FROM {{table}};
                        /// </code>
                        /// </remarks>
                        /// <param name="table">The text that replaces <c>{{table}}</c>.</param>
                        /// <returns>The SQL with each token replaced by its argument.</returns>
                        public static string ListFrom(string table)
                        {
                            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);
                            return string.Create(
                                checked(15 + table.Length),
                                table,
                                static (span, state) =>
                                {
                                    "SELECT * FROM ".CopyTo(span);
                                    span = span.Slice(14);
                                    state.CopyTo(span);
                                    span = span.Slice(state.Length);
                                    ";".CopyTo(span);
                                }
                            );
                        }

                        /// <summary>
                        /// The <c>Count</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT 3;
                        /// </code>
                        /// </remarks>
                        public const string Count = "SELECT 3;";
                    }
                }
            }

            """
        );
    }

    [Fact]
    public void Emit_QueryWithATokenInDirectPlacement_IsAMethodOnTheType()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(placement: MemberPlacement.Direct),
            File("Users.sql", TokenQuery("ListFrom"))
        );

        var source = output.Source.ShouldNotBeNull();
        source.ShouldContain("    {\n        /// <summary>\n        /// The <c>ListFrom</c> query from");
        source.ShouldContain("        public static string ListFrom(string table)\n        {\n");
        source.ShouldNotContain("class Sql");
    }

    // The query's own generator parameter, then what the project asks for.
    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Emit_QueryWithAToken_ValidatesByItsGeneratorParameterAndThenByTheProject(
        bool? parameter,
        bool project,
        bool expected
    )
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(),
            project,
            File("Users.sql", TokenQuery("ListFrom", parameter))
        );

        var source = output.Source.ShouldNotBeNull();
        if (expected)
        {
            source.ShouldContain("global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);\n");
        }
        else
        {
            source.ShouldNotContain("Throw");
        }
    }

    [Fact]
    public void Emit_ValidationOff_LeavesAConstantAsItIs()
    {
        var files = File("Users.sql", TestModels.Query("GetUser", "SELECT 1;"));

        Emit(OutputKind.Sql, TestModels.Type(), validateTokens: false, files)
            .ShouldBe(Emit(OutputKind.Sql, TestModels.Type(), files));
    }

    [Fact]
    public void Emit_ThreeLevels_ResolveForEachQuery()
    {
        var keep = new SettingsLevel { Parameters = GeneratorParameters.KeepComments };
        var none = new SettingsLevel { Parameters = GeneratorParameters.None };
        static SqlQuery Kept(string name, SettingsLevel markers) =>
            TestModels.Query(name, "SELECT 1;", keptSql: "SELECT 1; -- " + name, markers: markers);

        var output = TypeEmitter.Emit(
            new TypeQueries(
                new TypeFiles(
                    TestModels.Type(settings: none),
                    TestModels.Array<string>(),
                    TestModels.Array<DiagnosticInfo>()
                ),
                TestModels.Array(
                    File("Users.sql", Kept("FromTheAttribute", SettingsLevel.None), Kept("FromItsMarker", keep))
                ),
                TestModels.Array(keep with { Output = OutputKind.Sql }),
                "App.UserRepository.g.cs"
            )
        );

        // The attribute's empty list beats the file's level; a query's own marker beats the attribute.
        output.Source.ShouldNotBeNull().ShouldNotContain("-- FromTheAttribute");
        output.Source.ShouldContain("-- FromItsMarker");
    }

    [Fact]
    public void Emit_MethodNamedLikeAConstantOfAnEarlierFile_IsTheSameErrorAsForTwoConstants()
    {
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(),
            File("A.sql", TestModels.Query("GetUser", "SELECT 1;")),
            File("B.sql", TokenQuery("GetUser", file: "B.sql"))
        );

        output.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.DuplicateQueryName,
                TestModels.NameLocation("B.sql"),
                "GetUser",
                "A.sql",
                "UserRepository"
            ),
        ]);
        output.Source.ShouldNotBeNull().ShouldNotContain("public static string GetUser");
    }

    [Fact]
    public void Emit_MethodNamedLikeTheSqlClass_IsTheSameErrorAsForAConstant()
    {
        var output = Emit(OutputKind.Sql, TestModels.Type(), File("Users.sql", TokenQuery("Sql")));

        output.Diagnostics.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.QueryNamedLikeContainingType);
        output.Source.ShouldNotBeNull().ShouldNotContain("public static string Sql");
    }

    [Fact]
    public void Emit_TypeWhoseFilesGiveNoMembers_StillGetsItsEmptySqlClass()
    {
        var output = Emit(OutputKind.Sql, TestModels.Type(), File("Users.sql"));

        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            namespace App
            {
                partial class UserRepository
                {
                    private static class Sql
                    {
                    }
                }
            }

            """
        );
    }

    // A CodeGen query's method takes the place of its constant, whether it gets one or not.
    [Fact]
    public void Emit_CodeGenQuery_WritesNoConstant()
    {
        var output = Emit(
            OutputKind.CodeGen,
            TestModels.Type(placement: MemberPlacement.Direct),
            File("Users.sql", TestModels.Query("GetUser", "SELECT 1;"))
        );

        output.Diagnostics.ShouldBeEmpty();
        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            namespace App
            {
                partial class UserRepository
                {
                }
            }

            """
        );
    }

    [Fact]
    public void Emit_CodeGenTokenQuery_WritesNoTokenMethod()
    {
        var output = Emit(
            OutputKind.CodeGen,
            TestModels.Type(placement: MemberPlacement.Direct),
            File("Users.sql", TokenQuery("ListFrom"))
        );

        output.Diagnostics.ShouldBeEmpty();
        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            namespace App
            {
                partial class UserRepository
                {
                }
            }

            """
        );
    }

    [Fact]
    public void Emit_NestedTypeOfOnlyCodeGenQueries_WritesNoNestedClass()
    {
        var output = Emit(
            OutputKind.CodeGen,
            TestModels.Type(),
            File("Users.sql", TestModels.Query("GetUser", "SELECT 1;"), TokenQuery("ListFrom"))
        );

        output.Diagnostics.ShouldBeEmpty();
        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            namespace App
            {
                partial class UserRepository
                {
                }
            }

            """
        );
    }

    // The project's output is CodeGen, and two queries' markers say Sql and Models.
    [Fact]
    public void Emit_MixedSqlModelsAndCodeGen_WritesTheSqlAndModelsConstantsAlone()
    {
        var output = Emit(
            OutputKind.CodeGen,
            TestModels.Type(),
            File(
                "Users.sql",
                TestModels.Query("GetUser", "SELECT 1;"),
                TestModels.Query("AsSql", "SELECT 2;", markers: new SettingsLevel { Output = OutputKind.Sql }),
                TokenQuery("ListFrom"),
                TestModels.Query("AsModels", "SELECT 3;", markers: new SettingsLevel { Output = OutputKind.Models })
            )
        );

        output.Diagnostics.ShouldBeEmpty();
        output.Source.ShouldBe(
            $$"""
            {{GeneratedFile.Header}}
            namespace App
            {
                partial class UserRepository
                {
                    private static class Sql
                    {
                        /// <summary>
                        /// The <c>AsSql</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT 2;
                        /// </code>
                        /// </remarks>
                        public const string AsSql = "SELECT 2;";

                        /// <summary>
                        /// The <c>AsModels</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT 3;
                        /// </code>
                        /// </remarks>
                        public const string AsModels = "SELECT 3;";
                    }
                }
            }

            """
        );
    }

    [Fact]
    public void Emit_QueryThatKeepsItsComments_EmitsTheKeptForm()
    {
        var query = TestModels.Query(
            "GetUser",
            "SELECT 1;",
            keptSql: "SELECT 1; -- kept",
            markers: new SettingsLevel { Parameters = GeneratorParameters.KeepComments }
        );

        Emit(OutputKind.Sql, TestModels.Type(), File("Users.sql", query))
            .Source.ShouldNotBeNull()
            .ShouldContain("\"SELECT 1; -- kept\"");
    }

    [Fact]
    public void Emit_QueryThatKeepsCommentsButHasNoKeptForm_EmitsTheStrippedOne()
    {
        var query = TestModels.Query(
            "GetUser",
            "SELECT 1;",
            markers: new SettingsLevel { Parameters = GeneratorParameters.KeepComments }
        );

        Emit(OutputKind.Sql, TestModels.Type(), File("Users.sql", query))
            .Source.ShouldNotBeNull()
            .ShouldContain("\"SELECT 1;\"");
    }

    // The error of a kept form is the type's only when it emits that form: a type that does not keep the comments
    // uses the file.  The query is in the second file, whose level alone decides, and the first file's level says the
    // opposite, so the settings must be those of the query's own file.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Emit_QueryWithAnErrorInItsKeptForm_IsAnErrorAndItsFileGivesNoMembersOnlyWhereItKeepsComments(
        bool keepComments
    )
    {
        var keep = new SettingsLevel { Parameters = GeneratorParameters.KeepComments };
        var none = new SettingsLevel { Parameters = GeneratorParameters.None };
        var error = DiagnosticInfo.Create(SqlDiagnostics.ReservedTokenName, TestModels.NameLocation("B.sql"), "class");
        var output = Emit(
            OutputKind.Sql,
            TestModels.Type(),
            keepComments ? [none, keep] : [keep, none],
            File("A.sql", TestModels.Query("Kept", "SELECT 3;")),
            File(
                "B.sql",
                TestModels.Query("Other", "SELECT 1;", nameLocation: TestModels.NameLocation("B.sql")),
                TestModels.Query("GetUser", "SELECT 2;", nameLocation: TestModels.NameLocation("B.sql")) with
                {
                    KeptErrors = TestModels.Array(error),
                }
            )
        );

        var source = output.Source.ShouldNotBeNull();
        source.ShouldContain("public const string Kept = \"SELECT 3;\";");
        if (keepComments)
        {
            output.Diagnostics.ShouldBe([error]);
            source.ShouldNotContain("SELECT 1;");
            source.ShouldNotContain("SELECT 2;");
        }
        else
        {
            output.Diagnostics.ShouldBeEmpty();
            source.ShouldContain("public const string Other = \"SELECT 1;\";");
            source.ShouldContain("public const string GetUser = \"SELECT 2;\";");
        }
    }

    [Fact]
    public void Emit_SameInput_GivesEqualOutput()
    {
        static TypeOutput Create() =>
            Emit(OutputKind.Sql, TestModels.Type(), File("Users.sql", TestModels.Query("GetUser", "SELECT 1;")));

        Create().ShouldBe(Create());
    }

    // A theory cannot take the internal enum as a parameter.
    private static MemberPlacement Placement(bool direct) => direct ? MemberPlacement.Direct : MemberPlacement.Nested;

    // Each test states its output, which goes on each file's level.
    private static TypeOutput Emit(OutputKind? output, TargetType type, params ParsedSqlFile[] files) =>
        Emit(output, type, validateTokens: true, files);

    // The files under one level, of no-token-validation unless the tokens are validated.
    private static TypeOutput Emit(
        OutputKind? output,
        TargetType type,
        bool validateTokens,
        params ParsedSqlFile[] files
    )
    {
        var level = validateTokens
            ? SettingsLevel.None
            : new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation };
        return Emit(output, type, Array.ConvertAll(files, _ => level), files);
    }

    // The files, each under its level, one for each file, with the output on each.
    private static TypeOutput Emit(
        OutputKind? output,
        TargetType type,
        SettingsLevel[] fileSettings,
        params ParsedSqlFile[] files
    ) =>
        TypeEmitter.Emit(
            new TypeQueries(
                new TypeFiles(type, TestModels.Array<string>(), TestModels.Array<DiagnosticInfo>()),
                TestModels.Array(files),
                TestModels.Array(Array.ConvertAll(fileSettings, level => level with { Output = output })),
                HintName.Create(type)
            )
        );

    private static ParsedSqlFile File(string fileName, params SqlQuery[] queries) =>
        new(
            "app/Repo/" + fileName,
            fileName,
            TestModels.Array(queries),
            TestModels.Array<DiagnosticInfo>(),
            SqlDialect.Ansi
        );

    // "SELECT * FROM {{table}};", which is a method with one parameter.
    private static SqlQuery TokenQuery(string name, bool? tokenValidation = null, string file = "Users.sql") =>
        new(
            name,
            TestModels.NameLocation(file),
            null,
            null,
            TestModels.Array(
                TestModels.Literal("SELECT * FROM "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                TestModels.Literal(";")
            ),
            null,
            TestModels.Array(new SqlToken("table", null)),
            EquatableArray<SqlQueryParameter>.Empty,
            tokenValidation switch
            {
                false => new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation },
                true => new SettingsLevel { Parameters = GeneratorParameters.None },
                null => SettingsLevel.None,
            },
            null,
            null,
            EquatableArray<ColumnMarker>.Empty,
            EquatableArray<DiagnosticInfo>.Empty
        );
}
