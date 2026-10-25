// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// A C# type written by its short name in the type override, which the compilation resolves against the project's
// types and its references', and which generated code then writes by its full name.  Each run is at Models under
// postgres on Npgsql 10, with an entry written for each query's current hash.
public sealed class ShortNameTests
{
    private const string Place = "the property SqlSourceTypeOverrides";

    private const string AppUserId = """

        namespace App
        {
            public readonly record struct UserId(long Value);
        }
        """;

    private const string OtherUserId = """

        namespace Other
        {
            public readonly record struct UserId(long Value);
        }
        """;

    private const string LibraryUserId = """
        namespace Lib
        {
            public readonly record struct UserId(long Value);
        }
        """;

    private static readonly PostgresType Int8 = Catalog("int8");

    [Fact]
    public void Run_ShortName_OfTheProject_IsItsFullName()
    {
        var run = RunOf(Source + AppUserId, "int8=UserId", Int8);

        run.ShouldBeClean();
        TextOf(run).ShouldBe("global::App.UserId");
        run.Sources["App.GetUserDto-Model.g.cs"].ShouldContain("global::App.UserId Id");
    }

    [Fact]
    public void Run_ShortName_OfAReference_IsItsFullName()
    {
        var library = GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibraryUserId);

        var run = RunOf(Source, "int8=UserId", Int8, library);

        run.ShouldBeClean();
        TextOf(run).ShouldBe("global::Lib.UserId");
    }

    [Fact]
    public void Run_ShortName_InATypeArgument()
    {
        var run = RunOf(Source + AppUserId, "_int8=System.Collections.Generic.List<UserId>", ArrayOf(Int8, "_int8"));

        run.ShouldBeClean();
        TextOf(run).ShouldBe("global::System.Collections.Generic.List<global::App.UserId>");
    }

    [Fact]
    public void Run_ShortName_TwoCandidates_IsSqlsrc250()
    {
        var run = RunOf(Source + AppUserId + OtherUserId, "int8=UserId", Int8);

        run.Diagnostics.ShouldBe([
            $"SQLSRC250 {GeneratorHarness.UsersPath}(1,10)-(1,17): 'int8' is mapped to 'UserId' for "
                + $"column 'id', in {Place}, and no type of that name is found, or more than one that this project "
                + "can see, and it could be App.UserId or Other.UserId.  Correct the name, with its namespace and "
                + "type arguments, or for a name that two references have, map to a type of this project.",
        ]);
    }

    // The first three candidates are named, and the rest counted.
    [Fact]
    public void Run_ShortName_FiveCandidates_NamesThreeAndCountsTheRest()
    {
        var source = Source;
        foreach (var space in new[] { "A", "B", "C", "D", "E" })
        {
            source += "\n\nnamespace " + space + "\n{\n    public readonly record struct UserId(long Value);\n}\n";
        }

        var run = RunOf(source, "int8=UserId", Int8);

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldContain(
                "more than one that this project can see, and it could be A.UserId, B.UserId, C.UserId or 2 more.  "
            );
    }

    // A type of the global namespace is one more candidate of a short name, not the one it means.
    [Fact]
    public void Resolve_ShortName_GlobalAndNamespacedType_IsAmbiguous()
    {
        var compilation = GeneratorHarness.CreateCompilation([
            new SourceFile("/app/App.cs", "public sealed class UserId;\n" + AppUserId),
        ]);

        var type = Resolve(compilation, "UserId");

        type.Status.ShouldBe(MappedTypeStatus.Ambiguous);
        type.Candidates.ShouldBe(["App.UserId", "global::UserId"]);
        type.Text.ShouldBe("UserId");
    }

    [Fact]
    public void Run_GlobalName_IsTheGlobalNamespaceAlone()
    {
        var run = RunOf(Source + AppUserId, "int8=global::UserId", Int8);

        run.Diagnostics.ShouldBe([
            $"SQLSRC250 {GeneratorHarness.UsersPath}(1,10)-(1,17): 'int8' is mapped to 'global::UserId' "
                + $"for column 'id', in {Place}, and no type of that name is found, or more than one that this project "
                + "can see.  Correct the name, with its namespace and type arguments, or for a name that two "
                + "references have, map to a type of this project.",
        ]);
    }

    // The reference's internal UserId, which it shows the project no internals of, is no candidate beside App's.
    [Fact]
    public void Run_ShortName_NotAccessibleInAReference_IsNotACandidate()
    {
        var library = GeneratorHarness.CreateReference(
            OutputKind.Sql,
            "Lib",
            LibraryUserId.Replace("public readonly", "internal readonly", System.StringComparison.Ordinal)
        );

        var run = RunOf(Source + AppUserId, "int8=UserId", Int8, library);

        run.ShouldBeClean();
        TextOf(run).ShouldBe("global::App.UserId");
    }

    // A file-local type has a name no other file can write, so it is no candidate beside App.UserId.
    [Fact]
    public void Run_ShortName_FileLocalTypeBesideAPublicOne_IsThePublicOne()
    {
        const string FileLocal = """

            namespace Other
            {
                file sealed class UserId;
            }
            """;

        var run = RunOf(Source + AppUserId + FileLocal, "int8=UserId", Int8);

        run.ShouldBeClean();
        TextOf(run).ShouldBe("global::App.UserId");
    }

    [Fact]
    public void Run_ShortName_OnlyAFileLocalType_IsSqlsrc250()
    {
        const string FileLocal = """

            namespace App
            {
                file readonly record struct UserId(long Value);
            }
            """;

        var run = RunOf(Source + FileLocal, "int8=UserId", Int8);

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC250 ");
        run.CompilationErrors.ShouldBeEmpty();
    }

    // A reference that gives the project its internals makes its internal type a candidate.  A model of it is then
    // SQLSRC251 unless the claiming type is internal, which is another rule, so the resolution is asked alone.
    [Fact]
    public void Resolve_ShortName_InternalOfAReferenceThatShowsItsInternals_IsItsFullName()
    {
        var library = GeneratorHarness.CreateReference(
            OutputKind.Sql,
            "Lib",
            "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestAssembly\")]\n"
                + LibraryUserId.Replace("public readonly", "internal readonly", System.StringComparison.Ordinal)
        );
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/App.cs", "namespace App;\n\npublic sealed class Sample;\n")],
            references: [library]
        );

        var type = Resolve(compilation, "UserId");

        type.Status.ShouldBe(MappedTypeStatus.Found);
        type.Text.ShouldBe("global::Lib.UserId");
        type.IsPublic.ShouldBeFalse();
    }

    // The converters of a type named by its short name are found and applied, as for its full name.
    [Fact]
    public void Run_ShortNameWithConverters_AppliesThem()
    {
        const string Converting = """

            namespace App
            {
                public readonly record struct UserId(long Value)
                {
                    [SqlSourceConverter]
                    public static UserId FromDatabase(long value) => new(value);

                    [SqlSourceConverter]
                    public static long ToDatabase(UserId id) => id.Value;
                }
            }
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n");

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetUser", [Parameter("id", Int8)], [Column("id", Int8)])],
            Source + Converting,
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "int8=UserId" }
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("global::App.UserId.FromDatabase(reader.GetFieldValue<long>(0))");
        methods.ShouldContain("global::App.UserId.ToDatabase(");
    }

    [Fact]
    public void Run_ShortName_NestedType_IsNotFound()
    {
        const string Nested = """

            namespace App
            {
                public static class Outer
                {
                    public readonly record struct UserId(long Value);
                }
            }
            """;

        var run = RunOf(Source + Nested, "int8=UserId", Int8);

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC250 ");
    }

    // One assembly referenced twice gives its type's full name once, which then resolves as a full name does.
    [Fact]
    public void Resolve_ShortName_SameFullNameInTwoReferences_IsOneCandidate()
    {
        var image = GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibraryUserId);
        var copy = ((AssemblyMetadata)((PortableExecutableReference)image).GetMetadata()).GetReference();
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/App.cs", "namespace App;\n\npublic sealed class Sample;\n")],
            references: [image, copy]
        );

        var type = Resolve(compilation, "UserId");

        type.Status.ShouldBe(MappedTypeStatus.Found);
        type.Text.ShouldBe("global::Lib.UserId");
        type.Candidates.ShouldBeEmpty();
    }

    // UserId and App.UserId are one type, so two queries that name it each way share one model.
    [Fact]
    public void Run_ShortAndFullName_ShareAModel()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFirst\n-- output-model: Row\n-- type-override: int8 = UserId\nSELECT 1;\n\n"
                + "-- name: GetSecond\n-- output-model: Row\n-- type-override: int8 = App.UserId\nSELECT 2;\n"
        );

        var run = Run(
            Source + AppUserId,
            [
                file,
                Sidecars.Of(
                    file,
                    Entry(file, "GetFirst", [], [Column("id", Int8)]),
                    Entry(file, "GetSecond", [], [Column("id", Int8)])
                ),
            ]
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.Row-Model.g.cs"]);
        run.Sources["App.Row-Model.g.cs"].ShouldContain("global::App.UserId Id");
    }

    // GetUser, with one column id of the type, and the property's type override.
    private static GeneratorRun RunOf(
        string source,
        string typeOverrides,
        PostgresType type,
        MetadataReference? reference = null
    )
    {
        var file = GeneratorHarness.UsersFile;
        return Run(
            source,
            [file, Sidecars.Of(file, Entry(file, "GetUser", [], [Column("id", type)]))],
            new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = typeOverrides },
            reference is null ? null : [NpgsqlReferences.Npgsql10, reference]
        );
    }

    // The type text of GetUser's one column.
    private static string TextOf(GeneratorRun run) =>
        StepOutputs
            .Of<FileTypes>(run, TrackingNames.FileTypes)
            .SelectMany(file => file.Queries)
            .Single()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text;

    // A full name that two references both have is ambiguous by itself, so its argument's short name, ambiguous too,
    // lends it no candidates: they would name types of another part.
    [Fact]
    public void Resolve_AmbiguousFullNameWithAnAmbiguousShortArgument_HasNoCandidates()
    {
        const string Box = "namespace Shared\n{\n    public sealed class Box<T>;\n}\n";
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/App.cs", AppUserId + OtherUserId)],
            references:
            [
                GeneratorHarness.CreateReference(OutputKind.Sql, "First", Box),
                GeneratorHarness.CreateReference(OutputKind.Sql, "Second", Box),
            ]
        );

        var type = Resolve(compilation, "global::Shared.Box<UserId>");

        type.Status.ShouldBe(MappedTypeStatus.Ambiguous);
        type.Candidates.ShouldBeEmpty();
    }

    // The candidates of an ambiguous short argument are the name's, when the type around it is found.
    [Fact]
    public void Resolve_FoundFullNameWithAnAmbiguousShortArgument_HasTheArgumentsCandidates()
    {
        var compilation = GeneratorHarness.CreateCompilation([new SourceFile("/app/App.cs", AppUserId + OtherUserId)]);

        var type = Resolve(compilation, "global::System.Collections.Generic.List<UserId>");

        type.Status.ShouldBe(MappedTypeStatus.Ambiguous);
        type.Candidates.ShouldBe(["App.UserId", "Other.UserId"]);
    }

    private static MappedType Resolve(Compilation compilation, string name) =>
        MappedTypeResolver
            .Resolve(compilation, new EquatableArray<string>([name]), TestContext.Current.CancellationToken)
            .Types.ShouldHaveSingleItem();
}
