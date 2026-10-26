// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// A project that sees the internals of another one that uses SqlSource, as a test project sees the project it tests.
// Both hold the generated attribute and enums, and the compiler warns (CS0436) at every use of one of their names
// in this project's own code.  The package turns that warning off, for the generated types only.
public sealed class AttributeConflictTests
{
    private const string InternalsVisible =
        "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestAssembly\")]\n";

    [Theory]
    [InlineData("using SqlSource;\n[SqlSourceGenerate]\ninternal partial class Sample { }", "(2,2)-(2,19)")]
    [InlineData("using SqlSource;\n[SqlSourceGenerateAttribute]\ninternal partial class Sample { }", "(2,2)-(2,28)")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal partial class Sample { }", "(1,12)-(1,29)")]
    [InlineData("[global::SqlSource.SqlSourceGenerate()]\ninternal partial class Sample { }", "(1,20)-(1,37)")]
    [InlineData("internal class Sample { object o = typeof(SqlSource.SqlLocation); }", "(1,53)-(1,64)")]
    [InlineData("internal class Sample { object o = SqlSource.SqlLocation.Direct; }", "(1,36)-(1,57)")]
    [InlineData("internal class Sample { object o = typeof(SqlSource.GeneratorOutput); }", "(1,53)-(1,68)")]
    [InlineData("internal class Sample { object o = SqlSource.GeneratorOutput.Sql; }", "(1,36)-(1,61)")]
    [InlineData("internal class Sample { object o = typeof(SqlSource.UnexpectedRowCountException); }", "(1,53)-(1,80)")]
    [InlineData(
        "internal static class C { [SqlSource.SqlSourceConverter] internal static int F(long v) => (int)v; }",
        "(1,38)-(1,56)"
    )]
    public async Task Build_UseOfAGeneratedType_IsAConflictOnlyWithoutThePackageAnalyzers(string source, string span)
    {
        var compilerAlone = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)],
            packageAnalyzers: false
        );
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)]
        );

        Places(compilerAlone).ShouldBe([$"CS0436 {GeneratorHarness.SourcePath}{span}"]);
        build.ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_AttributeThatSetsTheLocation_HasNoConflictForEitherType()
    {
        const string Source = """
            using SqlSource;
            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            internal partial class Sample { }
            """;

        var compilerAlone = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)],
            packageAnalyzers: false
        );
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)]
        );

        Places(compilerAlone)
            .ShouldBe([
                $"CS0436 {GeneratorHarness.SourcePath}(2,2)-(2,19)",
                $"CS0436 {GeneratorHarness.SourcePath}(2,34)-(2,45)",
            ]);
        build.ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_AttributeThatSetsTheOutput_HasNoConflictForTheEnum()
    {
        const string Source = """
            using SqlSource;
            [SqlSourceGenerate(Output = GeneratorOutput.Sql)]
            internal partial class Sample { }
            """;

        var compilerAlone = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)],
            packageAnalyzers: false
        );
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)]
        );

        Places(compilerAlone)
            .ShouldBe([
                $"CS0436 {GeneratorHarness.SourcePath}(2,2)-(2,19)",
                $"CS0436 {GeneratorHarness.SourcePath}(2,29)-(2,44)",
            ]);
        build.ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_AttributeThatSetsTheModelsAndTheMethods_HasNoConflictForTheEnums()
    {
        const string Source = """
            using SqlSource;
            [SqlSourceGenerate(
                InputModelType = GeneratorModelType.Class,
                CollectionType = GeneratorCollectionType.List,
                MethodLocation = MethodLocation.Internal)]
            internal partial class Sample { }
            """;

        var compilerAlone = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)],
            packageAnalyzers: false
        );
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)]
        );

        Places(compilerAlone)
            .ShouldBe([
                $"CS0436 {GeneratorHarness.SourcePath}(2,2)-(2,19)",
                $"CS0436 {GeneratorHarness.SourcePath}(3,22)-(3,40)",
                $"CS0436 {GeneratorHarness.SourcePath}(4,22)-(4,45)",
                $"CS0436 {GeneratorHarness.SourcePath}(5,22)-(5,36)",
            ]);
        build.ShouldBeEmpty();
    }

    // The case that fails a build: a project that treats warnings as errors.
    [Fact]
    public async Task Build_WarningsAsErrors_HasNoConflictForAGeneratedType()
    {
        const string Source = "using SqlSource;\n[SqlSourceGenerate]\ninternal partial class Sample { }";

        var compilerAlone = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)],
            warningsAsErrors: true,
            packageAnalyzers: false
        );
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            Source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)],
            warningsAsErrors: true
        );

        Places(compilerAlone).ShouldBe([$"CS0436 {GeneratorHarness.SourcePath}(2,2)-(2,19)"]);
        build.ShouldBeEmpty();
    }

    // The compiler's error is the whole story: the conflict is not reported next to it.
    [Theory]
    [InlineData("[SqlSourceGenerate(1)]", "CS1729 /app/Repo/Sample.cs(2,2)-(2,22)")]
    [InlineData("[SqlSourceGenerate(Missing = 1)]", "CS0246 /app/Repo/Sample.cs(2,20)-(2,27)")]
    public async Task Build_AttributeThatDoesNotCompile_HasItsErrorAndNoConflict(string attribute, string error)
    {
        var source = $"using SqlSource;\n{attribute}\ninternal partial class Sample {{ }}";

        var compilerAlone = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)],
            packageAnalyzers: false
        );
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Sql,
            source,
            [GeneratorHarness.UsersFile],
            [Other(OutputKind.Sql)]
        );

        Places(compilerAlone).ShouldBe([$"CS0436 {GeneratorHarness.SourcePath}(2,2)-(2,19)", error], true);
        Places(build).ShouldBe([error]);
    }

    // A type that the project and the other one both declare by hand is a conflict that the user has to know about,
    // whatever its name.
    [Theory]
    [InlineData("Mine", "SqlLocation")]
    [InlineData("Mine", "SqlSourceGenerateAttribute")]
    [InlineData("Mine", "GeneratorOutput")]
    [InlineData("SqlSource", "Shared")]
    [InlineData("SqlSource.Inner", "SqlLocation")]
    public async Task Build_UseOfATypeThatBothProjectsDeclare_IsStillAConflict(string @namespace, string name)
    {
        var declaration = $"namespace {@namespace} {{ internal class {name} {{ }} }}\n";
        var other = GeneratorHarness.CreateReference(OutputKind.Sql, "Other", InternalsVisible + declaration);
        var source = declaration + $"internal class Sample {{ object o = typeof({@namespace}.{name}); }}";

        var build = await GeneratorHarness.BuildAsync(OutputKind.Sql, source, [], [other]);

        Places(build).ShouldHaveSingleItem().ShouldStartWith("CS0436 ");
    }

    // Both projects generate App.SqlSourceRowBuffer, as a type private to the methods file, so that neither sees the
    // other's.  A buffer that other code could name would be a CS0436 here, which the package does not hide for it.
    // The queries have different names, so that their models are not a conflict of their own.  What is left is the
    // attribute's, which the package hides.
    [Fact]
    public async Task Build_TwoProjectsSharingInternalsWithPooledMethods_HaveNoConflict()
    {
        const string Source =
            "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate]\ninternal partial class Sample;\n";
        const string OtherSource =
            "using SqlSource;\n"
            + InternalsVisible
            + "namespace App { [SqlSourceGenerate] public partial class Sample; }";
        var otherFile = new SqlFile("/other/Users.sql", "-- name: ListUsers\nSELECT id, name FROM users;\n");
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListOthers\nSELECT id, name FROM users;\n");
        var columns = new[] { Column("id"), Column("name", Text, nullable: true) };
        var (reference, sources) = GeneratorHarness.CreateProject(
            OutputKind.CodeGen,
            "Other",
            OtherSource,
            [otherFile, Sidecars.Of(otherFile, Entry(otherFile, "ListUsers", [], columns))],
            [NpgsqlReferences.Npgsql10],
            "postgres"
        );
        var entry = Entry(file, "ListOthers", [], columns);

        var build = await GeneratorHarness.BuildAsync(
            OutputKind.CodeGen,
            Source,
            [file, Sidecars.Of(file, entry)],
            [reference, NpgsqlReferences.Npgsql10],
            packageAnalyzers: false,
            dialect: "postgres"
        );

        Places(build).ShouldBe([$"CS0436 {GeneratorHarness.SourcePath}(5,2)-(5,19)"]);
        sources[MethodRuns.MethodsFile].ShouldContain("file struct SqlSourceRowBuffer<T>\n");
        MethodRuns
            .Run(file, [entry], Source)
            .Sources[MethodRuns.MethodsFile]
            .ShouldContain("file struct SqlSourceRowBuffer<T>\n");
    }

    // The other project, compiled with the generator: it holds the attribute and the enums too.
    private static MetadataReference Other(OutputKind? output) =>
        GeneratorHarness.CreateReference(output, "Other", InternalsVisible + "public class InOther { }\n");

    // Each diagnostic without its message, which for CS0436 names files and assemblies.
    private static IEnumerable<string> Places(IEnumerable<string> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic[..diagnostic.IndexOf(": ", System.StringComparison.Ordinal)]);
}
