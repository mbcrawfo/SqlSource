using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Shouldly;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The kinds of model that the generator writes: a record or a class, sealed or not.
internal enum ModelShape
{
    Record,
    SealedRecord,
    Class,
    SealedClass,
}

// What the tests of the models read of them by reflection, in one place for every project: the public properties in the
// order they are declared, each with its type and whether it can be null, the shape of a model and the column that a
// property stands for.  It uses nothing but the framework, so a project on any driver can use it.
internal static class ModelReflection
{
    // The model's public properties in the order they are declared, which is the order of its columns.  The order of
    // GetProperties is not promised, so it is that of the metadata.
    public static IReadOnlyList<PropertyInfo> PropertiesOf(Type model) =>
        [
            .. model
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(static property => property.MetadataToken),
        ];

    // The model's properties in the order they are declared, each with its type and whether it can be null.
    public static (string Name, Type Type, bool Nullable)[] TypedPropertiesOf(Type model) =>
        [
            .. PropertiesOf(model)
                .Select(static property => (property.Name, property.PropertyType, IsNullable(property))),
        ];

    // The names of the model's properties in the order they are declared.
    public static string[] NamesOf(Type model) => [.. PropertiesOf(model).Select(static property => property.Name)];

    // Whether the property can hold null: a Nullable<T>, or a reference type annotated nullable.
    public static bool IsNullable(PropertyInfo property) =>
        property.PropertyType.IsValueType
            ? Nullable.GetUnderlyingType(property.PropertyType) is not null
            : new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;

    // A record has the compiler's clone method, which a class has not.
    public static ModelShape ShapeOf(Type model) =>
        (model.GetMethod("<Clone>$") is not null, model.IsSealed) switch
        {
            (true, false) => ModelShape.Record,
            (true, true) => ModelShape.SealedRecord,
            (false, false) => ModelShape.Class,
            (false, true) => ModelShape.SealedClass,
        };

    // The type of the assembly with the full name, or null when the generator wrote none of that name.
    public static Type? ModelNamed(Assembly assembly, string fullName) => assembly.GetType(fullName);

    // Fails unless the model has the full name, the shape and exactly these properties in this order.
    public static void ShouldBeModel(
        Type model,
        string fullName,
        ModelShape shape,
        params (string Name, Type Type, bool Nullable)[] properties
    )
    {
        model.FullName.ShouldBe(fullName);
        ShapeOf(model).ShouldBe(shape);
        TypedPropertiesOf(model).ShouldBe(properties);
    }

    // Fails unless the row and the input model that wrote it have the same properties, and each property of the row the
    // value of the input model's of the same name: an array element by element, any other value by its Equals.
    public static void ShouldHoldTheValuesOf(object row, object arguments)
    {
        var written = PropertiesOf(arguments.GetType()).ToDictionary(static property => property.Name);
        var read = PropertiesOf(row.GetType());
        read.Select(static property => property.Name).ShouldBe(written.Keys, ignoreOrder: true);
        foreach (var property in read)
        {
            var value = property.GetValue(row);
            var expected = written[property.Name].GetValue(arguments);
            if (value is Array array && expected is Array expectedArray)
            {
                array.Cast<object?>().ShouldBe(expectedArray.Cast<object?>(), property.Name);
            }
            else
            {
                value.ShouldBe(expected, property.Name);
            }
        }
    }

    // The column of a property of a model of a table: Int4ArrayValue is int4_array_value.
    public static string ColumnOf(string property) =>
        string.Concat(
            property.Select(
                static (letter, i) =>
                    i > 0 && char.IsUpper(letter)
                        ? "_" + char.ToLowerInvariant(letter)
                        : char.ToLowerInvariant(letter).ToString(CultureInfo.InvariantCulture)
            )
        );
}
