using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Repository;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using SqlSource.Tests.Generation.Emitting.QueryMethods;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class RowBufferTests
{
    // QueryMethodWriterTests' GetUser, reading many rows into an array.
    private static readonly QueryMethod ListUsers = QueryMethodWriterTests.GetUser with
    {
        Shape = ResultShape.Many,
        Collection = CollectionKind.Array,
    };

    // The hand-written copy of the buffer that the allocation tests of tests/DatabaseTests/QueryMethodAllocation read
    // their rows into, and its declaration, in place of the generated one's.
    private static readonly string HandWrittenCopy = RepositoryRoot.Path(
        "tests",
        "DatabaseTests",
        "QueryMethodAllocation",
        "RowBufferByHand.cs"
    );

    private const string HandWrittenDeclaration = "internal struct RowBufferByHand<T>\n";

    private static readonly HashSet<CollectionKind> Pooled =
    [
        CollectionKind.Array,
        CollectionKind.IEnumerable,
        CollectionKind.IReadOnlyCollection,
        CollectionKind.IReadOnlyList,
        CollectionKind.List,
        CollectionKind.IList,
        CollectionKind.ICollection,
        CollectionKind.ImmutableArray,
    ];

    [Fact]
    public void IsUsedBy_EachShapeCollectionAndFlag_IsTrueOnlyForAPooledManyOfAllButTheImmutableLists()
    {
        foreach (var shape in Enum.GetValues<ResultShape>())
        {
            foreach (var collection in Enum.GetValues<CollectionKind>())
            {
                foreach (var useArrayPool in new[] { true, false })
                {
                    var method = ListUsers with { Shape = shape, Collection = collection, UseArrayPool = useArrayPool };
                    var expected = shape == ResultShape.Many && useArrayPool && Pooled.Contains(collection);

                    RowBuffer
                        .IsUsedBy(method)
                        .ShouldBe(expected, shape + ", " + collection + ", UseArrayPool = " + useArrayPool);
                }
            }
        }
    }

    // A streaming method yields each row as it reads it, so it has nothing to buffer.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsUsedBy_ManyOfIAsyncEnumerable_IsFalse(bool useArrayPool) =>
        RowBuffer
            .IsUsedBy(
                ListUsers with
                {
                    Shape = ResultShape.Many,
                    Collection = CollectionKind.IAsyncEnumerable,
                    UseArrayPool = useArrayPool,
                }
            )
            .ShouldBeFalse();

    // The project's one buffer, in a file of its own: Roslyn's EmbeddedAttribute keeps it from any other assembly that
    // sees this one's internals, so two projects that each have it do not clash.
    [Fact]
    public void Source_DeclaresAnEmbeddedInternalStructInNamespaceSqlSource()
    {
        RowBuffer.Source.ShouldBe(GeneratedFile.RowBuffer);
        RowBuffer.Source.ShouldStartWith(GeneratedText.Header + "\nnamespace SqlSource\n{\n");
        RowBuffer.Source.ShouldContain(
            "    [global::Microsoft.CodeAnalysis.Embedded]\n    internal struct SqlSourceRowBuffer<T>\n"
        );
        RowBuffer.Source.ShouldEndWith("    }\n}\n");
        RowBuffer.TypeText.ShouldBe("global::SqlSource.SqlSourceRowBuffer");
        RowBuffer.HintName.ShouldBe("SqlSource-RowBuffer.g.cs");
    }

    // The copy is the generated buffer, from its declaration to its last brace, but its attribute and its name, and at
    // the depth of a file-scoped namespace, so that the hand-written methods of the allocation tests read their rows
    // as the generated methods do.  A change to the generated buffer changes the copy with it.
    [Fact]
    public void Source_HandWrittenCopyOfTheAllocationTests_IsTheGeneratedBufferButItsAttributeNameAndDepth()
    {
        var copy = File.ReadAllText(HandWrittenCopy).ReplaceLineEndings("\n");
        var start = copy.IndexOf(HandWrittenDeclaration, StringComparison.Ordinal);
        var end = copy.IndexOf("\n}\n", start, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        end.ShouldBeGreaterThan(start);

        const string Attribute = "    [global::Microsoft.CodeAnalysis.Embedded]\n";
        const string LastBrace = "\n    }\n";
        var source = RowBuffer.Source;
        var declaration = source.IndexOf(Attribute, StringComparison.Ordinal) + Attribute.Length;
        var lastBrace = source.IndexOf(LastBrace, declaration, StringComparison.Ordinal);
        declaration.ShouldBeGreaterThanOrEqualTo(Attribute.Length);
        lastBrace.ShouldBeGreaterThan(declaration);
        var generated = string.Join(
            "\n",
            source[declaration..(lastBrace + LastBrace.Length)]
                .Split('\n')
                .Select(line =>
                    line.StartsWith(GeneratedText.Indent, StringComparison.Ordinal)
                        ? line[GeneratedText.Indent.Length..]
                        : line
                )
        );

        generated
            .Replace("internal struct SqlSourceRowBuffer<T>\n", HandWrittenDeclaration, StringComparison.Ordinal)
            .ShouldBe(copy[start..(end + 3)]);
    }
}
