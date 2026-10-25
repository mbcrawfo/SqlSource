using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
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

    public const string OrdersSourcePath = "/app/Orders/OrderQueries.cs";

    public static readonly string UsersSource = TypeSource(
        "App.Users",
        "UserQueries",
        members: "    public static string Get() => Sql.GetUser;\n"
    );

    public static readonly SourceFile[] Sources =
    [
        new(UsersSourcePath, UsersSource),
        new(OrdersSourcePath, TypeSource("App.Orders", "OrderQueries")),
    ];

    public InMemoryAdditionalText Users { get; } = new("/app/Users/Users.sql", SqlTexts.GetUser);

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

    // The source of an attributed type in the namespace, with these arguments on its attribute when they are given
    // and these members, each a line of its own.  A type with no members has no body.
    public static string TypeSource(
        string namespaceName,
        string typeName,
        string? arguments = null,
        string members = ""
    ) =>
        "using SqlSource;\n\nnamespace "
        + namespaceName
        + ";\n\n[SqlSourceGenerate"
        + (arguments is null ? string.Empty : "(" + arguments + ")")
        + "]\npublic partial class "
        + typeName
        + (members.Length == 0 ? ";\n" : "\n{\n" + members + "}\n");

    // The two types of the tests of the methods with these sources, which put both in the namespace App, so that a
    // method of each can clash, and read no constant, since a query at CodeGen has none.  OrderQueries is the plain
    // type of App unless its source is given.
    public static CSharpCompilation MethodsCompilation(string usersSource, string? ordersSource = null) =>
        GeneratorHarness.CreateCompilation(
            [
                new(UsersSourcePath, usersSource),
                new(OrdersSourcePath, ordersSource ?? TypeSource("App", "OrderQueries")),
            ],
            references: [NpgsqlReferences.Npgsql10]
        );

    // Both types at CodeGen under postgres, with Users and Orders, each with a current sidecar whose entry has one
    // column, of int4, so that each type has one method, in its extension class.  A sidecar given is used in place of
    // the one of its file.
    public GeneratorDriver FirstRunOfMethods(Compilation compilation, params InMemoryAdditionalText[] given)
    {
        var driver = FirstRunOfMethodsWith(compilation, Users, given);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // The same with this file in place of Users, its sidecar written for it, and the run's diagnostics left to the
    // test.
    public GeneratorDriver FirstRunOfMethodsWith(
        Compilation compilation,
        InMemoryAdditionalText users,
        params InMemoryAdditionalText[] given
    )
    {
        var sidecars = new List<InMemoryAdditionalText>
        {
            TypedSidecar(users, "GetUser", "int4"),
            TypedSidecar(Orders, "GetOrder", "int4"),
        };
        foreach (var sidecar in given)
        {
            _ = sidecars.RemoveAll(other => other.Path == sidecar.Path);
            sidecars.Add(sidecar);
        }

        return GeneratorHarness
            .CreateDriver([users, Orders, .. sidecars], new TestOptionsProvider(OutputKind.CodeGen, null, "postgres"))
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
    }

    // The compilation with this source of UserQueries in place of the one it has.
    public static Compilation WithUserQueries(Compilation compilation, string source) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
            CSharpSyntaxTree.ParseText(
                source,
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

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

    // Both types at Models under mssql, and each file with its current sidecar.
    public GeneratorDriver FirstRunWithSqlServerSidecars(Compilation compilation)
    {
        var driver = GeneratorHarness
            .CreateDriver(
                [
                    Users,
                    Orders,
                    CurrentSidecar(Users, "GetUser", SqlDialect.SqlServer),
                    CurrentSidecar(Orders, "GetOrder", SqlDialect.SqlServer),
                ],
                new TestOptionsProvider(OutputKind.Models, null, "mssql")
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

    public static InMemoryAdditionalText CurrentSidecar(
        InMemoryAdditionalText sqlFile,
        string queryName,
        SqlDialect dialect = SqlDialect.PostgreSql
    )
    {
        var sidecar = Sidecars.Current(ToSqlFile(sqlFile), dialect, queryName);
        return new InMemoryAdditionalText(sidecar.Path, sidecar.Text);
    }

    // Only this file of Users and Orders was read again: it is Modified at the input of the read, and so at ParsedFile,
    // by its name, while the other one is the input it was (the reason given: Cached, or Unchanged where the step that
    // resolves its input ran) and Cached at ParsedFile.
    public void ShouldHaveReadOnly(
        GeneratorDriverRunResult result,
        InMemoryAdditionalText file,
        IncrementalStepRunReason otherInput
    )
    {
        var other = file.Path == Users.Path ? Orders : Users;

        StepReasons
            .ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [file.Path] = IncrementalStepRunReason.Modified,
                    [other.Path] = otherInput,
                },
                ignoreOrder: true
            );
        StepReasons
            .Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, parsed => parsed.FileName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [Path.GetFileName(file.Path)] = IncrementalStepRunReason.Modified,
                    [Path.GetFileName(other.Path)] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
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
