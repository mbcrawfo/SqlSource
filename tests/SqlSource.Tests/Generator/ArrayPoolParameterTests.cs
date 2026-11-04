// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The generator parameter no-array-pool: read at every level a list is, the nearest list replacing the farther ones
// whole, and carried to the query's method as QueryMethod.UseArrayPool.  A many method that returns an array, a list,
// an immutable array or one of their interfaces reads its rows into a pooled buffer without it and into a list or a
// builder with it; every other method is written the same with it and without it.
public sealed class ArrayPoolParameterTests
{
    private const string On = "no-array-pool";

    private const string Other = "sort-input";

    private const string Default = "default";

    [Theory]
    // project's file, folder's file, attribute, preamble, query
    [InlineData(null, null, null, null, null, true)]
    [InlineData(On, null, null, null, null, false)]
    [InlineData(On, Default, null, null, null, true)]
    [InlineData(On, Other, null, null, null, true)]
    [InlineData(null, On, null, null, null, false)]
    // Removed with the attribute level in Task 3.
    [InlineData(Other, On, Default, null, null, true)]
    // Removed with the attribute level in Task 3.
    [InlineData(null, null, On, null, null, false)]
    // Removed with the attribute level in Task 3.
    [InlineData(Default, Other, On, null, null, false)]
    // Removed with the attribute level in Task 3.
    [InlineData(On, On, On, Default, null, true)]
    [InlineData(null, null, null, On, null, false)]
    [InlineData(null, null, null, On, Other, true)]
    // Removed with the attribute level in Task 3.
    [InlineData(On, On, On, On, Default, true)]
    [InlineData(null, null, null, Default, On, false)]
    public void Run_NoArrayPool_ComesWholeFromTheMostSpecificLevelThatGivesAList(
        string? projectFile,
        string? folderFile,
        string? attribute,
        string? preamble,
        string? query,
        bool expected
    )
    {
        var settings = new LevelSettings();
        if (folderFile is not null)
        {
            _ = settings.At(
                SettingLevel.FolderFile,
                Setting.GeneratorParameters,
                LevelSettings.WordsAt(SettingLevel.FolderFile, folderFile)
            );
        }

        var sql =
            (preamble is null ? string.Empty : "-- generator: " + preamble + "\n")
            + "-- name: ListUsers\n"
            + (query is null ? string.Empty : "-- generator: " + query + "\n")
            + "SELECT id, name FROM users;\n";

        var run = Run(settings.Files(new SqlFile(GeneratorHarness.UsersPath, sql)), attribute, projectFile);

        run.ShouldBeClean();
        Method(run).UseArrayPool.ShouldBe(expected);
    }

    [Theory]
    [InlineData("ImmutableList")]
    [InlineData("IImmutableList")]
    public void Run_NoArrayPool_LeavesTheGeneratedTextAsItIs(string collection)
    {
        var (pooled, unpooled) = RunBoth(collection);

        unpooled.Sources[MethodRuns.MethodsFile].ShouldBe(pooled.Sources[MethodRuns.MethodsFile]);
        pooled.Sources[MethodRuns.MethodsFile].ShouldNotContain("SqlSourceRowBuffer");
        pooled.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
    }

    [Theory]
    [InlineData("Array", "new global::System.Collections.Generic.List<")]
    [InlineData("IEnumerable", "new global::System.Collections.Generic.List<")]
    [InlineData("IReadOnlyCollection", "new global::System.Collections.Generic.List<")]
    [InlineData("IReadOnlyList", "new global::System.Collections.Generic.List<")]
    [InlineData("List", "new global::System.Collections.Generic.List<")]
    [InlineData("IList", "new global::System.Collections.Generic.List<")]
    [InlineData("ICollection", "new global::System.Collections.Generic.List<")]
    [InlineData("ImmutableArray", "global::System.Collections.Immutable.ImmutableArray.CreateBuilder<")]
    public void Run_NoArrayPool_GivesTheTextOfBeforeThePoolForThePooledTypes(string collection, string buffer)
    {
        var (pooled, unpooled) = RunBoth(collection);

        var unpooledMethods = unpooled.Sources[MethodRuns.MethodsFile];
        unpooledMethods.ShouldNotBe(pooled.Sources[MethodRuns.MethodsFile]);
        unpooledMethods.ShouldContain(buffer);
        unpooledMethods.ShouldNotContain("SqlSourceRowBuffer");
        unpooled.Sources.Keys.ShouldNotContain(RowBuffer.HintName);
        pooled.Sources.Keys.ShouldContain(RowBuffer.HintName);
    }

    // The other shapes read no list of rows, and the parameter leaves their methods as they are too.
    [Theory]
    [InlineData("one", true)]
    [InlineData("one-optional", true)]
    [InlineData("rowcount", false)]
    [InlineData("none", false)]
    public void Run_NoArrayPool_LeavesTheMethodOfEveryOtherShapeAsItIs(string shape, bool hasRows)
    {
        var sql = hasRows ? "SELECT id, name FROM users;\n" : "UPDATE users SET name = name;\n";
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListUsers -> " + shape + "\n" + sql);

        var unpooledFile = file with { Text = "-- generator: no-array-pool\n\n" + file.Text };

        var pooled = Run([file], null, null, hasRows: hasRows);
        var unpooled = Run([unpooledFile], null, null, hasRows: hasRows);

        pooled.ShouldBeClean();
        unpooled.ShouldBeClean();
        Method(unpooled).UseArrayPool.ShouldBeFalse();
        unpooled.Sources[MethodRuns.MethodsFile].ShouldBe(pooled.Sources[MethodRuns.MethodsFile]);
    }

    // Removed with the attribute level in Task 3.
    [Fact]
    public void Run_AttributeWithAMisspeltWordBesideNoArrayPool_IsAnErrorAtTheAttributeAndGivesNoMethods()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListUsers\nSELECT id, name FROM users;\n");

        var run = Run([file], "no-array-pool nope", null);

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC006 /app/Repo/Sample.cs(");
        diagnostic.ShouldEndWith("'nope' is not a valid value of Parameters");
        run.Sources.ShouldNotContainKey(MethodRuns.MethodsFile);
    }

    // ListUsers of the collection type, without and with no-array-pool, each clean.
    private static (GeneratorRun Pooled, GeneratorRun Unpooled) RunBoth(string collection)
    {
        var query = "\n-- name: ListUsers\nSELECT id, name FROM users;\n";
        var collectionType = "-- collection-type: " + collection + "\n";
        var file = new SqlFile(GeneratorHarness.UsersPath, collectionType + query);
        var unpooledFile = file with { Text = collectionType + "-- generator: no-array-pool\n" + query };

        var pooled = Run([file], null, null);
        var unpooled = Run([unpooledFile], null, null);

        pooled.ShouldBeClean();
        unpooled.ShouldBeClean();
        Method(pooled).UseArrayPool.ShouldBeTrue();
        Method(unpooled).UseArrayPool.ShouldBeFalse();
        return (pooled, unpooled);
    }

    // The one method the run planned for App.Sample.
    private static QueryMethod Method(GeneratorRun run) =>
        StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods.ShouldHaveSingleItem();

    // The first of the files, a .sql file, with its sidecar, and the others, at CodeGen under postgres on Npgsql 10, on
    // App.Sample with the attribute's Parameters, and the generator parameters of the project's settings file.  The
    // entry of ListUsers has the rows id and name, or none.
    private static GeneratorRun Run(SqlFile[] files, string? attribute, string? projectFile, bool hasRows = true)
    {
        var file = files[0];
        // Removed with the attribute level in Task 3.
        var source = attribute is null
            ? MethodRuns.Source
            : MethodRuns.SampleSource("Parameters = \"" + attribute + "\"");

        return GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [.. files, Sidecars.Of(file, Entries(file, hasRows))],
            references: [NpgsqlReferences.Npgsql10],
            generatorParameters: projectFile,
            dialect: "postgres"
        );
    }

    // The entry of ListUsers, with the rows id and name, or none.
    private static SidecarEntry[] Entries(SqlFile file, bool hasRows) =>
        [Entry(file, "ListUsers", [], hasRows ? [Column("id"), Column("name", Text, nullable: true)] : null)];
}
