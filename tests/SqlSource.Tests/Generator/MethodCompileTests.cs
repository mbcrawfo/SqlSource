// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Methods;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The methods compiled against each driver the generator supports, read as metadata: a query of each shape, a token,
// and parameters and columns that can and cannot be null, of value and reference types.  The compilation reports every
// warning wave, so a method compiles with no error and no warning in what is generated.
public sealed class MethodCompileTests
{
    // The usings of a source whose members of each kind MembersOf gives, and the namespace it opens.
    private const string SourcesUsings = """
        using System.Data.Common;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Data.SqlClient;
        using Npgsql;
        using SqlSource;

        namespace App
        {

        """;

    private const string Generate = "[SqlSourceGenerate(Source = \".\")]";

    private const string PostgresMark = "[SqlSourceConnection(Database = \"postgres\")]";

    private const string SqlServerMark = "[SqlSourceConnection(Database = \"mssql\")]";

    private static readonly SqlFile Postgres = new(
        GeneratorHarness.UsersPath,
        """
        -- name: ListUsers
        SELECT id, name, score, note FROM users WHERE id = @id OR name = @name;
        -- name: GetUser -> one
        SELECT id, name, score, note FROM users WHERE id = @id OR name = @name;
        -- name: FindUser -> one-optional
        SELECT id, name, score, note FROM users WHERE id = @id OR name = @name;
        -- name: TouchUsers -> none
        UPDATE users SET note = NULL;
        -- name: RenameUser
        UPDATE users SET name = @name WHERE id = @id;
        -- name: ListFrom
        -- token: {{table:users}}
        SELECT id FROM {{table}};

        """
    );

    private static readonly SqlFile SqlServer = new(
        GeneratorHarness.UsersPath,
        """
        -- name: ListDocuments
        SELECT id, title, score, note, embedding, draft FROM documents WHERE id = @id OR title = @title;
        -- name: GetDocument -> one
        SELECT id, title, score, note, embedding, draft FROM documents WHERE id = @id OR title = @title;
        -- name: FindDocument -> one-optional
        SELECT id, title, score, note, embedding, draft FROM documents WHERE id = @id OR title = @title;
        -- name: TouchDocuments -> none
        UPDATE documents SET note = NULL;
        -- name: RenameDocument
        UPDATE documents SET title = @title WHERE id = @id;
        -- name: ListFrom
        -- token: {{table:documents}}
        SELECT id FROM {{table}};
        -- name: SaveEverything
        INSERT INTO everything VALUES (
            @a_char, @a_varchar, @a_nchar, @a_nvarchar, @a_sysname, @a_binary, @a_varbinary, @a_decimal, @a_numeric,
            @a_datetime2, @a_time, @a_datetimeoffset, @a_json, @a_timestamp, @a_vector, @a_null_vector, @a_bigint,
            @an_int, @a_smallint, @a_tinyint, @a_bit, @a_float, @a_real, @a_money, @a_smallmoney, @a_date,
            @a_datetime, @a_smalldatetime, @a_uniqueidentifier, @a_text, @an_ntext, @an_xml, @an_image,
            @a_sql_variant, @an_email);

        """
    );

    // A parameter of each SQL Server type that a method binds as its type, by the name, length, precision and scale
    // the server describes it with.  rowversion is left out: the server describes it as timestamp, and the map has no
    // row of its own for it.
    private static readonly (string Name, SqlServerType Type)[] EveryType =
    [
        ("a_char", TestSidecars.SqlServer("char(5)", 5, 0)),
        ("a_varchar", TestSidecars.SqlServer("varchar(50)", 50, 0)),
        ("a_nchar", TestSidecars.SqlServer("nchar(5)", 10, 0)),
        ("a_nvarchar", TestSidecars.SqlServer("nvarchar(50)", 100, 0)),
        ("a_sysname", TestSidecars.SqlServer("sysname", 256, 0)),
        ("a_binary", TestSidecars.SqlServer("binary(4)", 4, 0)),
        ("a_varbinary", TestSidecars.SqlServer("varbinary(max)", -1, 0)),
        ("a_decimal", TestSidecars.SqlServer("decimal(12,4)", 9, 12, 4)),
        ("a_numeric", TestSidecars.SqlServer("numeric(10,2)", 9, 10, 2)),
        ("a_datetime2", TestSidecars.SqlServer("datetime2(3)", 7, 23, 3)),
        ("a_time", TestSidecars.SqlServer("time(0)", 3, 8, 0)),
        ("a_datetimeoffset", TestSidecars.SqlServer("datetimeoffset(7)", 10, 34, 7)),
        ("a_json", TestSidecars.SqlServer("json", -1, 0)),
        ("a_timestamp", TestSidecars.SqlServer("timestamp", 8, 0)),
        ("a_vector", TestSidecars.SqlServer("vector(3)", 20, 0)),
        ("a_null_vector", TestSidecars.SqlServer("vector(3)", 20, 0)),
        ("a_bigint", TestSidecars.SqlServer("bigint", 8, 19)),
        ("an_int", TestSidecars.SqlServer()),
        ("a_smallint", TestSidecars.SqlServer("smallint", 2, 5)),
        ("a_tinyint", TestSidecars.SqlServer("tinyint", 1, 3)),
        ("a_bit", TestSidecars.SqlServer("bit", 1, 1)),
        ("a_float", TestSidecars.SqlServer("float", 8, 53)),
        ("a_real", TestSidecars.SqlServer("real", 4, 24)),
        ("a_money", TestSidecars.SqlServer("money", 8, 19, 4)),
        ("a_smallmoney", TestSidecars.SqlServer("smallmoney", 4, 10, 4)),
        ("a_date", TestSidecars.SqlServer("date", 3, 10)),
        ("a_datetime", TestSidecars.SqlServer("datetime", 8, 23, 3)),
        ("a_smalldatetime", TestSidecars.SqlServer("smalldatetime", 4, 16)),
        ("a_uniqueidentifier", TestSidecars.SqlServer("uniqueidentifier", 16, 0)),
        ("a_text", TestSidecars.SqlServer("text", 16, 0)),
        ("an_ntext", TestSidecars.SqlServer("ntext", 16, 0)),
        ("an_xml", TestSidecars.SqlServer("xml", -1, 0)),
        ("an_image", TestSidecars.SqlServer("image", 16, 0)),
        ("a_sql_variant", TestSidecars.SqlServer("sql_variant", 8016, 0)),
        ("an_email", TestSidecars.SqlServer("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null))),
    ];

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Run_QueriesOfEveryShapeOnNpgsql_Compile(int major)
    {
        var reference = major switch
        {
            8 => NpgsqlReferences.Npgsql8,
            9 => NpgsqlReferences.Npgsql9,
            _ => NpgsqlReferences.Npgsql10,
        };
        SidecarParameter[] parameters = [Parameter("id"), Parameter("name", Text, nullable: true)];
        SidecarColumn[] columns =
        [
            Column("id"),
            Column("name", Text),
            Column("score", nullable: true),
            Column("note", Text, nullable: true),
        ];

        var run = MethodRuns.Run(
            Postgres,
            [
                Entry(Postgres, "ListUsers", parameters, columns),
                Entry(Postgres, "GetUser", parameters, columns),
                Entry(Postgres, "FindUser", parameters, columns),
                Entry(Postgres, "TouchUsers", [], null),
                Entry(Postgres, "RenameUser", [Parameter("name", Text), Parameter("id")], null),
                Entry(Postgres, "ListFrom", [], [Column("id")]),
            ],
            references: [reference]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" ListUsers(\n");
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldContain(" FindUser(\n");
        methods.ShouldContain(" TouchUsers(\n");
        methods.ShouldContain(" RenameUser(\n");

        // A query without rows and without a shape counts the rows it changes.
        methods.ShouldContain("Task<int> RenameUser(\n");
        methods.ShouldContain("return await command.ExecuteNonQueryAsync(");
        methods.ShouldContain(" ListFrom(\n");
    }

    // A parameter of each kind PostgreSQL binds, each a parameter of Npgsql by its canonical name, and an integer that
    // can be null, a parameter of int?, compile alike on every Npgsql, the setter of DataTypeName and TypedValue
    // included.
    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Run_ParameterOfEachKindOnNpgsql_Compiles(int major)
    {
        var reference = major switch
        {
            8 => NpgsqlReferences.Npgsql8,
            9 => NpgsqlReferences.Npgsql9,
            _ => NpgsqlReferences.Npgsql10,
        };
        var kinds = MethodRuns.ParameterKinds;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            MethodRuns.KindsOverride
                + "-- name: TouchEach\nUPDATE things SET n = 1 WHERE "
                + string.Join(" AND ", kinds.Select(row => row.Kind + " = @" + row.Kind))
                + " AND maybe = @maybe;\n"
        );
        SidecarParameter[] parameters =
        [
            .. kinds.Select(row => Parameter(row.Kind, row.Type, nullable: false)),
            Parameter("maybe", Int4 with { BindName = "integer" }, nullable: true),
        ];

        var run = MethodRuns.Run(
            file,
            [Entry(file, "TouchEach", parameters, null)],
            MethodRuns.KindsSource,
            references: [reference]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        foreach (var row in kinds)
        {
            methods.ShouldContain(" parameter.DataTypeName = \"" + row.DataTypeName + "\";\n", customMessage: row.Kind);
        }

        // The block that binds maybe, from its declaration to its Add.
        var named = methods.IndexOf(" parameter.ParameterName = \"maybe\";\n", StringComparison.Ordinal);
        var start = methods.LastIndexOf(" var parameter = ", named, StringComparison.Ordinal);
        var maybe = methods[start..methods.IndexOf(" command.Parameters.Add(", named, StringComparison.Ordinal)];
        maybe.ShouldContain(" var parameter = new global::Npgsql.NpgsqlParameter<int?>();\n");
        maybe.ShouldContain(" parameter.DataTypeName = \"pg_catalog.int4\";\n");
        maybe.ShouldContain(" parameter.TypedValue = parameters.Maybe;\n");
    }

    // Queries of every shape, a token, and a parameter of each SQL Server type bound as its type compile on SqlClient
    // 6.1 and 7.  SqlClient reads a NULL of vector as SqlVector<float>.Null, so the method checks a not-null one with
    // IsNull; the map's text must be the very spelling the method writer looks for.
    [Theory]
    [InlineData("6.1")]
    [InlineData("7")]
    public void Run_QueriesOfEveryShapeOnSqlClient_Compile(string version)
    {
        var reference = version == "7" ? SqlClientReferences.SqlClient7 : SqlClientReferences.SqlClient61;
        SidecarParameter[] parameters =
        [
            TestSidecars.Parameter(0, "id", TestSidecars.SqlServer(), false),
            TestSidecars.Parameter(1, "title", TestSidecars.SqlServer("nvarchar", 200), true),
        ];
        SidecarColumn[] columns =
        [
            TestSidecars.Column(0, "id", TestSidecars.SqlServer()),
            TestSidecars.Column(1, "title", TestSidecars.SqlServer("nvarchar", 200)),
            TestSidecars.Column(2, "score", TestSidecars.SqlServer(), nullable: true),
            TestSidecars.Column(3, "note", TestSidecars.SqlServer("nvarchar", 200), nullable: true),
            TestSidecars.Column(4, "embedding", TestSidecars.SqlServer("vector(3)", 20, 0)),
            TestSidecars.Column(5, "draft", TestSidecars.SqlServer("vector(3)", 20, 0), nullable: true),
        ];

        var run = MethodRuns.RunSqlServer(
            SqlServer,
            [
                MethodRuns.SqlServerEntry(SqlServer, "ListDocuments", parameters, columns),
                MethodRuns.SqlServerEntry(SqlServer, "GetDocument", parameters, columns),
                MethodRuns.SqlServerEntry(SqlServer, "FindDocument", parameters, columns),
                MethodRuns.SqlServerEntry(SqlServer, "TouchDocuments", [], null),
                MethodRuns.SqlServerEntry(
                    SqlServer,
                    "RenameDocument",
                    [
                        TestSidecars.Parameter(0, "title", TestSidecars.SqlServer("nvarchar", 200), false),
                        TestSidecars.Parameter(1, "id", TestSidecars.SqlServer(), false),
                    ],
                    null
                ),
                MethodRuns.SqlServerEntry(
                    SqlServer,
                    "ListFrom",
                    [],
                    [TestSidecars.Column(0, "id", TestSidecars.SqlServer())]
                ),
                MethodRuns.SqlServerEntry(
                    SqlServer,
                    "SaveEverything",
                    [
                        .. EveryType.Select(
                            (parameter, index) =>
                                TestSidecars.Parameter(index, parameter.Name, parameter.Type, index % 2 == 1)
                        ),
                    ],
                    null
                ),
            ],
            references: [reference]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "var value4 = reader.GetFieldValue<global::Microsoft.Data.SqlTypes.SqlVector<float>>(4);\n"
        );
        methods.ShouldContain("if (value4.IsNull)\n");
        methods.ShouldNotContain("value5");
        methods.ShouldContain(" TouchDocuments(\n");
        methods.ShouldContain(" RenameDocument(\n");
        methods.ShouldContain("Task<int> RenameDocument(\n");
        methods.ShouldContain("return await command.ExecuteNonQueryAsync(");
        methods.ShouldContain(" ListFrom(\n");

        // Each parameter is bound as its type, a vector's null as the null vector of its dimension.
        methods.ShouldContain(" SaveEverything(\n");
        methods.ShouldContain(
            "var parameter = (global::Microsoft.Data.SqlClient.SqlParameter)command.CreateParameter();\n"
        );
        methods.ShouldContain("parameter.SqlDbType = global::System.Data.SqlDbType.DateTime2;\n");
        methods.ShouldContain("parameter.Precision = 12;\n");
        methods.ShouldContain("global::Microsoft.Data.SqlTypes.SqlVector<float>.CreateNull(3);\n");
        methods.ShouldContain("if (parameter.Value is byte[] { Length: > 4 })\n");
    }

    // A repository of each kind of connection source, for each engine: a connection string from a primary-constructor
    // parameter and from a method, which takes the token or not, a data source of the driver's type and of
    // DbDataSource, a factory that returns its connection and one that returns a task of it, and a borrowed connection,
    // through an instance member, a static member of a generic type and a parameter.  Each claims a postgres file and
    // an mssql file of a query that reads one row, one of many and one that streams.
    [Fact]
    public void Run_RepositoryOfEachKind_CompilesWithoutWarnings()
    {
        const string Source = """
            using System.Data.Common;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Data.SqlClient;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Strings(
                    [SqlSourceConnection(Database = "postgres")] string postgres,
                    [SqlSourceConnection(Database = "mssql")] string? sqlServer
                );

                [SqlSourceGenerate(Source = ".")]
                public partial class StringMethods
                {
                    [SqlSourceConnection(Database = "postgres")]
                    private string Postgres() => "";

                    [SqlSourceConnection(Database = "mssql")]
                    private static string? SqlServer(CancellationToken cancellationToken) => null;
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class DataSources
                {
                    [SqlSourceConnection(Database = "postgres")]
                    private readonly NpgsqlDataSource _postgres = NpgsqlDataSource.Create("");

                    [SqlSourceConnection(Database = "mssql")]
                    private DbDataSource? SqlServer { get; set; }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Factories
                {
                    [SqlSourceConnection(Database = "postgres")]
                    private static NpgsqlConnection Open() => new();

                    [SqlSourceConnection(Database = "mssql")]
                    private async ValueTask<SqlConnection?> OpenAsync(CancellationToken cancellationToken)
                    {
                        await Task.Delay(0, cancellationToken).ConfigureAwait(false);
                        return new SqlConnection();
                    }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Borrowed<T>(
                    [SqlSourceConnection(Database = "mssql")] SqlConnection sqlServer
                )
                {
                    [SqlSourceConnection(Database = "postgres")]
                    private static DbConnection? Postgres { get; set; }
                }
            }
            """;
        var run = RunRepositories(Source);

        run.ShouldBeClean();
        run.Sources["App.Strings-Methods.g.cs"].ShouldContain("new global::Npgsql.NpgsqlConnection(postgres ?? ");
        run.Sources["App.Strings-Methods.g.cs"]
            .ShouldContain("new global::Microsoft.Data.SqlClient.SqlConnection(sqlServer ?? ");
        run.Sources["App.StringMethods-Methods.g.cs"].ShouldContain("(this.Postgres() ?? ");
        run.Sources["App.StringMethods-Methods.g.cs"]
            .ShouldContain("(global::App.StringMethods.SqlServer(cancellationToken) ?? ");
        run.Sources["App.DataSources-Methods.g.cs"].ShouldContain("await (this._postgres ?? ");
        run.Sources["App.DataSources-Methods.g.cs"].ShouldContain("await (this.SqlServer ?? ");
        run.Sources["App.Factories-Methods.g.cs"].ShouldContain("= global::App.Factories.Open() ?? ");
        run.Sources["App.Factories-Methods.g.cs"]
            .ShouldContain("= await this.OpenAsync(cancellationToken).ConfigureAwait(false) ?? ");
        run.Sources["App.Borrowed-1-Methods.g.cs"]
            .ShouldContain("var connection = global::App.Borrowed<T>.Postgres ?? ");
        run.Sources["App.Borrowed-1-Methods.g.cs"].ShouldContain("var connection = sqlServer ?? ");
    }

    // A struct repository of each kind, whose streaming methods read the source before their iterator, which cannot
    // reach this or a primary-constructor parameter of a struct.
    [Fact]
    public void Run_StructRepositoryOfEachKind_CompilesWithoutWarnings()
    {
        const string Source = """
            using System.Data.Common;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Data.SqlClient;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial struct Strings(
                    [SqlSourceConnection(Database = "postgres")] string postgres,
                    [SqlSourceConnection(Database = "mssql")] string? sqlServer
                );

                [SqlSourceGenerate(Source = ".")]
                public partial struct StringMethods
                {
                    [SqlSourceConnection(Database = "postgres")]
                    private static string Postgres() => "";

                    [SqlSourceConnection(Database = "mssql")]
                    private readonly string? SqlServer(CancellationToken cancellationToken) => null;
                }

                [SqlSourceGenerate(Source = ".")]
                public partial struct DataSources
                {
                    [SqlSourceConnection(Database = "postgres")]
                    private NpgsqlDataSource? Postgres { get; set; }

                    [SqlSourceConnection(Database = "mssql")]
                    private DbDataSource? _sqlServer;
                }

                [SqlSourceGenerate(Source = ".")]
                public partial record struct Factories
                {
                    [SqlSourceConnection(Database = "postgres")]
                    private readonly NpgsqlConnection Open() => new();

                    [SqlSourceConnection(Database = "mssql")]
                    private readonly ValueTask<SqlConnection?> OpenAsync(CancellationToken cancellationToken) =>
                        new(new SqlConnection());
                }

                [SqlSourceGenerate(Source = ".")]
                public partial struct Borrowed(
                    [SqlSourceConnection(Database = "postgres")] DbConnection postgres
                )
                {
                    [SqlSourceConnection(Database = "mssql")]
                    public SqlConnection? SqlServer { get; set; }
                }
            }
            """;

        var run = RunRepositories(Source);

        run.ShouldBeClean();
        run.Sources["App.Strings-Methods.g.cs"].ShouldContain("var source = postgres;\n");
        run.Sources["App.StringMethods-Methods.g.cs"].ShouldContain("var self = this;\n");
        run.Sources["App.StringMethods-Methods.g.cs"].ShouldContain("(self.SqlServer(cancellationToken) ?? ");
        run.Sources["App.DataSources-Methods.g.cs"].ShouldContain("var source = this._sqlServer;\n");
        run.Sources["App.Factories-Methods.g.cs"].ShouldContain("var self = this;\n");
        run.Sources["App.Factories-Methods.g.cs"]
            .ShouldContain("= await self.OpenAsync(cancellationToken).ConfigureAwait(false) ?? ");
        run.Sources["App.Borrowed-Methods.g.cs"].ShouldContain("var connection = source ?? ");
    }

    // A struct's marked factory beside an unmarked overload of it, which a method group of the name could not convert
    // to a delegate, is called through a copy of the struct, where the call resolves the overload as written.
    [Fact]
    public void Run_StructRepositoryWithAnOverloadedFactory_CompilesWithoutWarnings()
    {
        const string Source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Data.SqlClient;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial struct Factories
                {
                    public readonly NpgsqlConnection Open() => new();

                    [SqlSourceConnection(Database = "postgres")]
                    public readonly NpgsqlConnection Open(CancellationToken cancellationToken) => new();

                    public readonly Task<SqlConnection> OpenAsync() => Task.FromResult(new SqlConnection());

                    [SqlSourceConnection(Database = "mssql")]
                    public readonly Task<SqlConnection> OpenAsync(CancellationToken cancellationToken) =>
                        Task.FromResult(new SqlConnection());
                }

                [SqlSourceGenerate(Source = ".")]
                public partial record struct RecordFactories
                {
                    public readonly NpgsqlConnection Open() => new();

                    [SqlSourceConnection(Database = "postgres")]
                    public readonly NpgsqlConnection Open(CancellationToken cancellationToken) => new();

                    public readonly SqlConnection OpenSqlServer() => new();

                    [SqlSourceConnection(Database = "mssql")]
                    public readonly SqlConnection OpenSqlServer(CancellationToken cancellationToken) => new();
                }
            }
            """;

        var run = RunRepositories(Source);

        run.ShouldBeClean();
        run.Sources["App.Factories-Methods.g.cs"].ShouldContain("= self.Open(cancellationToken) ?? ");
        run.Sources["App.Factories-Methods.g.cs"]
            .ShouldContain("= await self.OpenAsync(cancellationToken).ConfigureAwait(false) ?? ");
        run.Sources["App.RecordFactories-Methods.g.cs"].ShouldContain("= self.OpenSqlServer(cancellationToken) ?? ");
    }

    // A repository of a type that the interface's declaration and the base list of the type's part must repeat: a
    // generic type with a constraint, which the interface leaves out, a type nested in a generic class, whose
    // interface is its sibling, internal as the type is, a generic type nested in one, and a struct, a record and a
    // record struct.
    [Theory]
    [InlineData(
        "[SqlSourceGenerate(Source = \".\")]\n    public partial class Repo<T>\n        where T : class\n"
            + "    {\n{0}    }",
        "partial class Repo<T> : global::App.IRepo<T>\n",
        "    public partial interface IRepo<T>\n"
    )]
    [InlineData(
        "public partial class Outer<U>\n    {\n        [SqlSourceGenerate(Source = \".\")]\n"
            + "        internal partial class Repo\n        {\n{0}        }\n    }",
        "partial class Repo : global::App.Outer<U>.IRepo\n",
        "        internal partial interface IRepo\n"
    )]
    [InlineData(
        "public partial class Outer<U>\n    {\n        [SqlSourceGenerate(Source = \".\")]\n"
            + "        public partial class Repo<T>\n        {\n{0}        }\n    }",
        "partial class Repo<T> : global::App.Outer<U>.IRepo<T>\n",
        "        public partial interface IRepo<T>\n"
    )]
    [InlineData(
        "[SqlSourceGenerate(Source = \".\")]\n    public partial struct Repo\n    {\n{0}    }",
        "partial struct Repo : global::App.IRepo\n",
        "    public partial interface IRepo\n"
    )]
    [InlineData(
        "[SqlSourceGenerate(Source = \".\")]\n    public partial record Repo\n    {\n{0}    }",
        "partial record Repo : global::App.IRepo\n",
        "    public partial interface IRepo\n"
    )]
    [InlineData(
        "[SqlSourceGenerate(Source = \".\")]\n    public partial record struct Repo\n    {\n{0}    }",
        "partial record struct Repo : global::App.IRepo\n",
        "    public partial interface IRepo\n"
    )]
    public void Run_RepositoryOnAwkwardTypes_Compiles(string declaration, string implements, string contract)
    {
        const string Members = """
                    [SqlSourceConnection(Database = "postgres")]
                    private static string Postgres() => "";

                    [SqlSourceConnection(Database = "mssql")]
                    private static string SqlServer() => "";

            """;
        var source =
            "using SqlSource;\n\nnamespace App\n{\n    "
            + declaration.Replace("{0}", Members, StringComparison.Ordinal)
            + "\n}\n";

        var run = RunRepositories(source);

        run.ShouldBeClean();
        var methods = run.Sources.Single(pair => pair.Key.EndsWith("-Methods.g.cs", StringComparison.Ordinal)).Value;
        methods.ShouldContain(implements);
        methods.ShouldContain(contract);
        methods.ShouldContain(" FindUser(\n");
        methods.ShouldContain(" StreamDocuments(\n");
    }

    // A repository that holds a factory of each kind of connection source, for each engine, through a parameter and
    // through a field, reached through the holder, which each method checks for null first.
    [Theory]
    [InlineData("connection string", "parameter")]
    [InlineData("connection string", "field")]
    [InlineData("data source", "parameter")]
    [InlineData("data source", "field")]
    [InlineData("factory", "parameter")]
    [InlineData("factory", "field")]
    [InlineData("borrowed", "parameter")]
    [InlineData("borrowed", "field")]
    public void Run_HeldFactoryOfEachKind_Compiles(string kind, string holder)
    {
        var repository =
            holder == "parameter"
                ? "    public partial class Repo(Factory factory);\n"
                : "    public partial class Repo\n    {\n        private readonly Factory _factory = new();\n    }\n";
        var source =
            SourcesUsings
            + "    public class Factory\n    {\n"
            + MembersOf(kind, string.Empty)
            + "    }\n\n    [SqlSourceGenerate(Source = \".\")]\n"
            + repository
            + "}\n";

        var run = RunRepositories(source);

        run.ShouldBeClean();
        var methods = run.Sources["App.Repo-Methods.g.cs"];
        methods.ShouldContain(holder == "parameter" ? "(factory ?? throw " : "(this._factory ?? throw ");
        methods.ShouldContain("\"The connection source '" + (holder == "parameter" ? "factory" : "_factory"));
    }

    // A holder that can be null, of an interface and of a struct, whose value a method reads after its check, in a
    // project whose code has nullable annotations on.
    [Fact]
    public void Run_NullableHolder_CompilesWithoutWarnings()
    {
        const string Source = """
            #nullable enable
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Data.SqlClient;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection(Database = "postgres")]
                    Task<NpgsqlConnection> Open(CancellationToken cancellationToken);

                    [SqlSourceConnection(Database = "mssql")]
                    string SqlServer { get; }
                }

                public struct StructFactory
                {
                    [SqlSourceConnection(Database = "postgres")]
                    public readonly NpgsqlConnection Open() => new();

                    [SqlSourceConnection(Database = "mssql")]
                    public readonly ValueTask<SqlConnection?> OpenAsync(CancellationToken cancellationToken) =>
                        new(new SqlConnection());
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Interfaces(IDatabaseFactory? dbf);

                [SqlSourceGenerate(Source = ".")]
                public partial class Structs(StructFactory? dbf);

                [SqlSourceGenerate(Source = ".")]
                public partial struct StructsInAStruct(StructFactory? dbf);
            }
            """;

        var run = RunRepositories(Source);

        run.ShouldBeClean();
        run.Sources["App.Interfaces-Methods.g.cs"].ShouldContain("(dbf ?? throw ");
        run.Sources["App.Structs-Methods.g.cs"].ShouldContain("(dbf ?? throw ");
        run.Sources["App.StructsInAStruct-Methods.g.cs"].ShouldContain("var source = dbf ?? throw ");
    }

    // A holder of a struct, or of a type parameter constrained to one, can never be null, so a method reads its member
    // with no check, which ?? could not apply to: in a class, and copied before a struct's iterator.
    [Fact]
    public void Run_HolderThatCannotBeNull_CompilesWithoutWarnings()
    {
        var source =
            SourcesUsings
            + """
                    public interface IFactory
                    {
                        [SqlSourceConnection(Database = "postgres")]
                        NpgsqlConnection Postgres();

                        [SqlSourceConnection(Database = "mssql")]
                        ValueTask<SqlConnection?> SqlServer(CancellationToken cancellationToken);
                    }

                    public struct Factory : IFactory
                    {

                """
            + MembersOf("factory", "readonly ")
            + """
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Structs(Factory dbf);

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Constrained<T>(T dbf)
                        where T : struct, IFactory;

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct StructsInAStruct(Factory dbf);
                }

                """;

        var run = RunRepositories(source);

        run.ShouldBeClean();
        run.Sources["App.Structs-Methods.g.cs"].ShouldContain("= dbf.Postgres() ?? throw ");
        run.Sources["App.Constrained-1-Methods.g.cs"].ShouldContain("= dbf.Postgres() ?? throw ");
        run.Sources["App.StructsInAStruct-Methods.g.cs"].ShouldContain("var source = dbf;\n");
        run.Sources["App.StructsInAStruct-Methods.g.cs"].ShouldContain("= source.Postgres() ?? throw ");
    }

    // A holder of a type parameter that may be a class or a struct, which ?? can test, and a property holder, each
    // checked for null first, in a class and copied before a struct's iterator.
    [Fact]
    public void Run_TypeParameterAndPropertyHolders_Compile()
    {
        var source =
            SourcesUsings
            + """
                    public interface IFactory
                    {
                        [SqlSourceConnection(Database = "postgres")]
                        NpgsqlConnection Postgres();

                        [SqlSourceConnection(Database = "mssql")]
                        ValueTask<SqlConnection?> SqlServer(CancellationToken cancellationToken);
                    }

                    public class Factory : IFactory
                    {
                        public NpgsqlConnection Postgres() => new();

                        public ValueTask<SqlConnection?> SqlServer(CancellationToken cancellationToken) =>
                            new(new SqlConnection());
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Generic<T>(T dbf)
                        where T : IFactory;

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct GenericStruct<T>(T dbf)
                        where T : IFactory;

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Properties
                    {
                        private Factory? Holder { get; } = new();
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct PropertiesInAStruct
                    {
                        public PropertiesInAStruct() { }

                        private Factory Holder { get; } = new();
                    }
                }

                """;

        var run = RunRepositories(source);

        run.ShouldBeClean();
        run.Sources["App.Generic-1-Methods.g.cs"].ShouldContain("= (dbf ?? throw ");
        run.Sources["App.GenericStruct-1-Methods.g.cs"].ShouldContain("var source = dbf ?? throw ");
        run.Sources["App.Properties-Methods.g.cs"].ShouldContain("= (this.Holder ?? throw ");
        run.Sources["App.PropertiesInAStruct-Methods.g.cs"].ShouldContain("var source = this.Holder ?? throw ");
    }

    // A factory of each kind of connection source, for each engine, that implements the marked members of its
    // interface explicitly, in a class and in a struct, reached through a cast of the holder to the interface:
    // through a parameter, a field, a property, a static field, a Nullable<T> of the struct, the struct, which cannot
    // be null, a type parameter constrained to the class, and in a struct repository, whose streaming methods cast a
    // copy of the holder, of the class and of the struct, in a project whose code has nullable annotations on.
    [Theory]
    [InlineData("connection string")]
    [InlineData("data source")]
    [InlineData("factory")]
    [InlineData("borrowed")]
    public void Run_HeldExplicitImplementationOfEachKind_Compiles(string kind)
    {
        var (members, implementations) = ExplicitMembersOf(kind);
        var source =
            "#nullable enable\n"
            + SourcesUsings
            + "    public interface IFactory\n    {\n"
            + members
            + "    }\n\n    public class Factory : IFactory\n    {\n"
            + implementations
            + "    }\n\n    public struct StructFactory : IFactory\n    {\n"
            + implementations.Replace("        ", "        readonly ", StringComparison.Ordinal)
            + """
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Parameters(Factory factory);

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Fields
                    {
                        private readonly Factory? _factory = new();
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Properties
                    {
                        private Factory Holder { get; } = new();
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Statics
                    {
                        private static readonly Factory Shared = new();
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class NullableStructs(StructFactory? factory);

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Structs(StructFactory factory);

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Constrained<T>(T factory)
                        where T : Factory;

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct InAStruct(Factory factory);

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct StructsInAStruct(StructFactory factory);
                }

                """;

        var run = RunRepositories(source);

        run.ShouldBeClean();
        run.Sources["App.Parameters-Methods.g.cs"].ShouldContain("((global::App.IFactory)(factory ?? throw ");
        run.Sources["App.Fields-Methods.g.cs"].ShouldContain("((global::App.IFactory)(this._factory ?? throw ");
        run.Sources["App.Properties-Methods.g.cs"].ShouldContain("((global::App.IFactory)(this.Holder ?? throw ");
        run.Sources["App.Statics-Methods.g.cs"]
            .ShouldContain("((global::App.IFactory)(global::App.Statics.Shared ?? throw ");
        run.Sources["App.NullableStructs-Methods.g.cs"].ShouldContain("((global::App.IFactory)(factory ?? throw ");
        run.Sources["App.Structs-Methods.g.cs"].ShouldContain("((global::App.IFactory)factory).");
        run.Sources["App.Constrained-1-Methods.g.cs"].ShouldContain("((global::App.IFactory)(factory ?? throw ");
        run.Sources["App.InAStruct-Methods.g.cs"].ShouldContain("var source = factory ?? throw ");
        run.Sources["App.InAStruct-Methods.g.cs"].ShouldContain("((global::App.IFactory)source).");
        run.Sources["App.StructsInAStruct-Methods.g.cs"].ShouldContain("var source = factory;\n");
        run.Sources["App.StructsInAStruct-Methods.g.cs"].ShouldContain("((global::App.IFactory)source).");
    }

    // A factory that implements one generic interface twice, explicitly, once for each engine's connection, and once
    // with a nullable annotation, gives each engine its own member, reached through the interface closed as the class
    // implements it.
    [Fact]
    public void Run_HeldExplicitImplementationOfAGenericInterface_Compiles()
    {
        var source =
            "#nullable enable\n"
            + SourcesUsings
            + """
                    public interface IFactory<TConnection>
                    {
                        [SqlSourceConnection]
                        TConnection Open();
                    }

                    public class Factory : IFactory<NpgsqlConnection>, IFactory<SqlConnection?>
                    {
                        NpgsqlConnection IFactory<NpgsqlConnection>.Open() => new();

                        SqlConnection? IFactory<SqlConnection?>.Open() => new();
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Repo(Factory factory);

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct StructRepo(Factory factory);
                }

                """;

        var run = RunRepositories(source);

        run.ShouldBeClean();
        var methods = run.Sources["App.Repo-Methods.g.cs"];
        methods.ShouldContain("((global::App.IFactory<global::Npgsql.NpgsqlConnection>)(factory ?? throw ");
        methods.ShouldContain("((global::App.IFactory<global::Microsoft.Data.SqlClient.SqlConnection?>)(factory ?? ");
        run.Sources["App.StructRepo-Methods.g.cs"]
            .ShouldContain("((global::App.IFactory<global::Npgsql.NpgsqlConnection>)source).Open()");
    }

    // A factory whose base class implements the marked members of its interface, each of which the factory hides with
    // a new member, a method of its signature and a property of its name, gives each engine the base class's member
    // through a cast of the holder to the interface: through a parameter, a type parameter that the class and the
    // interface constrain, and in a struct repository, whose streaming methods cast a copy of the holder.
    [Fact]
    public void Run_HeldImplementationThatANewMemberHides_Compiles()
    {
        var source =
            "#nullable enable\n"
            + SourcesUsings
            + """
                    public interface IFactory
                    {
                        [SqlSourceConnection(Database = "postgres")]
                        NpgsqlConnection Postgres();

                        [SqlSourceConnection(Database = "mssql")]
                        ValueTask<SqlConnection?> SqlServer(CancellationToken cancellationToken);
                    }

                    public class FactoryBase : IFactory
                    {
                        public NpgsqlConnection Postgres() => new();

                        public ValueTask<SqlConnection?> SqlServer(CancellationToken cancellationToken) =>
                            new(new SqlConnection());
                    }

                    public class Factory : FactoryBase
                    {
                        public new NpgsqlConnection Postgres() => new();

                        public new string SqlServer => "";
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Parameters(Factory factory);

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Constrained<T>(T factory)
                        where T : Factory, IFactory;

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct InAStruct(Factory factory);
                }

                """;

        var run = RunRepositories(source);

        run.ShouldBeClean();
        var parameters = run.Sources["App.Parameters-Methods.g.cs"];
        parameters.ShouldContain("((global::App.IFactory)(factory ?? throw ");
        parameters.ShouldContain(")).Postgres()");
        parameters.ShouldContain(")).SqlServer(cancellationToken)");
        run.Sources["App.Constrained-1-Methods.g.cs"].ShouldContain("((global::App.IFactory)(factory ?? throw ");
        run.Sources["App.InAStruct-Methods.g.cs"].ShouldContain("((global::App.IFactory)source).");
    }

    // A static of the project of each kind of connection source, for each engine, reached through its type in full.
    [Theory]
    [InlineData("connection string")]
    [InlineData("data source")]
    [InlineData("factory")]
    [InlineData("borrowed")]
    public void Run_GlobalOfEachKind_Compiles(string kind)
    {
        var source =
            SourcesUsings
            + "    public static class Connections\n    {\n"
            + MembersOf(kind, "static ")
            + "    }\n\n    [SqlSourceGenerate(Source = \".\")]\n    public partial class Repo;\n\n"
            + "    [SqlSourceGenerate(Source = \".\")]\n    public partial struct StructRepo;\n}\n";

        var run = RunRepositories(source);

        run.ShouldBeClean();
        run.Sources["App.Repo-Methods.g.cs"].ShouldContain("global::App.Connections.Postgres");
        run.Sources["App.StructRepo-Methods.g.cs"].ShouldContain("global::App.Connections.SqlServer");
    }

    // A struct repository whose streaming methods reach a held member through a copy of the holder, checked before
    // their iterator, which cannot reach this or a primary-constructor parameter of a struct: a parameter, a field and
    // a static field.
    [Fact]
    public void Run_StructRepositoryStreamingThroughHolder_Compiles()
    {
        var source =
            SourcesUsings
            + "    public class Factory\n    {\n"
            + MembersOf("factory", string.Empty)
            + """
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct Parameters(Factory factory);

                    [SqlSourceGenerate(Source = ".")]
                    public partial struct Fields
                    {
                        private readonly Factory? _factory;
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial record struct Statics
                    {
                        private static readonly Factory Shared = new();
                    }
                }

                """;

        var run = RunRepositories(source);

        run.ShouldBeClean();
        run.Sources["App.Parameters-Methods.g.cs"].ShouldContain("var source = factory ?? throw ");
        run.Sources["App.Parameters-Methods.g.cs"].ShouldContain("= source.Postgres() ?? throw ");
        run.Sources["App.Fields-Methods.g.cs"].ShouldContain("var source = this._factory ?? throw ");
        run.Sources["App.Statics-Methods.g.cs"].ShouldContain("var source = global::App.Statics.Shared ?? throw ");
        run.Sources["App.Statics-Methods.g.cs"]
            .ShouldContain("= await source.SqlServer(cancellationToken).ConfigureAwait(false) ?? throw ");
    }

    // A static source of a base type may have the repository's name, which inside the repository binds to the member
    // before the type: the access goes through the type in full, wherever the type is declared.
    [Theory]
    [MemberData(nameof(RepositoriesNamedAsTheirBaseMember))]
    public void Run_StaticSourceWithTheRepositorysName_Compiles(
        string open,
        string close,
        string member,
        string repository,
        string access
    )
    {
        var source =
            SourcesUsings.Replace("namespace App\n{\n", string.Empty, StringComparison.Ordinal)
            + open
            + "\npublic abstract class RepositoryBase\n{\n"
            + "    [SqlSourceConnection(Database = \"postgres\")]\n"
            + $"    protected static string? {member} {{ get; set; }}\n\n"
            + "    [SqlSourceConnection(Database = \"mssql\")]\n"
            + "    protected static string? Other { get; set; }\n}\n\n"
            + repository
            + "\n"
            + close
            + "\n";

        var run = RunRepositories(source);

        run.ShouldBeClean();
        run.Sources.Values.ShouldContain(text => text.Contains(access, StringComparison.Ordinal));
    }

    // A static holder of a base type may have the repository's name too, and its factory's members are reached through
    // the same access.
    [Theory]
    [MemberData(nameof(RepositoriesNamedAsTheirBaseMember))]
    public void Run_StaticHolderWithTheRepositorysName_Compiles(
        string open,
        string close,
        string member,
        string repository,
        string access
    )
    {
        var source =
            SourcesUsings.Replace("namespace App\n{\n", string.Empty, StringComparison.Ordinal)
            + open
            + "\npublic class Factory\n{\n"
            + MembersOf("factory", string.Empty)
            + "}\n\npublic abstract class RepositoryBase\n{\n"
            + $"    protected static Factory? {member} {{ get; set; }}\n}}\n\n"
            + repository
            + "\n"
            + close
            + "\n";

        var run = RunRepositories(source);

        run.ShouldBeClean();
        run.Sources.Values.ShouldContain(text => text.Contains(access, StringComparison.Ordinal));
    }

    // Where a repository named as its base type's static member can be declared: what opens and closes its namespace,
    // the member's name as written, the repository's declaration, and the access the method writes.
    public static TheoryData<string, string, string, string, string> RepositoriesNamedAsTheirBaseMember =>
        new()
        {
            {
                "namespace App {",
                "}",
                "Sample",
                "[SqlSourceGenerate(Source = \".\")] public partial class Sample : RepositoryBase;",
                "global::App.Sample.Sample"
            },
            {
                string.Empty,
                string.Empty,
                "Sample",
                "[SqlSourceGenerate(Source = \".\")] public partial class Sample : RepositoryBase;",
                "global::Sample.Sample"
            },
            {
                "namespace Sample {",
                "}",
                "Sample",
                "[SqlSourceGenerate(Source = \".\")] public partial class Sample : RepositoryBase;",
                "global::Sample.Sample.Sample"
            },
            {
                "namespace @class {",
                "}",
                "@event",
                "[SqlSourceGenerate(Source = \".\")] public partial class @event : RepositoryBase;",
                "global::@class.@event.@event"
            },
            {
                "namespace App {",
                "}",
                "Sample",
                "[SqlSourceGenerate(Source = \".\")] public partial class Sample<T> : RepositoryBase;",
                "global::App.Sample<T>.Sample"
            },
            {
                "namespace App {",
                "}",
                "parameters",
                "[SqlSourceGenerate(Source = \".\")] public partial class parameters : RepositoryBase;",
                "global::App.parameters.parameters"
            },
            {
                "namespace App {",
                "}",
                "Sample",
                "public partial class await { [SqlSourceGenerate(Source = \".\")] "
                    + "public partial class Sample : RepositoryBase; }",
                "global::App.@await.Sample.Sample"
            },
            {
                "namespace App {",
                "}",
                "Sample",
                "[SqlSourceGenerate(Source = \".\")] public partial class Sample<await> : RepositoryBase;",
                "global::App.Sample<@await>.Sample"
            },
        };

    // A type parameter around the repository can have no name inside it, when a nearer type repeats it or has a nested
    // type of its name, so a path from global:: could name another constructed type.  A repository nested in a generic
    // type is reached through its own name.  The interface is off, whose declaration repeats the parameter.
    [Theory]
    [InlineData(
        "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : RepositoryBase; }",
        "Repo.Source"
    )]
    [InlineData(
        "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<T> : RepositoryBase; }",
        "Repo<T>.Source"
    )]
    [InlineData(
        "public partial class Outer<@T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<T> : RepositoryBase; }",
        "Repo<T>.Source"
    )]
    [InlineData(
        "public partial class A<T> { public partial class B<T> { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : RepositoryBase; } }",
        "Repo.Source"
    )]
    [InlineData(
        "public partial class A<T> { public partial class B { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : RepositoryBase; } }",
        "Repo.Source"
    )]
    [InlineData(
        "public partial class A { public partial class B<T> { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : RepositoryBase; } }",
        "Repo.Source"
    )]
    [InlineData(
        "public partial class A<T, U> { public partial class B<U> { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<T> : RepositoryBase; } }",
        "Repo<T>.Source"
    )]
    [InlineData(
        "public partial class A<T> { public partial class B<U> { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<T> : RepositoryBase; } }",
        "Repo<T>.Source"
    )]
    [InlineData(
        "public class HidingBase : RepositoryBase { public class T { } } public partial class Outer<T> { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : HidingBase; }",
        "Repo.Source"
    )]
    [InlineData(
        "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : RepositoryBase { public class T { } } }",
        "Repo.Source"
    )]
    public void Run_StaticAccessNestedInAGenericType_UsesTheTypesOwnName(string repository, string access)
    {
        foreach (var source in StaticSourceAndHolderOf("Source", repository))
        {
            var run = RunRepositories(source);

            run.ShouldBeClean();
            var methods = run
                .Sources.Single(file => file.Key.EndsWith("-Methods.g.cs", StringComparison.Ordinal))
                .Value;
            methods.ShouldContain(access);
            methods.ShouldNotContain("." + access);
        }
    }

    // Only a type nested in a generic type is reached by its simple name: nesting in other types keeps the full path.
    [Fact]
    public void Run_StaticAccessNestedInATypeThatIsNotGeneric_GoesThroughTheTypeInFull()
    {
        const string Repository =
            "public partial class Outer { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<T> : RepositoryBase; }";

        foreach (var source in StaticSourceAndHolderOf("Source", Repository))
        {
            var run = RunRepositories(source);

            run.ShouldBeClean();
            run.Sources.Values.ShouldContain(text =>
                text.Contains("global::App.Outer.Repo<T>.Source", StringComparison.Ordinal)
            );
        }
    }

    // Inside a repository nested in a generic type, a member that the simple name finds takes it from the repository,
    // and a static access through it would read that member: the repository is refused, once for each static member
    // it reaches through the name.
    [Theory]
    [InlineData("protected static string? Repo { get; set; }", "Repo", "the property App.Hiding.Repo")]
    [InlineData("protected static string? Repo = null;", "Repo", "the field App.Hiding.Repo")]
    [InlineData("protected static void Repo() { }", "Repo", "the method App.Hiding.Repo()")]
    [InlineData("protected static void Repo<X>() { }", "Repo", "the method App.Hiding.Repo<X>()")]
    [InlineData("protected class Repo { }", "Repo", "the class App.Hiding.Repo")]
    [InlineData("protected static event System.Action? Repo;", "Repo", "the event App.Hiding.Repo")]
    [InlineData("protected class Repo<X> { }", "Repo<U>", "the class App.Hiding.Repo<X>")]
    [InlineData("protected static void Repo<X>() { }", "Repo<U>", "the method App.Hiding.Repo<X>()")]
    [InlineData("", "parameters", "the method argument 'parameters'")]
    [InlineData("", "cancellationToken", "the method argument 'cancellationToken'")]
    [InlineData("", "Repo(string Repo)", "the primary-constructor parameter 'Repo'")]
    [InlineData("protected static string? Repo { get; set; }", "Repo(int Repo)", "the property App.Hiding.Repo")]
    public void Run_SimpleNameOfTheRepositoryTakenByAMember_ReportsSqlsrc039(
        string member,
        string repository,
        string takenBy
    )
    {
        var declarations =
            "public class Hiding : RepositoryBase { "
            + member
            + " } public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class "
            + repository
            + " : Hiding; }";
        var sources = StaticSourceAndHolderOf("Source", declarations);

        var bySource = RunRepositories(sources[0]).Problems();
        var holderRun = RunRepositories(sources[1]);
        var byHolder = holderRun.Problems();

        bySource.Count.ShouldBe(2);
        _ = byHolder.ShouldHaveSingleItem();
        foreach (var problem in bySource.Concat(byHolder))
        {
            problem.ShouldStartWith("SQLSRC039 ");
            problem.ShouldContain("inside the type that name means " + takenBy + ".");
        }

        // The holder serves both databases and is reported once: the run's report lists a problem once whatever the
        // planner found, so the planner's own list says it too.
        StepOutputs
            .Single<TypeMethods>(holderRun, TrackingNames.TypeMethods)
            .Diagnostics.Count(diagnostic => diagnostic.Descriptor.Id == "SQLSRC039")
            .ShouldBe(1);
    }

    // A nearest member can leave the name to another type: an inherited nested type of the repository's name that is
    // another constructed type, and a type beyond a field of the repository's own type, which ends Color Color.
    [Theory]
    [InlineData(
        "public partial class Outer<T> : RepositoryBase { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Outer<int>; }",
        "the class App.Outer<int>.Repo"
    )]
    [InlineData(
        "public class B0 : RepositoryBase { public class Repo { } } "
            + "public class B1<X> : B0 { protected static Outer<X>.Repo? Repo; } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : B1<T>; }",
        "the class App.B0.Repo"
    )]
    public void Run_SimpleNameTakenByAnotherType_ReportsSqlsrc039(string declarations, string takenBy)
    {
        foreach (var source in StaticSourceAndHolderOf("Source", declarations))
        {
            var problems = RunRepositories(source).Problems();

            problems.ShouldNotBeEmpty();
            foreach (var problem in problems)
            {
                problem.ShouldStartWith("SQLSRC039 ");
                problem.ShouldContain("inside the type that name means " + takenBy + ".");
            }
        }
    }

    // The simple name still reaches the repository: a field or property of the repository's own type, which is C#'s
    // Color Color rule, and a parameter of that type; an inherited nested type that is the repository; a member of
    // another arity; a member the repository cannot reach; and an accessor or operator, which no name finds.
    [Theory]
    [InlineData(
        "public class Hiding<X> : RepositoryBase { protected static Outer<X>.Repo? Repo { get; set; } } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Hiding<T>; }",
        "Repo.Source"
    )]
    [InlineData(
        "public class Hiding<X> : RepositoryBase { protected static readonly Outer<X>.Repo? Repo = null; } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Hiding<T>; }",
        "Repo.Source"
    )]
    [InlineData(
        "public class Hiding : RepositoryBase { protected class Repo<X> { } } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Hiding; }",
        "Repo.Source"
    )]
    [InlineData(
        "public class Hiding : RepositoryBase { protected static string? Repo { get; set; } } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<U> : Hiding; }",
        "Repo<U>.Source"
    )]
    [InlineData(
        "public class Hiding : RepositoryBase { } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class parameters<U> : Hiding; }",
        "parameters<U>.Source"
    )]
    [InlineData(
        "public class Hiding : RepositoryBase { protected static void Repo() { } } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<U> : Hiding; }",
        "Repo<U>.Source"
    )]
    [InlineData(
        "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo(Repo? Repo) : RepositoryBase; }",
        "Repo.Source"
    )]
    [InlineData(
        "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<U>(string Repo) : RepositoryBase; }",
        "Repo<U>.Source"
    )]
    [InlineData(
        "public partial class Outer<T> : RepositoryBase { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Outer<T>; }",
        "Repo.Source"
    )]
    [InlineData(
        "public partial class Outer<T> : RepositoryBase { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<U> : Outer<T>; }",
        "Repo<U>.Source"
    )]
    [InlineData(
        "public partial class Outer<T> { public partial class Mid : RepositoryBase { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Mid; } }",
        "Repo.Source"
    )]
    [InlineData(
        "public class Between<X> : Outer<X> { protected static Outer<X>.Repo? Repo; } "
            + "public partial class Outer<T> : RepositoryBase { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Between<T>; }",
        "Repo.Source"
    )]
    [InlineData(
        "public class Between<X> : Outer<X> { } public partial class Outer<T> : RepositoryBase { "
            + "[SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Between<T>; }",
        "Repo.Source"
    )]
    [InlineData(
        "public class Hiding : RepositoryBase { public int X => 0; } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class get_X : Hiding; }",
        "get_X.Source"
    )]
    [InlineData(
        "public class Hiding : RepositoryBase { public static Hiding operator +(Hiding a, Hiding b) => a; } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class op_Addition : Hiding; }",
        "op_Addition.Source"
    )]
    [InlineData(
        "public class Hiding : RepositoryBase { private static string? Repo { get; set; } } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo : Hiding; }",
        "Repo.Source"
    )]
    public void Run_SimpleNameThatStillReachesTheRepository_Compiles(string declarations, string access)
    {
        foreach (var source in StaticSourceAndHolderOf("Source", declarations))
        {
            var run = RunRepositories(source);

            run.ShouldBeClean();
            run.Sources.Values.ShouldContain(text => text.Contains(access, StringComparison.Ordinal));
        }
    }

    // A source that a method does not reach through the type's name is not refused, whatever that name means: an
    // instance member, the member of an instance holder, and a global.
    [Theory]
    [InlineData(
        "public partial class Repo : Hiding { "
            + PostgresMark
            + " public string? Pg { get; set; } "
            + SqlServerMark
            + " public string? Ms { get; set; } }"
    )]
    [InlineData(
        "public partial class Repo(F f) : Hiding; public class F { "
            + PostgresMark
            + " public string? Pg => \"\"; "
            + SqlServerMark
            + " public string? Ms => null; }"
    )]
    [InlineData("public partial class Repo : Hiding;")]
    public void Run_SourceNotReachedThroughTheTakenName_Compiles(string repository)
    {
        var global = repository.EndsWith("Repo : Hiding;", StringComparison.Ordinal)
            ? "public static class Db { "
                + PostgresMark
                + " public static string? Pg { get; set; } "
                + SqlServerMark
                + " public static string? Ms { get; set; } } "
            : string.Empty;
        var source =
            SourcesUsings
            + global
            + "public class Hiding { protected static string? Repo { get; set; } } "
            + "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + repository
            + " }\n}\n";

        RunRepositories(source).ShouldBeClean();
    }

    // Through the type's own name, a static member can have a name that an argument or a type parameter has.
    [Theory]
    [InlineData("cancellationToken")]
    [InlineData("parameters")]
    [InlineData("T")]
    public void Run_StaticMemberNamedAsAnArgumentNestedInAGenericType_Compiles(string member)
    {
        const string Repository =
            "public partial class Outer<T> { [SqlSourceGenerate(Source = \".\", GenerateInterface = false)] "
            + "public partial class Repo<T> : RepositoryBase; }";

        foreach (var source in StaticSourceAndHolderOf(member, Repository))
        {
            RunRepositories(source).ShouldBeClean();
        }
    }

    // Each place of a source's access that can be named await: a method writes it with its @.
    [Theory]
    [InlineData(
        Generate
            + " public partial class Repo { "
            + PostgresMark
            + " public static string? await { get; set; } "
            + SqlServerMark
            + " public static string? Ms { get; set; } }"
    )]
    [InlineData(
        Generate
            + " public partial class Repo { "
            + PostgresMark
            + " public string? await { get; set; } "
            + SqlServerMark
            + " public string? Ms { get; set; } }"
    )]
    [InlineData(
        Generate + " public partial class Repo(" + PostgresMark + " string await, " + SqlServerMark + " string ms);"
    )]
    [InlineData(
        "public class F { "
            + PostgresMark
            + " public string? Pg => \"\"; "
            + SqlServerMark
            + " public string? Ms => null; } "
            + Generate
            + " public partial class Repo { private static readonly F? await = new(); }"
    )]
    [InlineData(
        "public class F { "
            + PostgresMark
            + " public string? await => \"\"; "
            + SqlServerMark
            + " public string? Ms => null; } "
            + Generate
            + " public partial class Repo(F f);"
    )]
    [InlineData(
        "public static class Db { "
            + PostgresMark
            + " public static string? await { get; set; } "
            + SqlServerMark
            + " public static string? Ms { get; set; } } "
            + Generate
            + " public partial class Repo;"
    )]
    [InlineData(
        "public static class await { "
            + PostgresMark
            + " public static string? Pg { get; set; } "
            + SqlServerMark
            + " public static string? Ms { get; set; } } "
            + Generate
            + " public partial class Repo;"
    )]
    [InlineData(
        "} namespace await { public static class Db { "
            + PostgresMark
            + " public static string? Pg { get; set; } "
            + SqlServerMark
            + " public static string? Ms { get; set; } } } namespace App { "
            + Generate
            + " public partial class Repo;"
    )]
    [InlineData(
        "} namespace await { public interface IF { "
            + PostgresMark
            + " string? Pg { get; } "
            + SqlServerMark
            + " string? Ms { get; } } } namespace App { public class F : global::await.IF { "
            + "string? global::await.IF.Pg => \"\"; string? global::await.IF.Ms => null; } "
            + Generate
            + " public partial class Repo(F f);"
    )]
    public void Run_ConnectionSourceWithAwaitInItsAccess_Compiles(string declarations)
    {
        var run = RunRepositories(SourcesUsings + declarations + "\n}\n");

        run.ShouldBeClean();
    }

    // A source of the two kinds whose member has the name: a static source of each engine on a base type, and a static
    // holder of a factory that gives both.
    private static string[] StaticSourceAndHolderOf(string member, string repository)
    {
        var factory = SourcesUsings + "public class Factory\n{\n" + MembersOf("factory", string.Empty) + "}\n\n";
        var end = repository + "\n}\n";
        return
        [
            factory
                + "public abstract class RepositoryBase\n{\n"
                + "    [SqlSourceConnection(Database = \"postgres\")]\n"
                + $"    protected static string? {member} {{ get; set; }}\n\n"
                + "    [SqlSourceConnection(Database = \"mssql\")]\n"
                + "    protected static string? Other { get; set; }\n}\n\n"
                + end,
            factory
                + "public abstract class RepositoryBase\n{\n"
                + $"    protected static Factory? {member} {{ get; set; }}\n}}\n\n"
                + end,
        ];
    }

    // A marked member of each engine, of the kind of connection source, instance or static as the modifier says, at
    // the indent of a type's members in a namespace.
    private static string MembersOf(string kind, string modifier)
    {
        var (postgres, sqlServer) = kind switch
        {
            "connection string" => (
                "string Postgres { get; } = \"\";",
                "string? SqlServer(CancellationToken cancellationToken) => null;"
            ),
            "data source" => (
                "NpgsqlDataSource Postgres { get; } = NpgsqlDataSource.Create(\"\");",
                "DbDataSource? SqlServer { get; set; }"
            ),
            "factory" => (
                "NpgsqlConnection Postgres() => new();",
                "ValueTask<SqlConnection?> SqlServer(CancellationToken cancellationToken) => new(new SqlConnection());"
            ),
            "borrowed" => ("DbConnection? Postgres { get; set; }", "SqlConnection SqlServer = new();"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return "        [SqlSourceConnection(Database = \"postgres\")]\n        public "
            + modifier
            + postgres
            + "\n\n        [SqlSourceConnection(Database = \"mssql\")]\n        public "
            + modifier
            + sqlServer
            + "\n";
    }

    // The marked members of an interface, of each engine, of the kind of connection source, and the explicit
    // implementation of each, at the indent of a type's members in a namespace.
    private static (string Members, string Implementations) ExplicitMembersOf(string kind)
    {
        var (postgres, sqlServer, postgresBody, sqlServerBody) = kind switch
        {
            "connection string" => (
                "string Postgres { get; }",
                "string? SqlServer(CancellationToken cancellationToken)",
                " => \"\";",
                " => null;"
            ),
            "data source" => (
                "NpgsqlDataSource Postgres { get; }",
                "DbDataSource? SqlServer { get; }",
                " => NpgsqlDataSource.Create(\"\");",
                " => null;"
            ),
            "factory" => (
                "NpgsqlConnection Postgres()",
                "ValueTask<SqlConnection?> SqlServer(CancellationToken cancellationToken)",
                " => new();",
                " => new(new SqlConnection());"
            ),
            "borrowed" => (
                "DbConnection? Postgres { get; }",
                "SqlConnection SqlServer { get; }",
                " => null;",
                " => new();"
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var members =
            "        [SqlSourceConnection(Database = \"postgres\")]\n        "
            + postgres
            + (postgres.EndsWith('}') ? "\n\n" : ";\n\n")
            + "        [SqlSourceConnection(Database = \"mssql\")]\n        "
            + sqlServer
            + (sqlServer.EndsWith('}') ? "\n" : ";\n");
        var implementations =
            "        "
            + ExplicitOf(postgres)
            + postgresBody
            + "\n\n        "
            + ExplicitOf(sqlServer)
            + sqlServerBody
            + "\n";
        return (members, implementations);
    }

    // The member's declaration as an explicit implementation of IFactory's: its name after IFactory., and a property's
    // accessors left for the body's =>.
    private static string ExplicitOf(string declaration)
    {
        var withoutAccessors = declaration.Replace(" { get; }", string.Empty, StringComparison.Ordinal);
        var open = withoutAccessors.IndexOf('(', StringComparison.Ordinal);
        var name = withoutAccessors.LastIndexOf(' ', open >= 0 ? open : withoutAccessors.Length - 1) + 1;
        return withoutAccessors[..name] + "IFactory." + withoutAccessors[name..];
    }

    // The repositories of the source, at Repository, claiming a postgres file and an mssql file of a query that reads
    // one row, one of many and one that streams, with both drivers referenced.
    private static GeneratorRun RunRepositories(string source)
    {
        var postgres = new SqlFile(
            GeneratorHarness.UsersPath,
            """
            -- name: FindUser -> one-optional
            SELECT id, name FROM users WHERE id = @id;
            -- name: ListUsers
            SELECT id, name FROM users;
            -- name: StreamUsers
            -- collection-type: IAsyncEnumerable
            SELECT id, name FROM users;

            """
        );
        var sqlServer = new SqlFile(
            "/app/Repo/Documents.sql",
            """
            -- dialect: mssql
            -- name: FindDocument -> one-optional
            SELECT id, title FROM documents WHERE id = @id;
            -- name: ListDocuments
            SELECT id, title FROM documents;
            -- name: StreamDocuments
            -- collection-type: IAsyncEnumerable
            SELECT id, title FROM documents;

            """
        );
        SidecarColumn[] users = [Column("id"), Column("name", Text, nullable: true)];
        SidecarColumn[] documents =
        [
            TestSidecars.Column(0, "id", TestSidecars.SqlServer()),
            TestSidecars.Column(1, "title", TestSidecars.SqlServer("nvarchar", 200), nullable: true),
        ];
        SidecarParameter[] sqlServerId = [TestSidecars.Parameter(0, "id", TestSidecars.SqlServer(), false)];

        return GeneratorHarness.Run(
            OutputKind.Repository,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [
                postgres,
                Sidecars.Of(
                    postgres,
                    Entry(postgres, "FindUser", [Parameter("id")], users),
                    Entry(postgres, "ListUsers", [], users),
                    Entry(postgres, "StreamUsers", [], users)
                ),
                sqlServer,
                Sidecars.Of(
                    sqlServer,
                    MethodRuns.SqlServerEntry(sqlServer, "FindDocument", sqlServerId, documents),
                    MethodRuns.SqlServerEntry(sqlServer, "ListDocuments", [], documents),
                    MethodRuns.SqlServerEntry(sqlServer, "StreamDocuments", [], documents)
                ),
            ],
            references: [NpgsqlReferences.Npgsql10, SqlClientReferences.SqlClient7],
            dialect: "postgres"
        );
    }
}
