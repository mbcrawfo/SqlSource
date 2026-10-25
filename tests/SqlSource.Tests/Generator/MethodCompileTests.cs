// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

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

        """
    );

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
        methods.ShouldContain(" ListFrom(\n");
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
        methods.ShouldContain(" ListFrom(\n");
    }
}
