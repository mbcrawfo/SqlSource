// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The driver a query's method needs: without it, SQLSRC273 once for the project and engine; below the floor,
// SQLSRC247 or SQLSRC267.  Either way the models are emitted and no method is.  Each run is at CodeGen.
public sealed class MethodDriverTests
{
    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users;\n-- name: ListUsers\nSELECT id FROM users;\n"
    );

    private static readonly SidecarEntry[] PostgresEntries =
    [
        Entry(Users, "GetUser", [], [Column("id")]),
        Entry(Users, "ListUsers", [], [Column("id")]),
    ];

    private static readonly SidecarEntry[] SqlServerEntries =
    [
        MethodRuns.SqlServerEntry(Users, "GetUser", [], [TestSidecars.Column(0, "id", TestSidecars.SqlServer())]),
        MethodRuns.SqlServerEntry(Users, "ListUsers", [], [TestSidecars.Column(0, "id", TestSidecars.SqlServer())]),
    ];

    [Fact]
    public void Run_PostgresCodeGenWithoutNpgsql_IsSqlsrc273OnceAndModelsAreEmitted()
    {
        var run = MethodRuns.Run(Users, PostgresEntries, references: []);

        run.Diagnostics.ShouldBe([
            "SQLSRC273 (1,1)-(1,1): A query for PostgreSQL has the output codegen, and this project does not reference "
                + "Npgsql, which its method needs.  Reference Npgsql, or set the output to models.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.GetUserDto-Model.g.cs");
        run.Sources.Keys.ShouldContain("App.ListUsersDto-Model.g.cs");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_SqlServerCodeGenWithoutSqlClient_IsSqlsrc273OnceAndModelsAreEmitted()
    {
        var run = MethodRuns.RunSqlServer(Users, SqlServerEntries, references: []);

        run.Diagnostics.ShouldBe([
            "SQLSRC273 (1,1)-(1,1): A query for SQL Server has the output codegen, and this project does not reference "
                + "Microsoft.Data.SqlClient, which its method needs.  Reference Microsoft.Data.SqlClient, or set the "
                + "output to models.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.GetUserDto-Model.g.cs");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_NpgsqlBelowTheFloor_GivesNoMethod()
    {
        var driver = NpgsqlReferences.Synthetic("7.0.4", slimBuilder: false, cube: false);

        var run = MethodRuns.Run(Users, PostgresEntries, references: [driver]);

        run.Diagnostics.ShouldBe([
            "SQLSRC247 (1,1)-(1,1): This project references Npgsql 7.0.4, and SqlSource needs Npgsql 8.0 or later.  No "
                + "query method is generated until the package is updated.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.GetUserDto-Model.g.cs");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_SqlClientBelowTheFloor_GivesNoMethod()
    {
        var run = MethodRuns.RunSqlServer(Users, SqlServerEntries, references: [SqlClientReferences.SqlClient60]);

        run.Diagnostics.ShouldBe([
            "SQLSRC267 (1,1)-(1,1): This project references Microsoft.Data.SqlClient 6.0.5, and SqlSource needs "
                + "Microsoft.Data.SqlClient 6.1 or later.  No query method is generated until the package is updated.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.GetUserDto-Model.g.cs");
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }
}
