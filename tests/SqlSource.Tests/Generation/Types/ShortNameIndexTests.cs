using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Types;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generation.Types;

public sealed class ShortNameIndexTests
{
    // The runtime's assemblies, which every compilation references, have none of these names.
    private const string LibrarySource = """
        namespace Lib
        {
            public readonly record struct UserId(long Value);

            public sealed class Crate<T>;

            public class Outer
            {
                public sealed class Nested;
            }

            internal sealed class LibSecret;
        }

        namespace Lib.Deep
        {
            public sealed class Crate;
        }
        """;

    private const string ProjectSource = """
        namespace App
        {
            public readonly record struct UserId(long Value);

            internal sealed class Crate;

            public class Outer
            {
                public sealed class UserId;
            }
        }
        """;

    // The index of a reference is built by the first compilation that asks, and the next compilation that has the
    // same reference reads it again.
    [Fact]
    public void MetadataNamesOf_ReferenceUnchanged_IndexesOnce()
    {
        var library = GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibrarySource);
        var first = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/First.cs", "namespace App;\n\npublic sealed class First;\n")],
            references: [library]
        );
        var second = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/Second.cs", "namespace App;\n\npublic sealed class Second;\n")],
            references: [library]
        );

        ShortNameIndex.MetadataNamesOf(first, "UserId", 0).ShouldBe(["Lib.UserId"]);
        var index = ShortNameIndex.IndexOf(library).ShouldNotBeNull();
        ShortNameIndex.MetadataNamesOf(second, "UserId", 0).ShouldBe(["Lib.UserId"]);

        ShortNameIndex.IndexOf(library).ShouldBeSameAs(index);
    }

    [Fact]
    public void IndexOf_ReferenceNoShortNameAsked_IsNull()
    {
        var library = GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibrarySource);

        ShortNameIndex.IndexOf(library).ShouldBeNull();
    }

    // The project's own and a reference's, each type at the top of its namespace, in ordinal order.
    [Fact]
    public void MetadataNamesOf_NameInTheProjectAndAReference_GivesBoth()
    {
        var compilation = Project();

        ShortNameIndex.MetadataNamesOf(compilation, "UserId", 0).ShouldBe(["App.UserId", "Lib.UserId"]);
    }

    [Fact]
    public void MetadataNamesOf_Arity_GivesTheTypesOfThatArityAlone()
    {
        var compilation = Project();

        ShortNameIndex.MetadataNamesOf(compilation, "Crate", 1).ShouldBe(["Lib.Crate`1"]);
        ShortNameIndex.MetadataNamesOf(compilation, "Crate", 0).ShouldBe(["App.Crate", "Lib.Deep.Crate"]);
    }

    [Fact]
    public void MetadataNamesOf_NestedType_IsNotGiven()
    {
        var compilation = Project();

        ShortNameIndex.MetadataNamesOf(compilation, "Nested", 0).ShouldBeEmpty();
    }

    // A reference's internal type is not accessible to a project that it shows no internals.
    [Fact]
    public void MetadataNamesOf_TypeNotAccessible_IsNotGiven()
    {
        var compilation = Project();

        ShortNameIndex.MetadataNamesOf(compilation, "LibSecret", 0).ShouldBeEmpty();
    }

    [Fact]
    public void MetadataNamesOf_NoTypeOfTheName_IsEmpty()
    {
        var compilation = Project();

        ShortNameIndex.MetadataNamesOf(compilation, "NoTypeHasThisName", 0).ShouldBeEmpty();
    }

    private static CSharpCompilation Project() =>
        GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/App.cs", ProjectSource)],
            references: [GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibrarySource)]
        );
}
