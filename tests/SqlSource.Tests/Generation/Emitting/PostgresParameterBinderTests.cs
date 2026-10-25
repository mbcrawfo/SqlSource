using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class PostgresParameterBinderTests
{
    // A converter of a value type to a long.
    private static readonly Conversion ToLong = new("global::App.Ids.ToLong", "long", true, false, null, false);

    [Fact]
    public void For_NotNullValueType_BindsATypedParameterByItsBindName() =>
        Write(new MethodParameter("user_id", "UserId", "int", true, false, Described: BoundAs("integer")))
            .ShouldBe(
                """
                    var p0 = new global::Npgsql.NpgsqlParameter<int>();
                    p0.ParameterName = "user_id";
                    p0.DataTypeName = "integer";
                    p0.TypedValue = parameters.UserId;
                    command.Parameters.Add(p0);

                """
            );

    [Fact]
    public void Append_NullableValueType_IsAParameterOfTheNullableType()
    {
        var written = Write(new MethodParameter("user_id", "UserId", "int", true, true, Described: BoundAs("integer")));

        written.ShouldContain("    var p0 = new global::Npgsql.NpgsqlParameter<int?>();\n");
        written.ShouldContain("    p0.TypedValue = parameters.UserId;\n");
    }

    [Fact]
    public void Append_ReferenceType_IsAParameterOfTheType()
    {
        var written = Write(
            new MethodParameter("email", "Email", "string", false, false, Described: BoundAs("public.email"))
        );

        written.ShouldContain("    var p0 = new global::Npgsql.NpgsqlParameter<string>();\n");
        written.ShouldContain("    p0.DataTypeName = \"public.email\";\n");
    }

    [Fact]
    public void Append_NoBindName_SetsNoDataTypeName() =>
        Write(new MethodParameter("user_id", "UserId", "int", true, false)).ShouldNotContain("DataTypeName");

    [Fact]
    public void Append_BindNameWithAQuote_IsWrittenAsALiteral() =>
        Write(
                new MethodParameter(
                    "name",
                    "Name",
                    "string",
                    false,
                    false,
                    Described: BoundAs("public.\"Odd \"\"name\"\"\"")
                )
            )
            .ShouldContain("    p0.DataTypeName = \"public.\\\"Odd \\\"\\\"name\\\"\\\"\\\"\";\n");

    [Fact]
    public void Append_NotNullValueTypeWithAConverter_BindsTheDatabaseType()
    {
        var written = Write(new MethodParameter("id", "Id", "global::App.Id", true, false, ToLong));

        written.ShouldContain("    var p0 = new global::Npgsql.NpgsqlParameter<long>();\n");
        written.ShouldContain("    p0.TypedValue = global::App.Ids.ToLong(parameters.Id);\n");
    }

    // The converter never sees a null, which is bound as a null of the database's type, and the pattern variable has
    // the name the method gave it.
    [Theory]
    [InlineData(true, true, "long", true, "long?")]
    [InlineData(false, false, "long", true, "long?")]
    [InlineData(false, true, "long", true, "long?")]
    [InlineData(true, true, "string", false, "string")]
    [InlineData(false, false, "string", false, "string")]
    public void Append_NullableOrReferencePropertyWithAConverter_BindsNullForNull(
        bool isValueType,
        bool isNullable,
        string databaseText,
        bool databaseIsValueType,
        string bound
    )
    {
        var write = new Conversion("global::App.Ids.ToLong", databaseText, databaseIsValueType, false, null, false);

        var written = Write(new MethodParameter("id", "Id", "global::App.Id", isValueType, isNullable, write), "v1");

        written.ShouldContain("    var p0 = new global::Npgsql.NpgsqlParameter<" + bound + ">();\n");
        written.ShouldContain("    p0.TypedValue = parameters.Id is { } v1 ? global::App.Ids.ToLong(v1) : null;\n");
    }

    // An array is converted element by element into the database's array, by a static lambda whose names are the
    // method's; a null element of a reference type, or of an element written nullable, is bound as a null element.
    [Theory]
    [InlineData(
        "global::App.Id",
        "long[]",
        true,
        true,
        "long[]",
        "global::System.Array.ConvertAll<global::App.Id, long>(v1, static e1 => global::App.Ids.ToDatabase(e1))"
    )]
    [InlineData(
        "global::App.Id",
        "string[]",
        false,
        false,
        "string?[]",
        "global::System.Array.ConvertAll<global::App.Id?, string?>(v1, "
            + "static e1 => e1 is { } i1 ? global::App.Ids.ToDatabase(i1) : null)"
    )]
    [InlineData(
        "global::App.Id?",
        "long[]",
        true,
        true,
        "long?[]",
        "global::System.Array.ConvertAll<global::App.Id?, long?>(v1, "
            + "static e1 => e1 is { } i1 ? global::App.Ids.ToDatabase(i1) : null)"
    )]
    public void Append_ArrayConvertedByElement_BindsTheDatabaseArray(
        string elementText,
        string databaseText,
        bool databaseIsValueType,
        bool elementIsValueType,
        string bound,
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

        var written = Write(new MethodParameter("ids", "Ids", elementText + "[]", false, false, write), "v1");

        written.ShouldContain("    var p0 = new global::Npgsql.NpgsqlParameter<" + bound + ">();\n");
        written.ShouldContain("    p0.TypedValue = parameters.Ids is { } v1 ? " + converted + " : null;\n");
    }

    // A parameter's type as the sidecar describes it, bound by the name.
    private static PostgresType BoundAs(string bindName) => TestSidecars.Postgres(bindName: bindName);

    private static string Write(MethodParameter bound, string value = "value")
    {
        var builder = new StringBuilder();
        ParameterBinder
            .For(DatabaseEngine.PostgreSql)
            .Append(builder, "    ", "command", "p0", "parameters", bound, new ConversionNames(value, "e1", "i1"));
        return builder.ToString();
    }
}
