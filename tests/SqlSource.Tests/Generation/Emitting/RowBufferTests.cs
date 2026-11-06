using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
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
}
