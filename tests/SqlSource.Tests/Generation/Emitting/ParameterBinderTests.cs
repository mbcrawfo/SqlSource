using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class ParameterBinderTests
{
    // A converter of a value type to a long.
    private static readonly Conversion ToLong = new("global::App.Ids.ToLong", "long", true, false, null, false);

    [Fact]
    public void For_SqlServer_WritesTheBindingByValue() =>
        Write(new MethodParameter("user_id", "UserId", "int", true, false))
            .ShouldBe(
                """
                    var p0 = command.CreateParameter();
                    p0.ParameterName = "user_id";
                    p0.Value = parameters.UserId;
                    command.Parameters.Add(p0);

                """
            );

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Append_NullableOrReferenceProperty_SendsDbNullForNull(bool isValueType, bool isNullable)
    {
        Write(new MethodParameter("name", "Name", "string", isValueType, isNullable))
            .ShouldContain("    p0.Value = (object?)parameters.Name ?? global::System.DBNull.Value;\n");
    }

    [Fact]
    public void Append_NotNullValueTypeWithAConverter_SendsTheConvertedValue()
    {
        Write(new MethodParameter("id", "Id", "global::App.Id", true, false, ToLong))
            .ShouldContain("    p0.Value = global::App.Ids.ToLong(parameters.Id);\n");
    }

    // The converter never sees a null, and the pattern variable has the name the method gave it.
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Append_NullableOrReferencePropertyWithAConverter_SendsDbNullForNull(bool isValueType, bool isNullable)
    {
        Write(new MethodParameter("id", "Id", "global::App.Id", isValueType, isNullable, ToLong), "v1")
            .ShouldContain(
                "    p0.Value = parameters.Id is { } v1 ? (object)global::App.Ids.ToLong(v1) : "
                    + "global::System.DBNull.Value;\n"
            );
    }

    // An array is converted element by element into the database's array, by a static lambda whose names are the
    // method's; a null element of a reference type, or of an element written nullable, is sent as a null element.
    [Theory]
    [InlineData(
        "global::App.Id",
        "long[]",
        true,
        true,
        "global::System.Array.ConvertAll<global::App.Id, long>(v1, static e1 => global::App.Ids.ToDatabase(e1))"
    )]
    [InlineData(
        "global::App.Id",
        "string[]",
        false,
        false,
        "global::System.Array.ConvertAll<global::App.Id?, string?>(v1, "
            + "static e1 => e1 is { } i1 ? global::App.Ids.ToDatabase(i1) : null)"
    )]
    [InlineData(
        "global::App.Id?",
        "long[]",
        true,
        true,
        "global::System.Array.ConvertAll<global::App.Id?, long?>(v1, "
            + "static e1 => e1 is { } i1 ? global::App.Ids.ToDatabase(i1) : null)"
    )]
    public void Append_ArrayConvertedByElement_SendsTheConvertedArray(
        string elementText,
        string databaseText,
        bool databaseIsValueType,
        bool elementIsValueType,
        string converted
    )
    {
        var write = new Conversion(
            "global::App.Ids.ToDatabase",
            databaseText,
            databaseIsValueType,
            true,
            elementText,
            elementIsValueType
        );
        Write(new MethodParameter("ids", "Ids", elementText + "[]", false, false, write), "v1")
            .ShouldContain(
                "    p0.Value = parameters.Ids is { } v1 ? (object)" + converted + " : global::System.DBNull.Value;\n"
            );
    }

    private static string Write(MethodParameter bound, string value = "value")
    {
        var builder = new StringBuilder();
        ParameterBinder
            .For(DatabaseEngine.SqlServer)
            .Append(builder, "    ", "command", "p0", "parameters", bound, new ConversionNames(value, "e1", "i1"));
        return builder.ToString();
    }
}
