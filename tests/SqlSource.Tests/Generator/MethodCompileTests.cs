// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The methods compiled against each driver the generator supports, read as metadata: a query of each shape, a token,
// and parameters and columns that can and cannot be null, of value and reference types.  The compilation reports every
// warning wave, so a method compiles with no error and no warning in what is generated.
public sealed class MethodCompileTests
{
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

    // A parameter of each kind PostgreSQL binds, each a parameter of Npgsql by its bindName, and an integer that can be
    // null, a parameter of int?, compile alike on every Npgsql, the setter of DataTypeName and TypedValue included.
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
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        var methods = run.Sources[MethodRuns.MethodsFile];
        foreach (var (kind, type) in kinds)
        {
            methods.ShouldContain(" parameter.DataTypeName = \"" + type.BindName + "\";\n", customMessage: kind);
        }

        methods.ShouldContain(" var parameter = new global::Npgsql.NpgsqlParameter<int?>();\n");
        methods.ShouldContain(" parameter.TypedValue = parameters.Maybe;\n");
    }

    // SqlClient reads a NULL of vector as SqlVector<float>.Null, so the method checks a not-null one with IsNull.  The
    // map's text must be the very spelling the method writer looks for.
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
}
