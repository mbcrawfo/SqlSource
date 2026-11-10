// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The interface of a repository: the public repository methods of the type, which its generated part implements, on
// by default, turned off by the project or the type, renamed by the type, joined by the user's own part, and reported
// when its name is taken.  Each run is under postgres on Npgsql 10, at Repository.
public sealed class RepositoryInterfaceTests
{
    private const string MethodsFile = "App.UserRepository-Methods.g.cs";

    private const string Source = """
        using Npgsql;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class UserRepository(NpgsqlDataSource dataSource);
        }
        """;

    private static readonly SqlFile UsersFile = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
    );

    [Fact]
    public void Run_RepositoryType_ImplementsItsGeneratedInterface()
    {
        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain(
            "namespace App\n{\n    partial class UserRepository : global::App.IUserRepository\n    {\n"
        );
        methods.ShouldEndWith(
            GeneratedFile.Joined(
                """
                    }

                    /// <summary>
                    /// The public repository methods of <c>App.UserRepository</c>, which it implements.
                    /// </summary>
                    public partial interface IUserRepository
                    {
                        /// <summary>
                        /// The <c>GetUser</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT id FROM users WHERE id = @id;
                        /// </code>
                        /// </remarks>
                        /// <param name="parameters">The query's parameters.</param>
                        /// <param name="cancellationToken">Cancels the query.</param>
                        /// <returns>The row, or null when there is none.</returns>
                        /// <exception cref="T:SqlSource.UnexpectedRowCountException">↩
                The query returned more than one row.</exception>
                        global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(
                            global::App.GetUserParams parameters,
                            global::System.Threading.CancellationToken cancellationToken = default);
                    }
                }

                """
            )
        );
    }

    // Only a public method is a member of the interface: a protected one is the type's own, and an internal one would
    // be public through the interface.
    [Fact]
    public void Run_ProtectedAndInternalMethods_AreNotInTheInterface()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
                + "-- name: FindUser -> one-optional\n-- visibility: protected\nSELECT id FROM users WHERE id = @id;\n"
                + "-- name: LoadUser -> one-optional\n-- visibility: internal\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(
            file,
            [GetUserEntry(file), GetUserEntry(file, "FindUser"), GetUserEntry(file, "LoadUser")],
            Source
        );

        run.ShouldBeClean();
        var contract = InterfaceOf(run.Sources[MethodsFile]);
        contract.ShouldContain(" GetUser(\n");
        contract.ShouldNotContain(" FindUser(\n");
        contract.ShouldNotContain(" LoadUser(\n");
    }

    // The interface is written when no method can be, so code that names it reports only the type's problem.
    [Fact]
    public void Run_EveryMethodFails_StillWritesTheEmptyInterface()
    {
        const string NoConnection = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class UserRepository;

                public sealed class Consumer
                {
                    public Consumer(IUserRepository repository) => Repository = repository;

                    public IUserRepository Repository { get; }
                }
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], NoConnection);

        run.Problems().Select(problem => problem.Split(' ')[0]).ShouldBe(["SQLSRC028"]);
        run.Sources[MethodsFile]
            .ShouldEndWith(
                "namespace App\n{\n    partial class UserRepository : global::App.IUserRepository\n    {\n    }\n\n"
                    + "    /// <summary>\n"
                    + "    /// The public repository methods of <c>App.UserRepository</c>, which it implements.\n"
                    + "    /// </summary>\n"
                    + "    public partial interface IUserRepository\n    {\n    }\n}\n"
            );
    }

    [Fact]
    public void Run_GenerateInterfaceFalse_WritesNone()
    {
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", GenerateInterface = false",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository\n    {\n");
        methods.ShouldNotContain("interface");
    }

    // The project's switch turns the interface off, ignoring case.
    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    public void Run_PropertyFalse_WritesNone(string value)
    {
        var run = RepositoryRuns.Run(
            UsersFile,
            [GetUserEntry()],
            Source,
            properties: new Dictionary<string, string?> { ["SqlSourceRepositoryInterfaces"] = value }
        );

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldNotContain("interface");
    }

    // A GenerateInterface that is written overrides the project's switch.
    [Fact]
    public void Run_PropertyFalseAndAttributeTrue_WritesOne()
    {
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", GenerateInterface = true",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(
            UsersFile,
            [GetUserEntry()],
            source,
            properties: new Dictionary<string, string?> { ["SqlSourceRepositoryInterfaces"] = "false" }
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository : global::App.IUserRepository\n");
        methods.ShouldContain("    public partial interface IUserRepository\n");
    }

    [Fact]
    public void Run_InterfaceName_RenamesIt()
    {
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", InterfaceName = \"IUserStore\"",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository : global::App.IUserStore\n");
        methods.ShouldContain("    public partial interface IUserStore\n");
        methods.ShouldNotContain("IUserRepository");
    }

    // A name that is a keyword is written with its @.
    [Fact]
    public void Run_InterfaceNameIsAKeyword_EscapesIt()
    {
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", InterfaceName = \"class\"",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository : global::App.@class\n");
        methods.ShouldContain("    public partial interface @class\n");
    }

    // A class of the name keeps it, and the type keeps its methods without the interface.
    [Fact]
    public void Run_InterfaceNameTakenByAClass_ReportsSqlsrc035AndWritesNone()
    {
        var source = Source.Replace(
            "NpgsqlDataSource dataSource);\n",
            "NpgsqlDataSource dataSource);\n\n    public class IUserRepository { }\n",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.Problems()
            .ShouldBe([
                "SQLSRC035 /app/Repo/Sample.cs(6,6)-(6,37): 'App.UserRepository' cannot get its interface "
                    + "'IUserRepository', because the class App.IUserRepository already has that name.  Set "
                    + "InterfaceName on its [SqlSourceGenerate] to another name, or set GenerateInterface to false.",
            ]);
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository\n    {\n");
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldNotContain("interface");
    }

    // A name that is no identifier would break the generated file, so the type gets no interface, and keeps its
    // methods.
    [Theory]
    [InlineData("I-Users")]
    [InlineData("@IUsers")]
    [InlineData("App.IUsers")]
    [InlineData("IUsers<T>")]
    public void Run_InterfaceNameNotAnIdentifier_ReportsSqlsrc037AndWritesNone(string name)
    {
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", InterfaceName = \"" + name + "\"",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        // The attribute ends after ", InterfaceName = \"\"" and the name.
        var end = 37 + 20 + name.Length;
        run.Problems()
            .ShouldBe([
                "SQLSRC037 /app/Repo/Sample.cs(6,6)-(6,"
                    + end.ToString(CultureInfo.InvariantCulture)
                    + "): 'App.UserRepository' cannot get its interface '"
                    + name
                    + "', because the name is not a C# identifier.  Set InterfaceName on its [SqlSourceGenerate] to an "
                    + "identifier, without an @ or a namespace, or remove it.",
            ]);
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository\n    {\n");
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldNotContain("interface");
    }

    // A name that the type does not use is not checked.
    [Fact]
    public void Run_InterfaceNameNotAnIdentifierWithTheInterfaceOff_ReportsNothing()
    {
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", GenerateInterface = false, InterfaceName = \"I-Users\"",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldNotContain("interface");
    }

    // A public method of the interface's name could not be its member (CS0542), so it is left out, and the interface
    // is written without it.
    [Fact]
    public void Run_PublicMethodNamedLikeTheInterface_ReportsSqlsrc274()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: IUserRepository -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [GetUserEntry(file, "IUserRepository")], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,25): The method of the query 'IUserRepository' cannot be "
                    + "named 'IUserRepository', because it is the name of the interface 'App.IUserRepository' that "
                    + "holds the method.  Rename the query.",
            ]);
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    public partial interface IUserRepository\n    {\n    }\n");
        methods.ShouldNotContain(" async ");
    }

    // A protected method is no member of the interface, so it can have the interface's name.
    [Fact]
    public void Run_ProtectedMethodNamedLikeTheInterface_IsKept()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: IUserRepository -> one-optional\n-- visibility: protected\n"
                + "SELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [GetUserEntry(file, "IUserRepository")], Source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain(" protected async ");
    }

    // A type parameter of the type, or of a type it is nested in, takes the name of a member beside it (CS0694,
    // CS0102), and the type it is nested in that of its member (CS0542), so the type gets no interface.
    [Theory]
    [InlineData(
        "[SqlSourceGenerate(Source = \".\")]\n    public partial class Repo<IRepo>(NpgsqlDataSource dataSource);",
        "App.Repo<IRepo>",
        "the type parameter IRepo of App.Repo<IRepo>",
        "App.Repo-1-Methods.g.cs"
    )]
    [InlineData(
        "public partial class Outer<IRepo>\n    {\n        [SqlSourceGenerate(Source = \".\")]\n"
            + "        public partial class Repo(NpgsqlDataSource dataSource);\n    }",
        "App.Outer<IRepo>.Repo",
        "the type parameter IRepo of App.Outer<IRepo>",
        "App.Outer-1.Repo-Methods.g.cs"
    )]
    [InlineData(
        "public partial class Outer\n    {\n        [SqlSourceGenerate(Source = \".\", InterfaceName = \"Outer\")]\n"
            + "        public partial class Repo(NpgsqlDataSource dataSource);\n    }",
        "App.Outer.Repo",
        "the containing type App.Outer",
        "App.Outer.Repo-Methods.g.cs"
    )]
    public void Run_InterfaceNamedLikeTheTypes_ReportsSqlsrc035AndWritesNone(
        string declaration,
        string typeName,
        string takenBy,
        string methodsFile
    )
    {
        var source = "using Npgsql;\nusing SqlSource;\n\nnamespace App\n{\n    " + declaration + "\n}\n";
        var name = takenBy.Contains("containing", StringComparison.Ordinal) ? "Outer" : "IRepo";

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.Problems()
            .Select(static problem => problem[problem.IndexOf(':', StringComparison.Ordinal)..])
            .ShouldBe([
                ": '"
                    + typeName
                    + "' cannot get its interface '"
                    + name
                    + "', because "
                    + takenBy
                    + " already has that name.  Set InterfaceName on its [SqlSourceGenerate] to another name, or set "
                    + "GenerateInterface to false.",
            ]);
        run.Problems().ShouldAllBe(problem => problem.StartsWith("SQLSRC035 ", StringComparison.Ordinal));
        var methods = run.Sources[methodsFile];
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldNotContain("interface");
    }

    // A model, an extension class or another type's interface that SqlSource writes beside the interface takes its
    // name (CS0101), so the type gets no interface.
    [Theory]
    [InlineData("GetUserDto", "the model App.GetUserDto that SqlSource writes")]
    [InlineData("UserRepositoryExtensions", "the extension class App.UserRepositoryExtensions that SqlSource writes")]
    public void Run_InterfaceNamedLikeWhatSqlSourceWrites_ReportsSqlsrc035AndWritesNone(string name, string takenBy)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
                + "-- name: FindUser -> one-optional\n-- output: methods\nSELECT id FROM users WHERE id = @id;\n"
        );
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", InterfaceName = \"" + name + "\"",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(file, [GetUserEntry(file), GetUserEntry(file, "FindUser")], source);

        var end = 37 + 20 + name.Length;
        run.Problems()
            .ShouldBe([
                "SQLSRC035 /app/Repo/Sample.cs(6,6)-(6,"
                    + end.ToString(CultureInfo.InvariantCulture)
                    + "): 'App.UserRepository' cannot get its interface '"
                    + name
                    + "', because "
                    + takenBy
                    + " already has that name.  Set InterfaceName on its [SqlSourceGenerate] to another name, or set "
                    + "GenerateInterface to false.",
            ]);
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository\n    {\n");
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldContain("static partial class UserRepositoryExtensions\n");
        methods.ShouldNotContain("interface");
    }

    // Two types whose interfaces have one name would each implement the other's members, so neither gets it.
    [Fact]
    public void Run_TwoTypesOfOneInterfaceName_ReportsSqlsrc035AtEach()
    {
        const string TwoTypes = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".", InterfaceName = "IStore")]
                public partial class UserRepository(NpgsqlDataSource dataSource);

                [SqlSourceGenerate(Source = ".", InterfaceName = "IStore")]
                public partial class OrderRepository(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], TwoTypes);

        run.Problems()
            .ShouldBe([
                "SQLSRC035 /app/Repo/Sample.cs(6,6)-(6,63): 'App.UserRepository' cannot get its interface 'IStore', "
                    + "because the interface App.IStore that SqlSource writes for App.OrderRepository already has "
                    + "that name.  Set InterfaceName on its [SqlSourceGenerate] to another name, or set "
                    + "GenerateInterface to false.",
                "SQLSRC035 /app/Repo/Sample.cs(9,6)-(9,63): 'App.OrderRepository' cannot get its interface 'IStore', "
                    + "because the interface App.IStore that SqlSource writes for App.UserRepository already has "
                    + "that name.  Set InterfaceName on its [SqlSourceGenerate] to another name, or set "
                    + "GenerateInterface to false.",
            ]);
        run.Sources[MethodsFile].ShouldNotContain("interface");
        run.Sources["App.OrderRepository-Methods.g.cs"].ShouldNotContain("interface");
    }

    // A contextual keyword that C# refuses as the name of a type, or reads otherwise, is written after an @.
    [Theory]
    [InlineData("file")]
    [InlineData("scoped")]
    [InlineData("required")]
    [InlineData("record")]
    [InlineData("extension")]
    public void Run_InterfaceNameIsAContextualKeyword_EscapesIt(string name)
    {
        var source = Source.Replace(
            "Source = \".\"",
            "Source = \".\", InterfaceName = \"" + name + "\"",
            StringComparison.Ordinal
        );

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class UserRepository : global::App.@" + name + "\n");
        methods.ShouldContain("    public partial interface @" + name + "\n");
    }

    // The user's own part of the interface adds a member, which the type implements beside the generated ones.
    [Fact]
    public void Run_UserPartialInterface_AddsItsOwnMembers()
    {
        const string WithExtra = """
            using System.Threading.Tasks;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class UserRepository(NpgsqlDataSource dataSource)
                {
                    public void Extra() { }
                }

                public partial interface IUserRepository
                {
                    void Extra();
                }

                public static class Consumer
                {
                    public static Task<GetUserDto?> Use(IUserRepository repository)
                    {
                        repository.Extra();
                        return repository.GetUser(new GetUserParams(1));
                    }
                }
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], WithExtra);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain("    public partial interface IUserRepository\n");
    }

    // A type whose queries have other outputs claims no repository query, so it gets no interface.
    [Fact]
    public void Run_TypeWithOnlySqlQueries_WritesNoInterface()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- output: sql\n\n-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [], Source);

        run.ShouldBeClean();
        run.Sources.Values.ShouldAllBe(text => !text.Contains("partial interface"));
    }

    [Fact]
    public void Run_TypeWithOnlyMethodsQueries_WritesNoInterface()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- output: methods\n\n-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [GetUserEntry(file)], Source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain(" static ");
        run.Sources.Values.ShouldAllBe(text => !text.Contains("partial interface"));
    }

    // An internal type's interface is internal.
    [Fact]
    public void Run_InternalType_WritesAnInternalInterface()
    {
        var run = RepositoryRuns.Run(
            UsersFile,
            [GetUserEntry()],
            Source.Replace("public partial class", "internal partial class", StringComparison.Ordinal)
        );

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain("    internal partial interface IUserRepository\n");
    }

    // The text from the interface's summary to the end of the file.
    private static string InterfaceOf(string methods) =>
        methods[methods.IndexOf("    /// <summary>\n    /// The public", StringComparison.Ordinal)..];

    private static SidecarEntry GetUserEntry(SqlFile? file = null, string name = "GetUser") =>
        Entry(file ?? UsersFile, name, [Parameter("id")], [Column("id")]);
}
