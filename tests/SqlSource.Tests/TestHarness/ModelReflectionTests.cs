using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.Tests.TestHarness;

// What the tests of the end-to-end projects read of a model by reflection: its properties in the order they are
// declared, with their types and whether each can hold null, its shape, the column of a property, and whether a row
// holds the values of the input model that wrote it.  The models here are types of this class, so no generator or
// server is needed.
public sealed class ModelReflectionTests
{
    [Fact]
    public void PropertiesOf_ClassDeclaredOutOfAlphabeticalOrder_IsInDeclarationOrder() =>
        ModelReflection
            .PropertiesOf(typeof(OutOfOrder))
            .Select(static property => property.Name)
            .ShouldBe(["Zeta", "Alpha", "Mu"]);

    [Fact]
    public void IsNullable_Property_IsWhetherItCanHoldNull() =>
        ModelReflection
            .PropertiesOf(typeof(Sample))
            .Select(ModelReflection.IsNullable)
            .ShouldBe([false, true, false, true]);

    [Fact]
    public void TypedPropertiesOf_Class_IsEachNameTypeAndNullability() =>
        ModelReflection
            .TypedPropertiesOf(typeof(Sample))
            .ShouldBe([
                ("A", typeof(int), false),
                ("B", typeof(int?), true),
                ("C", typeof(string), false),
                ("D", typeof(string), true),
            ]);

    [Fact]
    public void NamesOf_Class_IsTheNamesInDeclarationOrder() =>
        ModelReflection.NamesOf(typeof(OutOfOrder)).ShouldBe(["Zeta", "Alpha", "Mu"]);

    [Fact]
    public void ShapeOf_EachKind_IsItsShape()
    {
        ModelReflection.ShapeOf(typeof(Shapes.RecordModel)).ShouldBe(ModelShape.Record);
        ModelReflection.ShapeOf(typeof(Shapes.SealedRecordModel)).ShouldBe(ModelShape.SealedRecord);
        ModelReflection.ShapeOf(typeof(Shapes.ClassModel)).ShouldBe(ModelShape.Class);
        ModelReflection.ShapeOf(typeof(Shapes.SealedClassModel)).ShouldBe(ModelShape.SealedClass);
    }

    [Fact]
    public void ModelNamed_TypeThatIsNotThere_IsNull() =>
        ModelReflection
            .ModelNamed(typeof(ModelReflectionTests).Assembly, "SqlSource.Tests.TestHarness.NotThere")
            .ShouldBeNull();

    [Fact]
    public void ModelNamed_TypeThatIsThere_IsTheType() =>
        ModelReflection
            .ModelNamed(typeof(ModelReflectionTests).Assembly, typeof(ModelReflectionTests).FullName!)
            .ShouldBe(typeof(ModelReflectionTests));

    [Theory]
    [InlineData("Int4ArrayValue", "int4_array_value")]
    [InlineData("SqlVariantValue", "sql_variant_value")]
    [InlineData("Id", "id")]
    public void ColumnOf_Property_IsItsSnakeCase(string property, string column) =>
        ModelReflection.ColumnOf(property).ShouldBe(column);

    [Fact]
    public void ShouldBeModel_ModelOfThatNameShapeAndProperties_Passes() =>
        ModelReflection.ShouldBeModel(
            typeof(Shapes.SealedRecordModel),
            typeof(Shapes.SealedRecordModel).FullName!,
            ModelShape.SealedRecord,
            ("Id", typeof(int), false),
            ("Note", typeof(string), true)
        );

    [Fact]
    public void ShouldBeModel_OtherShape_Fails() =>
        _ = Should.Throw<ShouldAssertException>(() =>
            ModelReflection.ShouldBeModel(
                typeof(Shapes.SealedRecordModel),
                typeof(Shapes.SealedRecordModel).FullName!,
                ModelShape.Record,
                ("Id", typeof(int), false),
                ("Note", typeof(string), true)
            )
        );

    [Fact]
    public void ShouldBeModel_OtherProperties_Fails() =>
        _ = Should.Throw<ShouldAssertException>(() =>
            ModelReflection.ShouldBeModel(
                typeof(Shapes.SealedRecordModel),
                typeof(Shapes.SealedRecordModel).FullName!,
                ModelShape.SealedRecord,
                ("Id", typeof(int), false),
                ("Note", typeof(string), false)
            )
        );

    [Fact]
    public void ShouldHoldTheValuesOf_SameValuesAndEqualArrays_Passes() =>
        ModelReflection.ShouldHoldTheValuesOf(new Written(1, [1, 2], null), new Written(1, [1, 2], null));

    [Fact]
    public void ShouldHoldTheValuesOf_OtherValue_Fails() =>
        _ = Should.Throw<ShouldAssertException>(() =>
            ModelReflection.ShouldHoldTheValuesOf(new Written(1, [1, 2], "a"), new Written(1, [1, 2], null))
        );

    [Fact]
    public void ShouldHoldTheValuesOf_OtherArrayElement_Fails() =>
        _ = Should.Throw<ShouldAssertException>(() =>
            ModelReflection.ShouldHoldTheValuesOf(new Written(1, [1, 3], null), new Written(1, [1, 2], null))
        );

    [Fact]
    public void ShouldHoldTheValuesOf_OtherProperties_Fails() =>
        _ = Should.Throw<ShouldAssertException>(() =>
            ModelReflection.ShouldHoldTheValuesOf(new Shapes.SealedRecordModel(1, null), new Written(1, null, null))
        );

    // A model of a row and of the input model that wrote it alike: a value, an array and a reference that can be null.
    private sealed record Written(int Id, int[]? Values, string? Note);

    // The types below are read by reflection alone.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The tests read its properties by reflection."
    )]
    [SuppressMessage(
        "Major Code Smell",
        "S1144:Unused private types or members should be removed",
        Justification = "The tests read its properties by reflection."
    )]
    private sealed class OutOfOrder
    {
        public int Zeta { get; init; }

        public int Alpha { get; init; }

        public int Mu { get; init; }
    }

    // A class with each kind of property a model of the generator has.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The tests read its properties by reflection."
    )]
    [SuppressMessage(
        "Major Code Smell",
        "S1144:Unused private types or members should be removed",
        Justification = "The tests read its properties by reflection."
    )]
    private sealed class Sample
    {
        public int A { get; init; }

        public int? B { get; init; }

        public string C { get; init; } = "";

        public string? D { get; init; }
    }

    // One model of each shape.  The rules about a class that nothing makes are set aside, since the tests read them by
    // reflection alone.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The tests read the shapes by reflection."
    )]
    [SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "One of the shapes is a class that is not sealed."
    )]
    [SuppressMessage(
        "Major Code Smell",
        "S2094:Classes should not be empty",
        Justification = "The tests read the shapes by reflection."
    )]
    [SuppressMessage(
        "Major Code Smell",
        "S3260:Non-derived \"private\" classes and records should be \"sealed\"",
        Justification = "One of the shapes is a class that is not sealed."
    )]
    private static class Shapes
    {
        public record RecordModel(int Id);

        public sealed record SealedRecordModel(int Id, string? Note);

        public class ClassModel;

        public sealed class SealedClassModel;
    }
}
