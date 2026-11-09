// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// A project that sees the internals of another one that uses SqlSource, as a test project sees the project it tests.
// Both hold the generated attributes, but each marks them embedded, so neither sees the other's copy and the
// compiler has no conflict (CS0436) to warn about, with no analyzer loaded.  Each test of a generated type across the
// boundary runs on an image, as a build references a project, and on a compilation, as an IDE does; the test of a type
// the user declares in both projects needs only one.
public sealed class AttributeConflictTests
{
    private const string InternalsVisible =
        "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestAssembly\")]\n";

    // The file of the class of NodaTime's conversions.
    private const string ConversionsFile = "SqlSource-NodaTimeConversions.g.cs";

    // A query of each project that reads a datetime2 overridden to NodaTime's Instant, so that its method calls
    // SqlSource.NodaTimeConversions.
    private const string Override = "-- type-override: datetime2=NodaTime.Instant\n\n";

    // This project's type and the other's, each with the queries of its .sql files.
    private const string QueriesSource =
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(Source = \".\")]\ninternal partial class Sample;\n";

    private const string OtherQueriesSource =
        "using SqlSource;\n"
        + InternalsVisible
        + "namespace App { [SqlSourceGenerate(Source = \".\")] public partial class Sample; }";

    private static readonly MetadataReference[] SqlServerReferences =
    [
        SqlClientReferences.SqlClient7,
        LibraryReferences.NodaTime("3.3.7"),
    ];

    // Each use of a generated type in the project's own code, as an image and as a compilation.
    public static TheoryData<string, bool> UsesOfAGeneratedType()
    {
        string[] sources =
        [
            "using SqlSource;\n[SqlSourceGenerate(Source = \".\")]\ninternal partial class Sample { }",
            "using SqlSource;\n[SqlSourceGenerateAttribute(Source = \".\")]\ninternal partial class Sample { }",
            "[SqlSource.SqlSourceGenerate(Source = \".\")]\ninternal partial class Sample { }",
            "[global::SqlSource.SqlSourceGenerate(Source = \".\")]\ninternal partial class Sample { }",
            "internal class Sample { object o = typeof(SqlSource.SqlSourceGenerateAttribute); }",
            "internal static class C { [SqlSource.SqlSourceConverter] internal static int F(long v) => (int)v; }",
        ];
        var uses = new TheoryData<string, bool>();
        foreach (var source in sources)
        {
            uses.Add(source, false);
            uses.Add(source, true);
        }

        return uses;
    }

    [Theory]
    [MemberData(nameof(UsesOfAGeneratedType))]
    public async Task Build_UseOfAGeneratedType_HasNoConflict(string source, bool compilationReference)
    {
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql, compilationReference)]
        );

        build.ShouldBeEmpty();
    }

    // The case that fails a build: a project that treats warnings as errors.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_WarningsAsErrors_HasNoConflictForAGeneratedType(bool compilationReference)
    {
        const string Source =
            "using SqlSource;\n[SqlSourceGenerate(Source = \".\")]\ninternal partial class Sample { }";

        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql, compilationReference)],
            warningsAsErrors: true
        );

        build.ShouldBeEmpty();
    }

    // The compiler's error is the whole story: the conflict is not reported next to it.
    [Theory]
    [InlineData("[SqlSourceGenerate(1)]", "CS1729 /app/Repo/Sample.cs(2,2)-(2,22)", false)]
    [InlineData("[SqlSourceGenerate(1)]", "CS1729 /app/Repo/Sample.cs(2,2)-(2,22)", true)]
    [InlineData("[SqlSourceGenerate(Source = \".\", Missing = 1)]", "CS0246 /app/Repo/Sample.cs(2,34)-(2,41)", false)]
    [InlineData("[SqlSourceGenerate(Source = \".\", Missing = 1)]", "CS0246 /app/Repo/Sample.cs(2,34)-(2,41)", true)]
    public async Task Build_AttributeThatDoesNotCompile_HasItsErrorAndNoConflict(
        string attribute,
        string error,
        bool compilationReference
    )
    {
        var source = $"using SqlSource;\n{attribute}\ninternal partial class Sample {{ }}";

        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql, compilationReference)]
        );

        Places(build).ShouldBe([error]);
    }

    // A type that the project and the other one both declare by hand is a conflict that the user has to know about,
    // whatever its name.
    [Theory]
    [InlineData("Mine", "SqlSourceConverterAttribute")]
    [InlineData("Mine", "SqlSourceGenerateAttribute")]
    [InlineData("Mine", "SqlSourceRowBuffer")]
    [InlineData("SqlSource", "Shared")]
    [InlineData("SqlSource.Inner", "SqlSourceConverterAttribute")]
    public async Task Build_UseOfATypeThatBothProjectsDeclare_IsStillAConflict(string @namespace, string name)
    {
        var declaration = $"namespace {@namespace} {{ internal class {name} {{ }} }}\n";
        var other = GeneratorHarness.CreateReference(OutputKind.Sql, "Other", InternalsVisible + declaration);
        var source = declaration + $"internal class Sample {{ object o = typeof({@namespace}.{name}); }}";

        var build = await GeneratorHarness.BuildAsync(OutputKind.Sql, source, [], [other]);

        Places(build).ShouldHaveSingleItem().ShouldStartWith("CS0436 ");
    }

    // Both projects generate SqlSource.SqlSourceRowBuffer, marked as embedded, so that neither sees the other's.  The
    // queries have different names, so that their models are not a conflict of their own.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_TwoProjectsSharingInternalsWithPooledMethods_HaveNoConflict(bool compilationReference)
    {
        var otherFile = new SqlFile("/other/Users.sql", "-- name: ListUsers\nSELECT id, name FROM users;\n");
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListOthers\nSELECT id, name FROM users;\n");
        var columns = new[] { Column("id"), Column("name", Text, nullable: true) };
        var (reference, sources) = GeneratorHarness.CreateProject(
            OutputKind.Methods,
            "Other",
            OtherQueriesSource,
            [otherFile, Sidecars.Of(otherFile, Entry(otherFile, "ListUsers", [], columns))],
            [NpgsqlReferences.Npgsql10],
            "postgres",
            compilationReference: compilationReference
        );
        var entry = Entry(file, "ListOthers", [], columns);

        var (diagnostics, builtSources) = await GeneratorHarness.BuildProjectAsync(
            OutputKind.Methods,
            QueriesSource,
            [file, Sidecars.Of(file, entry)],
            [reference, NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldContain(RowBuffer.HintName);
        builtSources.Keys.ShouldContain(RowBuffer.HintName);
        sources[MethodRuns.MethodsFile].ShouldNotContain("struct SqlSourceRowBuffer");
        builtSources[MethodRuns.MethodsFile].ShouldNotContain("struct SqlSourceRowBuffer");
    }

    // Both projects call a conversion of NodaTime's, so both hold SqlSource.NodaTimeConversions, and the methods this
    // project generates name it where the other's is too.  The queries have different names, so that their models are
    // not a conflict of their own.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_NodaTimeConversionsInBothProjects_HaveNoConflict(bool compilationReference)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, Override + "-- name: ListOthers\nSELECT at FROM events;\n");
        var (reference, sources) = OtherWithConversions(compilationReference);
        SqlFile[] files = [file, Sidecars.Of(file, MethodRuns.SqlServerEntry(file, "ListOthers", [], [At()]))];

        var (diagnostics, builtSources) = await GeneratorHarness.BuildProjectAsync(
            OutputKind.Methods,
            QueriesSource,
            files,
            [reference, .. SqlServerReferences],
            dialect: "mssql"
        );

        sources.Keys.ShouldContain(ConversionsFile);
        builtSources.Keys.ShouldContain(ConversionsFile);
        diagnostics.ShouldBeEmpty();
    }

    // A project without SqlSource that sees the other's internals does not see the types SqlSource gave the other: the
    // attribute and a library's class of conversions are the other's alone.
    [Theory]
    [InlineData("typeof(SqlSource.NodaTimeConversions)", "NodaTimeConversions", false)]
    [InlineData("typeof(SqlSource.NodaTimeConversions)", "NodaTimeConversions", true)]
    [InlineData("typeof(SqlSource.SqlSourceGenerateAttribute)", "SqlSourceGenerateAttribute", false)]
    [InlineData("typeof(SqlSource.SqlSourceGenerateAttribute)", "SqlSourceGenerateAttribute", true)]
    public async Task Build_FriendWithoutSqlSource_CannotNameTheOthersEmbeddedTypes(
        string expression,
        string name,
        bool compilationReference
    )
    {
        var (other, _) = OtherWithConversions(compilationReference);

        var diagnostics = await GeneratorHarness.CompileAsync(
            "internal class Sample { object o = " + expression + "; }",
            [other, .. SqlServerReferences]
        );

        diagnostics.ShouldHaveSingleItem().ShouldStartWith($"CS0234 {GeneratorHarness.SourcePath}(1,");
        diagnostics[0].ShouldContain($"'{name}' does not exist in the namespace 'SqlSource'");
    }

    // Another package's generator adds Roslyn's own Microsoft.CodeAnalysis.EmbeddedAttribute to the project, which
    // merges with SqlSource's declaration of it.
    [Fact]
    public async Task Build_ProjectWithAnotherGeneratorsEmbeddedAttribute_CompilesClean()
    {
        const string Source =
            "using SqlSource;\n[SqlSourceGenerate(Source = \".\")]\ninternal partial class Sample { }";

        var (diagnostics, sources) = await GeneratorHarness.BuildProjectAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [],
            otherGenerator: new RoslynEmbeddedAttributeGenerator()
        );

        diagnostics.ShouldBeEmpty();
        sources.Keys.ShouldContain(GeneratedFile.EmbeddedAttributeName);
        sources.Keys.ShouldContain("Microsoft.CodeAnalysis.EmbeddedAttribute.cs");
    }

    // A project that declares Microsoft.CodeAnalysis.EmbeddedAttribute itself, as another generator may have it do,
    // compiles only when its declaration can be merged with Roslyn's internal sealed partial class: it is partial,
    // internal, not static, and a class that derives from System.Attribute or names no base.  Otherwise the build fails
    // with the compiler's errors, which SqlSource cannot report first; their order varies, so their ids are compared.
    [Theory]
    [InlineData("internal sealed partial class EmbeddedAttribute : global::System.Attribute", "")]
    [InlineData("internal partial class EmbeddedAttribute : global::System.Attribute", "")]
    [InlineData("internal sealed partial class EmbeddedAttribute", "")]
    [InlineData("internal sealed class EmbeddedAttribute : global::System.Attribute", "CS0260")]
    [InlineData("public sealed partial class EmbeddedAttribute : global::System.Attribute", "CS0262 CS9271")]
    [InlineData("internal sealed partial class EmbeddedAttribute : global::System.Exception", "CS0263 CS0616 CS9271")]
    [InlineData("internal static partial class EmbeddedAttribute", "CS0441 CS0616 CS0713 CS9271")]
    [InlineData("internal sealed partial record EmbeddedAttribute", "CS0261 CS9271")]
    public async Task Build_ProjectThatDeclaresEmbeddedAttribute_CompilesOnlyWhenRoslynsCanBeMergedWithIt(
        string declaration,
        string ids
    )
    {
        var source =
            "namespace Microsoft.CodeAnalysis\n{\n    /// <summary>The project's own.</summary>\n    "
            + declaration
            + " { }\n}\n\nnamespace App\n{\n    [SqlSource.SqlSourceGenerate(Source = \".\")]\n"
            + "    internal partial class Sample { }\n}\n";

        var build = await GeneratorHarness.BuildAsync(OutputKind.Sql, source, [GeneratorHarness.UsersFile], []);

        string.Join(
                " ",
                build
                    .Select(diagnostic => diagnostic[..diagnostic.IndexOf(' ', System.StringComparison.Ordinal)])
                    .Distinct()
                    .Order(System.StringComparer.Ordinal)
            )
            .ShouldBe(ids);
    }

    // The other project, compiled with the generator: it holds the attributes and the exceptions too.
    private static MetadataReference Other(OutputKind? output, bool compilationReference) =>
        GeneratorHarness
            .CreateProject(
                output,
                "Other",
                InternalsVisible + "public class InOther { }\n",
                compilationReference: compilationReference
            )
            .Reference;

    // The other project with a methods query on SQL Server whose method calls a conversion of NodaTime's, so that it
    // holds SqlSource.NodaTimeConversions as well as the attributes and the exceptions.
    private static (MetadataReference Reference, IReadOnlyDictionary<string, string> Sources) OtherWithConversions(
        bool compilationReference
    )
    {
        var otherFile = new SqlFile("/other/Events.sql", Override + "-- name: ListEvents\nSELECT at FROM events;\n");
        return GeneratorHarness.CreateProject(
            OutputKind.Methods,
            "Other",
            OtherQueriesSource,
            [otherFile, Sidecars.Of(otherFile, MethodRuns.SqlServerEntry(otherFile, "ListEvents", [], [At()]))],
            SqlServerReferences,
            "mssql",
            compilationReference: compilationReference
        );
    }

    // The column at, a datetime2.
    private static SidecarColumn At() => TestSidecars.Column(0, "at", TestSidecars.SqlServer("datetime2", 8, 27, 7));

    // Each diagnostic without its message, which for CS0436 names files and assemblies, and with
    // '/' between folders, since on Windows the compiler names a generated file's folder with '\'.
    private static IEnumerable<string> Places(IEnumerable<string> diagnostics) =>
        diagnostics.Select(diagnostic =>
            diagnostic[..diagnostic.IndexOf(": ", System.StringComparison.Ordinal)].Replace('\\', '/')
        );
}
