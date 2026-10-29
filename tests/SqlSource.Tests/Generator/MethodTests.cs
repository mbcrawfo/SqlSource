// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The method of each query whose output is CodeGen: where it goes, how it is named, and which models it reads and
// binds.  Each run is at CodeGen under postgres on Npgsql 10, with an entry written for each query's current hash, and
// every method must compile without a warning.
public sealed class MethodTests
{
    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id, name FROM users WHERE id = @id;\n"
    );

    private static readonly SidecarEntry[] UsersEntries =
    [
        Entry(
            Users,
            "GetUser",
            [Parameter("id", Int4 with { BindName = "integer" })],
            [Column("id"), Column("name", Text, nullable: true)]
        ),
    ];

    [Fact]
    public void Run_CodeGenQuery_IsAnExtensionMethodOfTheTypesExtensionClass()
    {
        var run = MethodRuns.Run(Users, UsersEntries);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldBe(
                GeneratedFile.Joined(
                    $$$"""
                    {{{GeneratedFile.ModelHeader}}}
                    namespace App
                    {
                        /// <summary>
                        /// The query methods of <c>App.Sample</c>, which extend the connection they run on.
                        /// </summary>
                        public static partial class SampleExtensions
                        {
                            /// <summary>
                            /// The <c>GetUser</c> query from <c>Users.sql</c>.
                            /// </summary>
                            /// <remarks>
                            /// <code>
                            /// SELECT id, name FROM users WHERE id = @id;
                            /// </code>
                            /// </remarks>
                            /// <param name="connection">The connection to run the query on.  ↩
                        It is opened when it is closed, and closed again when the method opened it.</param>
                            /// <param name="parameters">The query's parameters.</param>
                            /// <param name="transaction">The transaction to run the query in, or null.</param>
                            /// <param name="commandTimeout">↩
                        The command's timeout in seconds, or null for the driver's.</param>
                            /// <param name="cancellationToken">Cancels the query.</param>
                            /// <returns>The rows the query returned.</returns>
                            public static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(
                                this global::System.Data.Common.DbConnection connection,
                                global::App.GetUserParams parameters,
                                global::System.Data.Common.DbTransaction? transaction = null,
                                int? commandTimeout = null,
                                global::System.Threading.CancellationToken cancellationToken = default)
                            {
                                global::System.ArgumentNullException.ThrowIfNull(connection);
                                global::System.ArgumentNullException.ThrowIfNull(parameters);
                                const string sql = "SELECT id, name FROM users WHERE id = @id;";
                                var wasClosed = connection.State == global::System.Data.ConnectionState.Closed;
                                if (wasClosed)
                                {
                                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                                }

                                try
                                {
                                    var command = connection.CreateCommand();
                                    await using var commandScope = ↩
                        global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(command, false);
                                    command.CommandText = sql;
                                    command.Transaction = transaction;
                                    if (commandTimeout.HasValue)
                                    {
                                        command.CommandTimeout = commandTimeout.Value;
                                    }

                                    {
                                        var parameter = new global::Npgsql.NpgsqlParameter<int>();
                                        parameter.ParameterName = "id";
                                        parameter.DataTypeName = "integer";
                                        parameter.TypedValue = parameters.Id;
                                        command.Parameters.Add(parameter);
                                    }

                                    var reader = await command.ExecuteReaderAsync(↩
                        global::System.Data.CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
                                    await using var readerScope = ↩
                        global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false);
                                    var rows = new global::App.SqlSourceRowBuffer<global::App.GetUserDto>();
                                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                    {
                                        rows.Add(new global::App.GetUserDto(reader.GetFieldValue<int>(0), ↩
                        reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1)));
                                    }

                                    return rows.ToArray();
                                }
                                finally
                                {
                                    if (wasClosed)
                                    {
                                        await connection.CloseAsync().ConfigureAwait(false);
                                    }
                                }
                            }
                        }

                    {{{GeneratedFile.RowBuffer}}}}

                    """
                )
            );
    }

    [Theory]
    [InlineData("Public", "public static")]
    [InlineData("Internal", "internal static")]
    [InlineData("Private", "private static")]
    public void Run_EachTypePlacement_PutsTheMethodOnTheType(string placement, string modifiers)
    {
        var run = MethodRuns.Run(
            Users,
            UsersEntries,
            MethodRuns.SampleSource("MethodLocation = MethodLocation." + placement)
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldStartWith(GeneratedFile.Header + "\nnamespace App\n{\n    partial class Sample\n    {\n");
        methods.ShouldContain(
            "        "
                + modifiers
                + " async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
                + "            global::System.Data.Common.DbConnection connection,\n"
        );
        methods.ShouldNotContain("SampleExtensions");
    }

    // The method takes the place of the query's constant, so the two never clash on the type.
    [Fact]
    public void Run_MethodOnTheTypeUnderDirect_TakesThePlaceOfTheConstant()
    {
        var run = MethodRuns.Run(
            Users,
            UsersEntries,
            MethodRuns.SampleSource("SqlLocation = SqlLocation.Direct, MethodLocation = MethodLocation.Public")
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser");
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                "        public static async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n"
            );
    }

    // A nested class that only CodeGen queries would fill is not written, so a method may have its name.
    [Fact]
    public void Run_MethodNamedLikeTheNestedClassOfCodeGenQueriesAlone_Compiles()
    {
        var run = MethodRuns.Run(
            Users,
            UsersEntries,
            MethodRuns.SampleSource(
                "MethodLocation = MethodLocation.Public, NestedClass = \"GetUserAsync\", "
                    + "Parameters = \"async-method-suffix\""
            )
        );

        run.ShouldBeClean();
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("class GetUserAsync");
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" GetUserAsync(\n");
    }

    // A query at sql in a file that SQLSRC008 leaves out fills no nested class, so the method still may have its name.
    [Fact]
    public void Run_MethodNamedLikeTheNestedClassBesideAFileLeftOut_Compiles()
    {
        var other = new SqlFile("/app/Repo/Zed.sql", "-- name: GetUser\n-- output: sql\nSELECT 2;\n");

        var run = MethodRuns.Run(
            MethodRuns.SampleSource(
                "MethodLocation = MethodLocation.Public, NestedClass = \"GetUserAsync\", "
                    + "Parameters = \"async-method-suffix\""
            ),
            Users,
            Sidecars.Of(Users, UsersEntries),
            other
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC008 /app/Repo/Zed.sql(");
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("class GetUserAsync");
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("SELECT 2;");
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" GetUserAsync(\n");
    }

    // A CodeGen query has no member in the nested class, so it may have the nested class's name: its method goes into
    // the extension class, alone, beside another CodeGen query, or beside a query at sql whose constant the nested
    // class holds.
    [Theory]
    [InlineData("")]
    [InlineData("-- name: GetUser\nSELECT id FROM users;\n")]
    [InlineData("-- name: GetName\n-- output: sql\nSELECT name FROM users;\n")]
    public void Run_CodeGenQueryNamedLikeTheNestedClass_IsAMethodOfTheExtensionClass(string other)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Sql\nSELECT id FROM users;\n" + other);
        var entries = new List<SidecarEntry> { Entry(file, "Sql", [], [Column("id")]) };
        if (other.Contains("GetUser", StringComparison.Ordinal))
        {
            entries.Add(Entry(file, "GetUser", [], [Column("id")]));
        }

        var run = MethodRuns.Run(file, [.. entries]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("    public static partial class SampleExtensions\n");
        methods.ShouldContain("Task<global::App.SqlDto[]> Sql(\n");
        if (other.Contains("GetUser", StringComparison.Ordinal))
        {
            methods.ShouldContain("Task<global::App.GetUserDto[]> GetUser(\n");
        }

        if (other.Contains("GetName", StringComparison.Ordinal))
        {
            run.Sources[GeneratorHarness.SampleFile]
                .ShouldContain("public const string GetName = \"SELECT name FROM users;\";");
        }
    }

    // Under SqlLocation.Direct a CodeGen query has no member on the type, so it may have the type's name when its
    // method goes into the extension class.
    [Fact]
    public void Run_CodeGenQueryNamedLikeTheTypeUnderDirect_IsAMethodOfTheExtensionClass()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Sample\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(
            file,
            [Entry(file, "Sample", [], [Column("id")])],
            MethodRuns.SampleSource("SqlLocation = SqlLocation.Direct")
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("    public static partial class SampleExtensions\n");
        methods.ShouldContain("Task<global::App.SampleDto[]> Sample(\n");
    }

    // A query at sql still has a constant in the nested class, so it cannot have its name, and its file, the CodeGen
    // query of it too, is left out.
    [Fact]
    public void Run_SqlQueryNamedLikeTheNestedClass_IsSqlsrc009()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: Sql\n-- output: sql\nSELECT 1;\n-- name: GetUser\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "GetUser", [], [Column("id")])]);

        run.Diagnostics.ShouldBe([
            "SQLSRC009 /app/Repo/Users.sql(1,10)-(1,13): A query of 'Sample' cannot be named 'Sql', because its member "
                + "would have the name of the type that contains it",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // One file of a query at each output, by its marker, under SqlLocation.Direct and MethodLocation.Public.
    [Fact]
    public void Run_MixedType_HasConstantsMethodsAndModels()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetName\n-- output: sql\nSELECT name FROM users WHERE id = @id;\n"
                + "-- name: GetEmail\n-- output: models\nSELECT email FROM users WHERE id = @id;\n"
                + "-- name: GetUser\n-- output: codegen\nSELECT id, name FROM users WHERE id = @id;\n"
        );

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    MethodRuns.SampleSource("SqlLocation = SqlLocation.Direct, MethodLocation = MethodLocation.Public")
                ),
            ],
            [
                file,
                Sidecars.Of(
                    file,
                    Entry(file, "GetEmail", [Parameter("id")], [Column("email", Text, nullable: true)]),
                    Entry(file, "GetUser", [Parameter("id")], [Column("id"), Column("name", Text, nullable: true)])
                ),
            ],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.ShouldBeClean();
        var type = run.Sources[GeneratorHarness.SampleFile];
        type.ShouldContain("public const string GetName = \"SELECT name FROM users WHERE id = @id;\";\n");
        type.ShouldContain("public const string GetEmail = \"SELECT email FROM users WHERE id = @id;\";\n");
        type.ShouldNotContain("GetUser");
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldNotContain("GetName");
        methods.ShouldNotContain("GetEmail");
        ModelFiles(run)
            .ShouldBe([
                "App.GetEmailDto-Model.g.cs",
                "App.GetEmailParams-Model.g.cs",
                GetUserDtoFile,
                "App.GetUserParams-Model.g.cs",
            ]);
    }

    [Fact]
    public void Run_InternalType_GivesAnInternalExtensionClass()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                internal partial class Sample;
            }
            """;

        var run = MethodRuns.Run(Users, UsersEntries, source);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("    internal static partial class SampleExtensions\n");
    }

    // One top-level class for each type, named by the type's own name, internal unless the type and every type around
    // it are public.
    [Fact]
    public void Run_NestedAndGenericTypes_GiveOneTopLevelExtensionClassEach()
    {
        var source = """
            using SqlSource;

            namespace App
            {
                public partial class Outer
                {
                    [SqlSourceGenerate(Path = "Users.sql")]
                    public partial class Inner;

                    [SqlSourceGenerate(Path = "Orders.sql")]
                    private partial class Hidden;
                }

                [SqlSourceGenerate(Path = "Lines.sql")]
                public partial class Repository<T>;
            }
            """;
        var orders = new SqlFile("/app/Repo/Orders.sql", "-- name: GetOrder\nSELECT id FROM orders;\n");
        var lines = new SqlFile("/app/Repo/Lines.sql", "-- name: GetLine\nSELECT id FROM lines;\n");

        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [
                Users,
                Sidecars.Of(Users, UsersEntries),
                orders,
                Sidecars.Of(orders, Entry(orders, "GetOrder", [], [Column("id")])),
                lines,
                Sidecars.Of(lines, Entry(lines, "GetLine", [], [Column("id")])),
            ],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.ShouldBeClean();
        run.Sources["App.Outer.Inner-Methods.g.cs"]
            .ShouldStartWith(
                GeneratedFile.ModelHeader
                    + "\nnamespace App\n{\n    /// <summary>\n"
                    + "    /// The query methods of <c>App.Outer.Inner</c>, which extend the connection they run on.\n"
                    + "    /// </summary>\n    public static partial class InnerExtensions\n    {\n"
            );
        run.Sources["App.Outer.Hidden-Methods.g.cs"]
            .ShouldContain("    internal static partial class HiddenExtensions\n");
        run.Sources["App.Repository-1-Methods.g.cs"]
            .ShouldContain(
                "    /// The query methods of <c>App.Repository&lt;T&gt;</c>, which extend the connection they run "
                    + "on.\n"
                    + "    /// </summary>\n    public static partial class RepositoryExtensions\n"
            );
    }

    [Fact]
    public void Run_AsyncMethodSuffix_NamesTheMethodWithAsync()
    {
        var run = MethodRuns.Run(Users, UsersEntries, MethodRuns.SampleSource("Parameters = \"async-method-suffix\""));

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" GetUserAsync(\n");
        run.Sources[MethodRuns.MethodsFile].ShouldNotContain(" GetUser(\n");
    }

    // Under sort-output a record's properties are in the order of their names, and its constructor takes each column
    // in that order: two columns of one type out of that order would compile in either order.
    [Fact]
    public void Run_SortedRecordOutput_PassesEachColumnToItsProperty()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT zeta, alpha FROM users;\n");

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetUser", [], [Column("zeta"), Column("alpha")])],
            MethodRuns.SampleSource("Parameters = \"sort-output\"")
        );

        run.ShouldBeClean();
        run.Sources[GetUserDtoFile].ShouldContain("record GetUserDto(int Alpha, int Zeta);");
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                "rows.Add(new global::App.GetUserDto(reader.GetFieldValue<int>(1), reader.GetFieldValue<int>(0)));\n"
            );
    }

    // A class is set by an object initializer, which names each property, so sort-output leaves it in column order.
    [Fact]
    public void Run_SortedClassOutput_SetsEachPropertyByItsName()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT zeta, alpha FROM users;\n");

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetUser", [], [Column("zeta"), Column("alpha")])],
            MethodRuns.SampleSource("Parameters = \"sort-output\", OutputModelType = GeneratorModelType.Class")
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                "rows.Add(new global::App.GetUserDto { Zeta = reader.GetFieldValue<int>(0), "
                    + "Alpha = reader.GetFieldValue<int>(1) });\n"
            );
    }

    // The method checks each token unless the query says no-token-validation.
    [Theory]
    [InlineData("", true)]
    [InlineData("-- generator: no-token-validation\n", false)]
    public void Run_TokenQuery_ChecksItsTokenUnlessNoTokenValidation(string marker, bool checks)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListFrom\n" + marker + "-- token: {{table:users}}\nSELECT id FROM {{table}};\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "ListFrom", [], [Column("id")])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" ListFrom(\n");
        methods.Contains("ThrowIfNullOrWhiteSpace(", StringComparison.Ordinal).ShouldBe(checks);
    }

    // The method's SQL keeps the query's comments under keep-comments, and has none without it.
    [Theory]
    [InlineData("", "SELECT id\\nFROM users;")]
    [InlineData("-- generator: keep-comments\n", "SELECT id -- the key\\nFROM users;")]
    public void Run_QueryWithAComment_KeepsItUnderKeepComments(string marker, string sql)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n" + marker + "SELECT id -- the key\nFROM users;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "GetUser", [], [Column("id")])]);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("const string sql = \"" + sql + "\";\n");
    }

    [Fact]
    public void Run_SharedModelAndTableModel_AreReadAndBoundByTheirNames()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetA\n-- input-model: ById\n-- output-model: Row\nSELECT id FROM a WHERE id = @id;\n"
                + "-- name: GetB\n-- input-model: ById\n-- output-model: Row\nSELECT id FROM b WHERE id = @id;\n"
                + "-- name: GetUser\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(
            file,
            [
                Entry(file, "GetA", [Parameter("id")], [Column("id")]),
                Entry(file, "GetB", [Parameter("id")], [Column("id")]),
                Entry(file, "GetUser", [], [Column("id")], new SidecarTable("public", "users")),
            ]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("Task<global::App.Row[]> GetA(\n");
        methods.ShouldContain("Task<global::App.Row[]> GetB(\n");
        methods.ShouldContain("            global::App.ById parameters,\n");
        methods.ShouldContain("rows.Add(new global::App.Row(reader.GetFieldValue<int>(0)));\n");
        methods.ShouldContain("Task<global::App.UsersDto[]> GetUser(\n");
        methods.ShouldContain("rows.Add(new global::App.UsersDto(reader.GetFieldValue<int>(0)));\n");
    }

    // The method names the model from global::, whatever its namespace.
    [Theory]
    [InlineData("", "global::GetUserDto")]
    [InlineData("namespace @class.Rows;\n\n", "global::@class.Rows.GetUserDto")]
    public void Run_ModelInTheGlobalNamespaceOrUnderAnEscapedKeyword_Compiles(string namespaceLine, string model)
    {
        var source = "using SqlSource;\n\n" + namespaceLine + "[SqlSourceGenerate]\npublic partial class Sample;\n";

        var run = MethodRuns.Run(Users, UsersEntries, source);

        run.ShouldBeClean();
        run.Sources.Values.ShouldContain(text => text.Contains("Task<" + model + "[]> GetUser(\n"));
    }

    [Fact]
    public void Run_TypeOverride_ReadsAndBindsTheOverridesType()
    {
        var run = MethodRuns.Run(
            Users,
            UsersEntries,
            settings: /*lang=json,strict*/
            """{"typeOverrides": {"int4": "long"}}"""
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("reader.GetFieldValue<long>(0)");
        run.Sources["App.GetUserParams-Model.g.cs"].ShouldContain("(long Id)");
    }

    // A PostgreSQL parameter is a parameter of Npgsql of the type of the value bound, by the bindName the entry
    // describes it by, as written: the database's type for a converted one.
    [Theory]
    [InlineData("base", "int", "integer")]
    [InlineData("array", "int[]", "integer[]")]
    [InlineData("enum", "string", "public.mood")]
    [InlineData("domain", "string", "public.email")]
    [InlineData("range", "global::NpgsqlTypes.NpgsqlRange<int>", "int4range")]
    [InlineData("multirange", "global::NpgsqlTypes.NpgsqlRange<int>[]", "int4multirange")]
    [InlineData("converted", "long", "bigint")]
    public void Run_ParameterOfEachKind_BindsItsBindName(string kind, string bound, string bindName)
    {
        var type = Array.Find(MethodRuns.ParameterKinds, row => row.Kind == kind).Type;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            MethodRuns.KindsOverride + "-- name: CountUsers\nSELECT count(*) AS n FROM users WHERE x = @p;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "CountUsers", [Parameter("p", type, nullable: false)], [Column("n")])],
            MethodRuns.KindsSource
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" var parameter = new global::Npgsql.NpgsqlParameter<" + bound + ">();\n");
        methods.ShouldContain(" parameter.DataTypeName = \"" + bindName + "\";\n");
    }

    [Fact]
    public void Run_ModelsOutput_GivesNoMethod()
    {
        var run = MethodRuns.Run(Users, UsersEntries, MethodRuns.SampleSource("Output = GeneratorOutput.Models"));

        run.ShouldBeClean();
        run.Sources.Keys.ShouldContain(GetUserDtoFile);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_QueryWithoutAUsableEntry_GivesNoMethod()
    {
        var stale = Users with { Text = "-- name: GetUser\nSELECT id, name FROM users WHERE id = @id AND 1 = 1;\n" };

        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [stale, Sidecars.Of(stale, UsersEntries)],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC244 ");
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }
}
