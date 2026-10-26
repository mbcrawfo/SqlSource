using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
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
    ];

    [Fact]
    public void IsUsedBy_EachShapeCollectionAndFlag_IsTrueOnlyForAPooledManyOfTheFourTypes()
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

    [Fact]
    public void NameFor_ExtensionClassOfATypeNamedLikeTheBuffer_IsTheName() =>
        RowBuffer.NameFor(Methods(MethodPlacement.ExtensionClass, "SqlSourceRowBuffer")).ShouldBe(RowBuffer.Name);

    [Theory]
    [InlineData(nameof(MethodPlacement.Public))]
    [InlineData(nameof(MethodPlacement.Internal))]
    [InlineData(nameof(MethodPlacement.Private))]
    public void NameFor_TypeNamedLikeTheBufferWithTheMethodsOnIt_IsTheOtherName(string placement) =>
        RowBuffer
            .NameFor(Methods(Enum.Parse<MethodPlacement>(placement), "SqlSourceRowBuffer"))
            .ShouldBe(RowBuffer.OtherName);

    [Fact]
    public void NameFor_TypeNamedOtherwiseWithTheMethodsOnIt_IsTheName() =>
        RowBuffer.NameFor(Methods(MethodPlacement.Public, "Sample")).ShouldBe(RowBuffer.Name);

    // Only the outermost type is in the namespace beside the buffer.
    [Fact]
    public void NameFor_NestedTypeNamedLikeTheBuffer_IsTheName() =>
        RowBuffer
            .NameFor(
                Methods(
                    MethodPlacement.Public,
                    "Outer",
                    new TypeDeclaration("class", "SqlSourceRowBuffer", "SqlSourceRowBuffer", string.Empty, 0)
                )
            )
            .ShouldBe(RowBuffer.Name);

    // The value of the name, not its text: @SqlSourceRowBuffer is the same type.
    [Fact]
    public void NameFor_EscapedTypeNamedLikeTheBuffer_IsTheOtherName() =>
        RowBuffer
            .NameFor(
                Methods(MethodPlacement.Public, "SqlSourceRowBuffer") with
                {
                    Types = TestModels.Array(
                        new TypeDeclaration("class", "@SqlSourceRowBuffer", "SqlSourceRowBuffer", "<T>", 1)
                    ),
                }
            )
            .ShouldBe(RowBuffer.OtherName);

    [Theory]
    [InlineData("App", "global::App.SqlSourceRowBuffer")]
    [InlineData("@class.Rows", "global::@class.Rows.SqlSourceRowBuffer")]
    [InlineData("", "global::SqlSourceRowBuffer")]
    public void TypeTextFor_EachNamespace_NamesTheBufferFromGlobal(string namespaceText, string expected) =>
        RowBuffer
            .TypeTextFor(Methods(MethodPlacement.ExtensionClass, "Sample") with { NamespaceText = namespaceText })
            .ShouldBe(expected);

    [Fact]
    public void TypeTextFor_TypeNamedLikeTheBufferWithTheMethodsOnIt_NamesTheOtherName() =>
        RowBuffer
            .TypeTextFor(Methods(MethodPlacement.Public, "SqlSourceRowBuffer"))
            .ShouldBe("global::App.SqlSourceRowBufferOfRows");

    // The methods of App.<name>, and of the types nested in it, under the placement.
    private static TypeMethods Methods(MethodPlacement placement, string name, params TypeDeclaration[] nested) =>
        new(
            "App." + name,
            "App." + name + "-Methods.g.cs",
            placement,
            "App",
            "App",
            TestModels.Array([new TypeDeclaration("class", name, name, string.Empty, 0), .. nested]),
            name + "Extensions",
            true,
            TestModels.AttributeLocation,
            TestModels.Array(ListUsers),
            EquatableArray<DiagnosticInfo>.Empty,
            CodeGenEngines.Postgres,
            EquatableArray<string>.Empty
        );
}
