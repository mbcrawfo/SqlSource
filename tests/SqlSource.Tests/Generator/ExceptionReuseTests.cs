// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Exceptions;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// UnexpectedRowCountException, TransactionRolledBackException and CollectionItemException: a project writes them,
// internal or public, unless one referenced assembly that it sees gives it all three, which it then uses, so that a
// project that sees another's internals catches the other's by type.  Each test that builds across the boundary runs
// on an image, as a build references a project, and on a compilation, as an IDE does.
public sealed class ExceptionReuseTests
{
    private const string InternalsVisible =
        "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestAssembly\")]\n";

    // Code that names both exceptions, as a caller that catches them does.
    private const string CatchSource = """
        namespace Caller
        {
            internal static class Calls
            {
                public static void Call(System.Action action)
                {
                    try
                    {
                        action();
                    }
                    catch (SqlSource.UnexpectedRowCountException e)
                    {
                        _ = e.QueryName;
                    }
                    catch (SqlSource.TransactionRolledBackException)
                    {
                    }
                    catch (SqlSource.CollectionItemException e)
                    {
                        _ = e.ItemIndex;
                    }
                }
            }
        }

        """;

    // A type of the other project with a methods query of the shape one, whose method throws
    // UnexpectedRowCountException.
    private const string OtherQueriesSource = """
        namespace Other
        {
            [SqlSource.SqlSourceGenerate(Source = ".")]
            public partial class OtherQueries;
        }

        """;

    private static readonly SqlFile OtherFile = new(
        "/other/Users.sql",
        "-- name: GetUser -> one\nSELECT id FROM users;\n"
    );

    // This project's own methods query of the shape one, whose method throws the exception it sees.
    private static readonly SqlFile OwnFile = new(
        GeneratorHarness.UsersPath,
        "-- name: GetOne -> one\nSELECT id FROM users;\n"
    );

    // The project sets SqlSourcePublicExceptions to true.
    private static readonly Dictionary<string, string?> Public = new() { [PublicExceptions.PropertyName] = "true" };

    [Fact]
    public void Run_NoReferenceHasThem_WritesThemInternal()
    {
        var run = GeneratorHarness.Run(OutputKind.Sql, GeneratorHarness.SampleSource(null), GeneratorHarness.UsersFile);

        run.ShouldBeClean();
        var exceptions = run.Sources[ExceptionSource.HintName];
        exceptions.ShouldContain("internal sealed class UnexpectedRowCountException ");
        exceptions.ShouldContain("internal sealed class TransactionRolledBackException ");
        exceptions.ShouldContain("internal sealed class CollectionItemException ");
        var attribute = run.Sources[AttributeSource.HintName];
        attribute.ShouldNotContain("UnexpectedRowCountException");
        attribute.ShouldNotContain("TransactionRolledBackException");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void Run_PublicExceptionsTrue_WritesThemPublic(string value)
    {
        var run = RunWithPublicExceptions(value);

        run.ShouldBeClean();
        var exceptions = run.Sources[ExceptionSource.HintName];
        exceptions.ShouldContain("public sealed class UnexpectedRowCountException ");
        exceptions.ShouldContain("public sealed class TransactionRolledBackException ");
        exceptions.ShouldContain("public sealed class CollectionItemException ");
        exceptions.ShouldNotContain("internal ");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("yes")]
    public void Run_PublicExceptionsOtherwise_WritesThemInternal(string value)
    {
        var run = RunWithPublicExceptions(value);

        run.ShouldBeClean();
        var exceptions = run.Sources[ExceptionSource.HintName];
        exceptions.ShouldContain("internal sealed class UnexpectedRowCountException ");
        exceptions.ShouldContain("internal sealed class TransactionRolledBackException ");
        exceptions.ShouldContain("internal sealed class CollectionItemException ");
        exceptions.ShouldNotContain("public sealed class");
    }

    // The project of the shared-project workaround uses SqlSource for its exceptions alone.
    [Fact]
    public void Run_ProjectWithNoAttributeAndNoQueries_WritesThem()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample { }")],
            []
        );

        run.ShouldBeClean();
        run.Sources.Keys.ShouldBe(
            [GeneratedFile.EmbeddedAttributeName, AttributeSource.HintName, ExceptionSource.HintName],
            ignoreOrder: true
        );
        run.Sources[ExceptionSource.HintName].ShouldBe(ExceptionSource.TextFor(isPublic: false));
    }

    // The other project's method throws its exception, and this one's method throws the same type, which this
    // project's code catches by type.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_FriendThatUsesSqlSource_ReusesAndCatchesTheOthersExceptions(bool compilationReference)
    {
        var other = OtherWithQuery(InternalsVisible, properties: null, compilationReference);

        var (diagnostics, sources) = await BuildWithQuery(other);

        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        sources.Keys.ShouldContain(MethodRuns.MethodsFile);
    }

    // A cref is bound without regard to accessibility, so the copy of an assembly that this project cannot see, beside
    // the friend's that it uses, would make a bound cref in a method's documentation ambiguous (CS0419).  The harness
    // documents generated code, as a project that builds a documentation file does.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_ReuseWithAnotherAssemblysCopyReferenced_HasNoAmbiguousCref(bool compilationReference)
    {
        var friend = OtherWithQuery(InternalsVisible, properties: null, compilationReference);
        var (stranger, strangerSources) = GeneratorHarness.CreateProject(
            OutputKind.Sql,
            "Stranger",
            "public class InStranger { }\n",
            compilationReference: compilationReference
        );

        var (diagnostics, sources) = await BuildWithQuery(friend, references: [stranger]);

        strangerSources.Keys.ShouldContain(ExceptionSource.HintName);
        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        sources[MethodRuns.MethodsFile].ShouldContain("cref=\"T:SqlSource.UnexpectedRowCountException\"");
    }

    // The project's own documentation: a cref is bound without regard to accessibility, so beside the friend's copy
    // that it uses, the stranger's that it cannot see makes a bound cref to an exception ambiguous (CS0419), and one to
    // a member of it unresolved (CS1574).  This pins today's behaviour (TD-0095).
    [Theory]
    [InlineData("SqlSource.UnexpectedRowCountException", "CS0419", false)]
    [InlineData("SqlSource.UnexpectedRowCountException", "CS0419", true)]
    [InlineData("SqlSource.UnexpectedRowCountException.QueryName", "CS1574", false)]
    [InlineData("SqlSource.UnexpectedRowCountException.QueryName", "CS1574", true)]
    public async Task Build_UserCrefWithAStrangersCopyReferenced_IsAWarning(
        string cref,
        string id,
        bool compilationReference
    )
    {
        var (diagnostics, sources) = await BuildWithCref(cref, compilationReference, withStranger: true);

        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        diagnostics.ShouldHaveSingleItem().ShouldStartWith(id + " " + GeneratorHarness.SourcePath + "(7,");
    }

    // The control: with the friend's copy alone, the same crefs bind.
    [Theory]
    [InlineData("SqlSource.UnexpectedRowCountException", false)]
    [InlineData("SqlSource.UnexpectedRowCountException", true)]
    [InlineData("SqlSource.UnexpectedRowCountException.QueryName", false)]
    [InlineData("SqlSource.UnexpectedRowCountException.QueryName", true)]
    public async Task Build_UserCrefWithTheFriendsCopyAlone_HasNoDiagnostic(string cref, bool compilationReference)
    {
        var (diagnostics, sources) = await BuildWithCref(cref, compilationReference, withStranger: false);

        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        diagnostics.ShouldBeEmpty();
    }

    // The workaround the manual gives: the documentation ID of what the cref names, which the compiler does not bind.
    [Theory]
    [InlineData("T:SqlSource.UnexpectedRowCountException", false)]
    [InlineData("T:SqlSource.UnexpectedRowCountException", true)]
    [InlineData("P:SqlSource.UnexpectedRowCountException.QueryName", false)]
    [InlineData("P:SqlSource.UnexpectedRowCountException.QueryName", true)]
    public async Task Build_UserCrefAsADocumentationIdWithAStrangersCopyReferenced_HasNoDiagnostic(
        string cref,
        bool compilationReference
    )
    {
        var (diagnostics, sources) = await BuildWithCref(cref, compilationReference, withStranger: true);

        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_FriendThatUsesSqlSourceWithPublicExceptions_StillReuses(bool compilationReference)
    {
        var other = OtherWithQuery(InternalsVisible, properties: null, compilationReference);

        var (diagnostics, sources) = await BuildWithQuery(other, Public);

        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_FriendWithoutSqlSource_CatchesTheOthersExceptionsByType(bool compilationReference)
    {
        var other = OtherWithQuery(InternalsVisible, properties: null, compilationReference);

        var diagnostics = await GeneratorHarness.CompileAsync(CatchSource, [other]);

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_PublicExceptionsWithoutInternalsVisibleTo_AreCaughtByType(bool compilationReference)
    {
        var other = OtherWithQuery(string.Empty, Public, compilationReference);

        var diagnostics = await GeneratorHarness.CompileAsync(CatchSource, [other]);

        diagnostics.ShouldBeEmpty();
    }

    // A copy that this project cannot see is neither used nor a second copy.  Without SqlSourcePublicExceptions the
    // exceptions are internal, so a project without SqlSource that is no friend cannot name them.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_InternalExceptionsOfANonFriend_AreNotSeen(bool compilationReference)
    {
        var stranger = OtherWithQuery(string.Empty, properties: null, compilationReference);
        var friend = GeneratorHarness
            .CreateProject(
                OutputKind.Sql,
                "Friend",
                InternalsVisible + "public class InFriend { }\n",
                compilationReference: compilationReference
            )
            .Reference;

        var (diagnostics, sources) = await GeneratorHarness.BuildProjectAsync(
            OutputKind.Sql,
            CatchSource,
            [],
            [stranger]
        );
        var withFriend = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, CatchSource)],
            [],
            references: [stranger, friend]
        );
        var withoutSqlSource = await GeneratorHarness.CompileAsync(CatchSource, [stranger]);

        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldContain(ExceptionSource.HintName);
        withFriend.ShouldBeClean();
        withFriend.Sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        StepOutputs
            .Single<ExceptionProbe>(withFriend, TrackingNames.ExceptionProbe)
            .ShouldBe(new ExceptionProbe(ExceptionOrigin.Referenced, new(["Friend"])));
        withoutSqlSource.ShouldNotBeEmpty();
        withoutSqlSource.ShouldAllBe(diagnostic => diagnostic.StartsWith("CS0122 ", System.StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_TwoFriendsHaveThem_ReportsSqlsrc022AndWritesItsOwn(bool compilationReference)
    {
        var first = Friend("OtherA", compilationReference);
        var second = Friend("OtherB", compilationReference);

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample { }")],
            [],
            references: [second, first]
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC022 (1,1)-(1,1): This project sees UnexpectedRowCountException, TransactionRolledBackException or "
                + "CollectionItemException in more than one referenced assembly: OtherA, OtherB.  Use SqlSource in one "
                + "project that the others reference and that grants them InternalsVisibleTo, so that they all use its "
                + "exceptions.",
        ]);
        run.Sources.Keys.ShouldContain(ExceptionSource.HintName);
        run.CompilationErrors.ShouldBeEmpty();
    }

    // A reference under an extern alias that is not global is no copy that the generated methods can name as
    // global::SqlSource.UnexpectedRowCountException, so the project writes its own.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_FriendOnlyUnderAnAlias_WritesItsOwn(bool compilationReference)
    {
        var (diagnostics, sources) = await BuildOwnQuery(
            Friend("Friend", compilationReference).WithAliases(["friend"])
        );

        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldContain(ExceptionSource.HintName);
        sources.Keys.ShouldContain(MethodRuns.MethodsFile);
    }

    // A reference with no alias, or with global among its aliases, is reached from global::, and its copy is used.
    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "global", "friend" }, false)]
    [InlineData(new[] { "global", "friend" }, true)]
    public async Task Build_FriendReachedFromGlobal_IsUsed(string[] aliases, bool compilationReference)
    {
        var (diagnostics, sources) = await BuildOwnQuery(Friend("Friend", compilationReference).WithAliases(aliases));

        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        sources.Keys.ShouldContain(MethodRuns.MethodsFile);
    }

    // Of two friends, one only under an alias: the other is the one copy, used with no SQLSRC022.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_TwoFriendsOneUnderAnAlias_UsesTheGlobalOne(bool compilationReference)
    {
        var aliased = Friend("OtherA", compilationReference).WithAliases(["friend"]);
        var global = Friend("OtherB", compilationReference);

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample { }")],
            [],
            references: [aliased, global]
        );

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(ExceptionSource.HintName);
        StepOutputs
            .Single<ExceptionProbe>(run, TrackingNames.ExceptionProbe)
            .ShouldBe(new ExceptionProbe(ExceptionOrigin.Referenced, new(["OtherB"])));
    }

    // The workaround for SQLSRC022: Shared uses SqlSource for its exceptions alone and is the friend of DataA and of
    // this project, so DataA uses its exceptions, and this project, which sees both, sees one copy.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_SharedProjectWorkaround_HasNoError(bool compilationReference)
    {
        var (shared, sharedSources) = GeneratorHarness.CreateProject(
            OutputKind.Sql,
            "Shared",
            "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"DataA\")]\n"
                + InternalsVisible
                + "public class InShared { }\n",
            compilationReference: compilationReference
        );
        var (dataA, dataASources) = GeneratorHarness.CreateProject(
            OutputKind.Methods,
            "DataA",
            InternalsVisible + OtherQueriesSource,
            [OtherFile, Sidecars.Of(OtherFile, Entry(OtherFile, "GetUser", [], [Column("id")]))],
            [shared, NpgsqlReferences.Npgsql10],
            "postgres",
            compilationReference: compilationReference
        );

        var (diagnostics, sources) = await BuildWithQuery(dataA, references: [shared]);

        sharedSources.Keys.ShouldContain(ExceptionSource.HintName);
        dataASources.Keys.ShouldNotContain(ExceptionSource.HintName);
        dataASources.Keys.ShouldContain("Other.OtherQueries-Methods.g.cs");
        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldNotContain(ExceptionSource.HintName);
    }

    // Copies of another version of SqlSource: one exception without the other, a constructor that a generated method
    // calls missing, a class that is no exception, one that cannot be created, or public types whose constructors this
    // project cannot call.  Each as an image and as a compilation.
    public static TheoryData<string, bool> OtherShapes()
    {
        string[] sources =
        [
            Old(RowCount("System.InvalidOperationException", "string queryName, bool moreThanOne") + Item()),
            Old(
                RowCount("System.InvalidOperationException", "string queryName")
                    + RolledBack("System.InvalidOperationException", "string queryName, System.Exception inner")
                    + Item()
            ),
            Old(
                RowCount("System.InvalidOperationException", "string queryName, bool moreThanOne")
                    + RolledBack("System.InvalidOperationException", "string queryName")
                    + Item()
            ),
            Old(
                RowCount("System.Object", "string queryName, bool moreThanOne")
                    + RolledBack("System.Object", "string queryName, System.Exception inner")
                    + Item("System.Object")
            ),
            Old(
                RowCount("System.InvalidOperationException", "string queryName, bool moreThanOne", "abstract")
                    + RolledBack("System.InvalidOperationException", "string queryName, System.Exception inner")
                    + Item()
            ),
            // The exceptions of a version before CollectionItemException, with no third.
            Old(
                RowCount("System.InvalidOperationException", "string queryName, bool moreThanOne")
                    + RolledBack("System.InvalidOperationException", "string queryName, System.Exception inner")
            ),
            Old(
                RowCount("System.InvalidOperationException", "string queryName, bool moreThanOne")
                    + RolledBack("System.InvalidOperationException", "string queryName, System.Exception inner")
                    + Item(parameters: "string queryName, int itemIndex")
            ),
            Old(
                RowCount("System.InvalidOperationException", "string queryName, bool moreThanOne")
                    + RolledBack("System.InvalidOperationException", "string queryName, System.Exception inner")
                    + Item(parameters: "string queryName, int itemIndex, System.Exception inner")
            ),
            Old(
                RowCount("System.InvalidOperationException", "string queryName, bool moreThanOne")
                    + RolledBack("System.InvalidOperationException", "string queryName, System.Exception inner")
                    + Item("System.Object")
            ),
            Old(
                RowCount(
                    "System.InvalidOperationException",
                    "string queryName, bool moreThanOne",
                    accessibility: "public",
                    constructorAccessibility: "internal"
                )
                    + RolledBack(
                        "System.InvalidOperationException",
                        "string queryName, System.Exception inner",
                        accessibility: "public",
                        constructorAccessibility: "internal"
                    )
                    + Item(accessibility: "public", constructorAccessibility: "internal"),
                internalsVisible: false
            ),
        ];
        var shapes = new TheoryData<string, bool>();
        foreach (var source in sources)
        {
            shapes.Add(source, false);
            shapes.Add(source, true);
        }

        return shapes;
    }

    [Theory]
    [MemberData(nameof(OtherShapes))]
    public void Run_ReferenceOfAnotherShape_ReportsSqlsrc023AndWritesItsOwn(string source, bool compilationReference)
    {
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/old/Old.cs", source)],
            assemblyName: "Old"
        );
        MetadataReference old = compilationReference ? compilation.ToMetadataReference() : ImageOf(compilation);

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample { }")],
            [],
            references: [old]
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC023 (1,1)-(1,1): The UnexpectedRowCountException, TransactionRolledBackException and "
                + "CollectionItemException that this project sees in Old are not those of this version of SqlSource.  "
                + "Use the same version of SqlSource in both projects.",
        ]);
        run.Sources.Keys.ShouldContain(ExceptionSource.HintName);
        run.CompilationErrors.ShouldBeEmpty();
    }

    // The source of Old: the declarations in the namespace SqlSource, with InternalsVisibleTo for this project unless
    // it is left out.
    private static string Old(string declarations, bool internalsVisible = true) =>
        (internalsVisible ? InternalsVisible : string.Empty) + "namespace SqlSource { " + declarations + " }\n";

    // A copy of UnexpectedRowCountException, internal and sealed unless they are given, of this base and constructor,
    // public unless its accessibility is given.
    private static string RowCount(
        string baseType,
        string parameters,
        string modifier = "sealed",
        string accessibility = "internal",
        string constructorAccessibility = "public"
    ) =>
        accessibility
        + " "
        + modifier
        + " class UnexpectedRowCountException : "
        + baseType
        + " { "
        + constructorAccessibility
        + " UnexpectedRowCountException("
        + parameters
        + ") { } } ";

    // A copy of TransactionRolledBackException of this base and constructor, with the accessibility of each as given.
    private static string RolledBack(
        string baseType,
        string parameters,
        string accessibility = "internal",
        string constructorAccessibility = "public"
    ) =>
        accessibility
        + " sealed class TransactionRolledBackException : "
        + baseType
        + " { "
        + constructorAccessibility
        + " TransactionRolledBackException("
        + parameters
        + ") { } } ";

    // A copy of CollectionItemException of this base and constructor, with the accessibility of each as given.
    private static string Item(
        string baseType = "System.Data.Common.DbException",
        string parameters = "string queryName, int itemIndex, System.Data.Common.DbException inner",
        string accessibility = "internal",
        string constructorAccessibility = "public"
    ) =>
        accessibility
        + " sealed class CollectionItemException : "
        + baseType
        + " { "
        + constructorAccessibility
        + " CollectionItemException("
        + parameters
        + ") { } } ";

    // The compilation's image, as a build references a project.
    private static PortableExecutableReference ImageOf(Compilation compilation)
    {
        using var image = new System.IO.MemoryStream();
        compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    private static GeneratorRun RunWithPublicExceptions(string value) =>
        GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample { }")],
            [],
            properties: new Dictionary<string, string?> { [PublicExceptions.PropertyName] = value }
        );

    // The other project, which uses SqlSource with a methods query on Npgsql 10, with this text before its source.
    private static MetadataReference OtherWithQuery(
        string before,
        IReadOnlyDictionary<string, string?>? properties,
        bool compilationReference
    ) =>
        GeneratorHarness
            .CreateProject(
                OutputKind.Methods,
                "Other",
                before + OtherQueriesSource,
                [OtherFile, Sidecars.Of(OtherFile, Entry(OtherFile, "GetUser", [], [Column("id")]))],
                [NpgsqlReferences.Npgsql10],
                "postgres",
                properties,
                compilationReference
            )
            .Reference;

    // This project, which uses SqlSource with a methods query of the shape one and nothing else, over the reference.
    private static Task<(IReadOnlyList<string> Diagnostics, IReadOnlyDictionary<string, string> Sources)> BuildOwnQuery(
        MetadataReference reference
    ) =>
        GeneratorHarness.BuildProjectAsync(
            OutputKind.Methods,
            "namespace App { [SqlSource.SqlSourceGenerate(Source = \".\")] internal partial class Sample; }\n",
            [OwnFile, Sidecars.Of(OwnFile, Entry(OwnFile, "GetOne", [], [Column("id")]))],
            [reference, NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

    // This project, which uses SqlSource and documents a method with the cref, over a friend whose exceptions it uses
    // and, when asked, a stranger whose copy it cannot see.
    private static Task<(IReadOnlyList<string> Diagnostics, IReadOnlyDictionary<string, string> Sources)> BuildWithCref(
        string cref,
        bool compilationReference,
        bool withStranger
    )
    {
        MetadataReference[] references = withStranger
            ? [Friend("Friend", compilationReference), Stranger(compilationReference)]
            : [Friend("Friend", compilationReference)];
        var source = $$"""
            namespace App
            {
                /// <summary>Calls the queries.</summary>
                public static class Calls
                {
                    /// <summary>Runs a query.</summary>
                    /// <seealso cref="{{cref}}"/>
                    public static void Run() { }
                }
            }

            """;
        return GeneratorHarness.BuildProjectAsync(OutputKind.Sql, source, [], references);
    }

    // A project that uses SqlSource and grants this one no InternalsVisibleTo, so that it holds a copy of the
    // exceptions that this project cannot see.
    private static MetadataReference Stranger(bool compilationReference) =>
        GeneratorHarness
            .CreateProject(
                OutputKind.Sql,
                "Stranger",
                "public class InStranger { }\n",
                compilationReference: compilationReference
            )
            .Reference;

    // A friend that uses SqlSource with no query.
    private static MetadataReference Friend(string assemblyName, bool compilationReference) =>
        GeneratorHarness
            .CreateProject(
                OutputKind.Sql,
                assemblyName,
                InternalsVisible + "public class In" + assemblyName + " { }\n",
                compilationReference: compilationReference
            )
            .Reference;

    // This project, which uses SqlSource with a methods query of the shape one, calls the other's method and catches
    // both exceptions by type, after the other references.
    private static Task<(
        IReadOnlyList<string> Diagnostics,
        IReadOnlyDictionary<string, string> Sources
    )> BuildWithQuery(
        MetadataReference other,
        IReadOnlyDictionary<string, string?>? properties = null,
        MetadataReference[]? references = null
    ) =>
        GeneratorHarness.BuildProjectAsync(
            OutputKind.Methods,
            """
            using System.Data.Common;
            using System.Threading.Tasks;
            using Other;

            namespace App
            {
                [SqlSource.SqlSourceGenerate(Source = ".")]
                internal partial class Sample;

                internal static class Calls
                {
                    public static async Task CallAsync(DbConnection connection)
                    {
                        try
                        {
                            _ = await connection.GetUser();
                            _ = await connection.GetOne();
                        }
                        catch (SqlSource.UnexpectedRowCountException e)
                        {
                            _ = e.QueryName;
                        }
                        catch (SqlSource.TransactionRolledBackException)
                        {
                        }
                    }
                }
            }

            """,
            [OwnFile, Sidecars.Of(OwnFile, Entry(OwnFile, "GetOne", [], [Column("id")]))],
            [.. references ?? [], other, NpgsqlReferences.Npgsql10],
            dialect: "postgres",
            properties: properties
        );
}
