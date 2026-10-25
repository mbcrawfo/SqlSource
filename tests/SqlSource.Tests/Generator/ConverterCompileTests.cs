// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The methods that read and bind through converters, compiled against each driver the generator supports, as
// MethodCompileTests compiles those without: a column and a parameter converted from and to a value type and a
// reference type, nullable and not, in a query of each shape.  The compilation reports every warning wave, so the
// converted code compiles with no error and no warning in what is generated.  Each run is at CodeGen.
public sealed class ConverterCompileTests
{
    // UserId, a value type over a long; Email, a value type over a string; and Name, a reference type over a string.
    // EXTRA holds the types of one engine.
    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public readonly record struct UserId(long Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(long value) => new(value);

                [SqlSourceConverter]
                public static long ToDatabase(UserId id) => id.Value;
            }

            public readonly record struct Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Email email) => email.Value;
            }

            public sealed record Name(string Value)
            {
                [SqlSourceConverter]
                public static Name FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Name name) => name.Value;
            }

            EXTRA
        }
        """;

    // A reference type over SqlClient's vector, a value type that reads a NULL as its Null.
    private const string Embedding = """
        public sealed record Embedding(float[] Values)
            {
                [SqlSourceConverter]
                public static Embedding FromDatabase(Microsoft.Data.SqlTypes.SqlVector<float> value) =>
                    new(value.Memory.ToArray());

                [SqlSourceConverter]
                public static Microsoft.Data.SqlTypes.SqlVector<float> ToDatabase(Embedding embedding) =>
                    new(embedding.Values);
            }
        """;

    private static readonly SqlFile Postgres = new(
        GeneratorHarness.UsersPath,
        """
        -- type-override: int8=App.UserId
        -- type-override: public.email=App.Email
        -- type-override: varchar=App.Name
        -- name: ListUsers
        SELECT id, manager, email, contact, name, nickname FROM users
        WHERE id = @id OR manager = @manager OR email = @email OR name = @name;
        -- name: GetUser -> one
        SELECT id, manager, email, contact, name, nickname FROM users
        WHERE id = @id OR manager = @manager OR email = @email OR name = @name;
        -- name: FindUser -> one-optional
        SELECT id, manager, email, contact, name, nickname FROM users
        WHERE id = @id OR manager = @manager OR email = @email OR name = @name;
        -- name: RenameUser
        UPDATE users SET name = @name, email = @email, manager = @manager WHERE id = @id;

        """
    );

    private static readonly SqlFile SqlServer = new(
        GeneratorHarness.UsersPath,
        """
        -- type-override: bigint=App.UserId
        -- type-override: varchar=App.Email
        -- type-override: nvarchar=App.Name
        -- type-override: vector=App.Embedding
        -- name: ListDocuments
        SELECT id, manager, email, contact, title, note, embedding, draft FROM documents
        WHERE id = @id OR manager = @manager OR email = @email OR title = @title;
        -- name: GetDocument -> one
        SELECT id, manager, email, contact, title, note, embedding, draft FROM documents
        WHERE id = @id OR manager = @manager OR email = @email OR title = @title;
        -- name: FindDocument -> one-optional
        SELECT id, manager, email, contact, title, note, embedding, draft FROM documents
        WHERE id = @id OR manager = @manager OR email = @email OR title = @title;
        -- name: RenameDocument
        UPDATE documents SET title = @title, email = @email, manager = @manager WHERE id = @id;

        """
    );

    private static readonly PostgresType Int8 = Catalog("int8");

    private static readonly PostgresType Varchar = Catalog("varchar");

    // The domain email over text.
    private static readonly PostgresType EmailDomain = TestSidecars.Postgres(
        name: "email",
        kind: SidecarValues.Kind.Domain,
        schema: "public",
        internalName: "email",
        baseType: Text
    );

    [Theory]
    [InlineData("Npgsql 8")]
    [InlineData("Npgsql 9")]
    [InlineData("Npgsql 10")]
    [InlineData("SqlClient 6.1")]
    [InlineData("SqlClient 7")]
    public void Run_ConvertedQueries_CompileWithNoWarning(string driver)
    {
        switch (driver)
        {
            case "Npgsql 8":
                RunOnNpgsql(NpgsqlReferences.Npgsql8);
                break;
            case "Npgsql 9":
                RunOnNpgsql(NpgsqlReferences.Npgsql9);
                break;
            case "Npgsql 10":
                RunOnNpgsql(NpgsqlReferences.Npgsql10);
                break;
            case "SqlClient 6.1":
                RunOnSqlClient(SqlClientReferences.SqlClient61);
                break;
            default:
                RunOnSqlClient(SqlClientReferences.SqlClient7);
                break;
        }
    }

    private static void RunOnNpgsql(MetadataReference reference)
    {
        SidecarParameter[] parameters =
        [
            Parameter("id", Int8, nullable: false),
            Parameter("manager", Int8, nullable: true),
            Parameter("email", EmailDomain, nullable: true),
            Parameter("name", Varchar, nullable: false),
        ];
        SidecarColumn[] columns =
        [
            Column("id", Int8),
            Column("manager", Int8, nullable: true),
            Column("email", EmailDomain),
            Column("contact", EmailDomain, nullable: true),
            Column("name", Varchar),
            Column("nickname", Varchar, nullable: true),
        ];

        var run = MethodRuns.Run(
            Postgres,
            [
                Entry(Postgres, "ListUsers", parameters, columns),
                Entry(Postgres, "GetUser", parameters, columns),
                Entry(Postgres, "FindUser", parameters, columns),
                Entry(Postgres, "RenameUser", [parameters[3], parameters[2], parameters[1], parameters[0]], null),
            ],
            SourceWith(string.Empty),
            references: [reference]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" ListUsers(\n");
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldContain(" FindUser(\n");
        methods.ShouldContain(" RenameUser(\n");
        methods.ShouldContain("global::App.UserId.FromDatabase(reader.GetFieldValue<long>(0))");
        methods.ShouldContain("global::App.Name.FromDatabase(reader.GetFieldValue<string>(5))");
        methods.ShouldContain("parameter.Value = global::App.UserId.ToDatabase(parameters.Id);\n");
        methods.ShouldContain("parameters.Name is { } value ? (object)global::App.Name.ToDatabase(value)");
    }

    private static void RunOnSqlClient(MetadataReference reference)
    {
        var bigint = TestSidecars.SqlServer("bigint", 8, 19);
        var varchar = TestSidecars.SqlServer("varchar", 200);
        var nvarchar = TestSidecars.SqlServer("nvarchar", 200);
        var vector = TestSidecars.SqlServer("vector(3)", 20, 0);
        SidecarParameter[] parameters =
        [
            TestSidecars.Parameter(0, "id", bigint, false),
            TestSidecars.Parameter(1, "manager", bigint, true),
            TestSidecars.Parameter(2, "email", varchar, true),
            TestSidecars.Parameter(3, "title", nvarchar, false),
        ];
        SidecarColumn[] columns =
        [
            TestSidecars.Column(0, "id", bigint),
            TestSidecars.Column(1, "manager", bigint, nullable: true),
            TestSidecars.Column(2, "email", varchar),
            TestSidecars.Column(3, "contact", varchar, nullable: true),
            TestSidecars.Column(4, "title", nvarchar),
            TestSidecars.Column(5, "note", nvarchar, nullable: true),
            TestSidecars.Column(6, "embedding", vector),
            TestSidecars.Column(7, "draft", vector, nullable: true),
        ];

        var run = MethodRuns.RunSqlServer(
            SqlServer,
            [
                MethodRuns.SqlServerEntry(SqlServer, "ListDocuments", parameters, columns),
                MethodRuns.SqlServerEntry(SqlServer, "GetDocument", parameters, columns),
                MethodRuns.SqlServerEntry(SqlServer, "FindDocument", parameters, columns),
                MethodRuns.SqlServerEntry(
                    SqlServer,
                    "RenameDocument",
                    [parameters[3], parameters[2], parameters[1], parameters[0]],
                    null
                ),
            ],
            SourceWith(Embedding),
            references: [reference]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" ListDocuments(\n");
        methods.ShouldContain(" GetDocument(\n");
        methods.ShouldContain(" FindDocument(\n");
        methods.ShouldContain(" RenameDocument(\n");
        methods.ShouldContain("if (value6.IsNull)\n");
        methods.ShouldContain("global::App.Embedding.FromDatabase(value6)");
        methods.ShouldNotContain("value7");
        methods.ShouldContain("parameter.Value = global::App.UserId.ToDatabase(parameters.Id);\n");
        methods.ShouldContain("parameters.Title is { } value ? (object)global::App.Name.ToDatabase(value)");
    }

    private static string SourceWith(string extra) => Source.Replace("EXTRA", extra, StringComparison.Ordinal);
}
