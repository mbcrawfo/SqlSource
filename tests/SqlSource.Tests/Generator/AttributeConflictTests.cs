using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// A project that sees the internals of another one that uses SqlSource, as a test project sees the project it tests.
// Both hold the generated attribute and enum, and the compiler warns (CS0436) at every use of either name in this
// project's own code.  The package turns that warning off, for those two types only.
public class AttributeConflictTests
{
    private const string InternalsVisible =
        "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestAssembly\")]\n";

    private static readonly SqlFile Users = new("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");

    [Theory]
    [InlineData("using SqlSource;\n[SqlSourceGenerate]\ninternal partial class Sample { }", "(2,2)-(2,19)")]
    [InlineData("using SqlSource;\n[SqlSourceGenerateAttribute]\ninternal partial class Sample { }", "(2,2)-(2,28)")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal partial class Sample { }", "(1,12)-(1,29)")]
    [InlineData("[global::SqlSource.SqlSourceGenerate()]\ninternal partial class Sample { }", "(1,20)-(1,37)")]
    [InlineData("internal class Sample { object o = typeof(SqlSource.SqlQueriesMode); }", "(1,53)-(1,67)")]
    [InlineData("internal class Sample { object o = SqlSource.SqlQueriesMode.Direct; }", "(1,36)-(1,60)")]
    public async Task Build_UseOfAGeneratedType_IsAConflictOnlyWithoutThePackageAnalyzers(string source, string span)
    {
        var compilerAlone = await GeneratorHarness.BuildAsync(source, [Users], [Other()], packageAnalyzers: false);
        var build = await GeneratorHarness.BuildAsync(source, [Users], [Other()]);

        Places(compilerAlone).ShouldBe([$"CS0436 {GeneratorHarness.SourcePath}{span}"]);
        build.ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_AttributeThatSetsTheMode_HasNoConflictForEitherType()
    {
        const string Source = """
            using SqlSource;
            [SqlSourceGenerate(Mode = SqlQueriesMode.Direct)]
            internal partial class Sample { }
            """;

        var compilerAlone = await GeneratorHarness.BuildAsync(Source, [Users], [Other()], packageAnalyzers: false);
        var build = await GeneratorHarness.BuildAsync(Source, [Users], [Other()]);

        Places(compilerAlone)
            .ShouldBe([
                $"CS0436 {GeneratorHarness.SourcePath}(2,2)-(2,19)",
                $"CS0436 {GeneratorHarness.SourcePath}(2,27)-(2,41)",
            ]);
        build.ShouldBeEmpty();
    }

    // The case that fails a build: a project that treats warnings as errors.
    [Fact]
    public async Task Build_WarningsAsErrors_HasNoConflictForAGeneratedType()
    {
        const string Source = "using SqlSource;\n[SqlSourceGenerate]\ninternal partial class Sample { }";

        var compilerAlone = await GeneratorHarness.BuildAsync(
            Source,
            [Users],
            [Other()],
            warningsAsErrors: true,
            packageAnalyzers: false
        );
        var build = await GeneratorHarness.BuildAsync(Source, [Users], [Other()], warningsAsErrors: true);

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

        var compilerAlone = await GeneratorHarness.BuildAsync(source, [Users], [Other()], packageAnalyzers: false);
        var build = await GeneratorHarness.BuildAsync(source, [Users], [Other()]);

        Places(compilerAlone).ShouldBe([$"CS0436 {GeneratorHarness.SourcePath}(2,2)-(2,19)", error], true);
        Places(build).ShouldBe([error]);
    }

    // A type that the project and the other one both declare by hand is a conflict that the user has to know about,
    // whatever its name.
    [Theory]
    [InlineData("Mine", "SqlQueriesMode")]
    [InlineData("Mine", "SqlSourceGenerateAttribute")]
    [InlineData("SqlSource", "Shared")]
    [InlineData("SqlSource.Inner", "SqlQueriesMode")]
    public async Task Build_UseOfATypeThatBothProjectsDeclare_IsStillAConflict(string @namespace, string name)
    {
        var declaration = $"namespace {@namespace} {{ internal class {name} {{ }} }}\n";
        var other = GeneratorHarness.CreateReference("Other", InternalsVisible + declaration);
        var source = declaration + $"internal class Sample {{ object o = typeof({@namespace}.{name}); }}";

        var build = await GeneratorHarness.BuildAsync(source, [], [other]);

        Places(build).ShouldHaveSingleItem().ShouldStartWith("CS0436 ");
    }

    // The other project, compiled with the generator: it holds the attribute and the enum too.
    private static MetadataReference Other() =>
        GeneratorHarness.CreateReference("Other", InternalsVisible + "public class InOther { }\n");

    // Each diagnostic without its message, which for CS0436 names files and assemblies.
    private static IEnumerable<string> Places(IEnumerable<string> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic[..diagnostic.IndexOf(": ", System.StringComparison.Ordinal)]);
}
