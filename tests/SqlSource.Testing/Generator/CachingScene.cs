using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator;

// The fixture of the caching tests of tests/SqlSource.Tests/Generator/: two types, UserQueries and OrderQueries, the
// files that they claim and the sidecars of those files, the first run of the generator over them, and the edits that
// a test makes to the compilation.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal sealed class CachingScene
{
    public const string UsersSourcePath = "/app/Users/UserQueries.cs";

    public const string UsersSource = """
        using SqlSource;

        namespace App.Users;

        [SqlSourceGenerate]
        public partial class UserQueries
        {
            public static string Get() => Sql.GetUser;
        }
        """;

    public static readonly SourceFile[] Sources =
    [
        new(UsersSourcePath, UsersSource),
        new(
            "/app/Orders/OrderQueries.cs",
            """
            using SqlSource;

            namespace App.Orders;

            [SqlSourceGenerate]
            public partial class OrderQueries;
            """
        ),
    ];

    public InMemoryAdditionalText Users { get; } = new("/app/Users/Users.sql", "-- name: GetUser\nSELECT 1;\n");

    public InMemoryAdditionalText Orders { get; } = new("/app/Orders/Orders.sql", "-- name: GetOrder\nSELECT 2;\n");

    // The fixture of the tests of sidecars: a current sidecar beside each file, which a run at Models under postgres
    // reads.
    public InMemoryAdditionalText UsersSidecar { get; }

    public InMemoryAdditionalText OrdersSidecar { get; }

    // A third file for OrderQueries, which the tests of the models use, with its sidecar of one column, of int4.
    public InMemoryAdditionalText Payments { get; } =
        new("/app/Orders/Payments.sql", "-- name: GetPayment\nSELECT 3;\n");

    public InMemoryAdditionalText PaymentsSidecar { get; }

    public CachingScene()
    {
        UsersSidecar = CurrentSidecar(Users, "GetUser");
        OrdersSidecar = CurrentSidecar(Orders, "GetOrder");
        PaymentsSidecar = TypedSidecar(Payments, "GetPayment", "int4");
    }

    public static CSharpCompilation ModelsCompilation() =>
        GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);

    // Both types at Models under postgres, with Users, Orders and Payments, each with a current sidecar whose entry has
    // one column, of int4.  A sidecar given is used in place of the one of its file.
    public GeneratorDriver FirstRunOfModels(
        Compilation compilation,
        InMemoryAdditionalText users,
        InMemoryAdditionalText orders,
        params InMemoryAdditionalText[] given
    )
    {
        var sidecars = new List<InMemoryAdditionalText>
        {
            TypedSidecar(users, "GetUser", "int4"),
            TypedSidecar(orders, "GetOrder", "int4"),
            PaymentsSidecar,
        };
        foreach (var sidecar in given)
        {
            _ = sidecars.RemoveAll(other => other.Path == sidecar.Path);
            sidecars.Add(sidecar);
        }

        var driver = GeneratorHarness
            .CreateDriver([users, orders, Payments, .. sidecars], OptionsWithTypeOverrides(null))
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    public GeneratorDriver FirstRun(OutputKind? output, Compilation compilation) =>
        FirstRun(compilation, Users, new TestOptionsProvider(output, null));

    public GeneratorDriver FirstRun(Compilation compilation, InMemoryAdditionalText users, TestOptionsProvider options)
    {
        var driver = GeneratorHarness
            .CreateDriver([users, Orders], options: options)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // Both types at Models under postgres, and each file with its current sidecar.
    public GeneratorDriver FirstRunWithSidecars(Compilation compilation)
    {
        var driver = GeneratorHarness
            .CreateDriver(
                [Users, Orders, UsersSidecar, OrdersSidecar],
                new TestOptionsProvider(OutputKind.Models, null, "postgres")
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // Both types at Models under postgres, and each file with a current sidecar whose entry has a column of int4,
    // Users with this one when it is given.
    public GeneratorDriver FirstRunWithTypes(
        Compilation compilation,
        TestOptionsProvider options,
        InMemoryAdditionalText? usersSidecar = null
    )
    {
        var driver = GeneratorHarness
            .CreateDriver(
                [
                    Users,
                    Orders,
                    usersSidecar ?? TypedSidecar(Users, "GetUser", "int4"),
                    TypedSidecar(Orders, "GetOrder", "int4"),
                ],
                options
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    public static TestOptionsProvider OptionsWithTypeOverrides(string? overrides) =>
        new(
            OutputKind.Models,
            null,
            "postgres",
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = overrides }
        );

    // A current sidecar whose entry has one column, of the type of pg_catalog, named value unless another name is
    // given, and these parameters when given.
    public static InMemoryAdditionalText TypedSidecar(
        InMemoryAdditionalText sqlFile,
        string queryName,
        string internalName,
        SidecarParameter[]? parameters = null,
        string columnName = "value"
    )
    {
        var file = ToSqlFile(sqlFile);
        var sidecar = Sidecars.Of(
            file,
            TestSidecars.Entry(
                queryName,
                Sidecars.HashOf(file, queryName, SqlDialect.PostgreSql),
                "postgres",
                "main",
                "18.0",
                parameters: parameters,
                columns:
                [
                    TestSidecars.Column(0, columnName, TestSidecars.Postgres(internalName, internalName: internalName)),
                ]
            )
        );
        return new InMemoryAdditionalText(sidecar.Path, sidecar.Text);
    }

    public static InMemoryAdditionalText CurrentSidecar(InMemoryAdditionalText sqlFile, string queryName)
    {
        var sidecar = Sidecars.Current(ToSqlFile(sqlFile), SqlDialect.PostgreSql, queryName);
        return new InMemoryAdditionalText(sidecar.Path, sidecar.Text);
    }

    public static SqlFile ToSqlFile(InMemoryAdditionalText file) => new(file.Path, file.GetText()!.ToString());

    // The compilation with the body of UserQueries.Get changed, and its attribute left alone.
    public static Compilation WithMethodBodyEdited(Compilation compilation) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
            CSharpSyntaxTree.ParseText(
                UsersSource.Replace("=> Sql.GetUser;", "=> Sql.GetUser + \" \";", StringComparison.Ordinal),
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    // The compilation with a list of generator parameters on the attribute of UserQueries.
    public static Compilation WithParametersOnUserQueries(Compilation compilation, string parameters) =>
        WithArgumentsOnUserQueries(compilation, "Parameters = \"" + parameters + "\"");

    // The compilation with these arguments on the attribute of UserQueries.
    public static Compilation WithArgumentsOnUserQueries(Compilation compilation, string arguments) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
            CSharpSyntaxTree.ParseText(
                UsersSource.Replace(
                    "[SqlSourceGenerate]",
                    "[SqlSourceGenerate(" + arguments + ")]",
                    StringComparison.Ordinal
                ),
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
}
