using System;
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
    public void Append_BaseType_BindsTheCanonicalName() =>
        Write(new MethodParameter("user_id", "UserId", "int", true, false, Described: BoundAs("integer")))
            .ShouldBe(
                """
                    var p0 = new global::Npgsql.NpgsqlParameter<int>();
                    p0.ParameterName = "user_id";
                    p0.DataTypeName = "pg_catalog.int4";
                    p0.TypedValue = parameters.UserId;
                    command.Parameters.Add(p0);

                """
            );

    [Fact]
    public void Append_Array_BindsTheArraysOwnName() =>
        DataTypeNameOf(
                TestSidecars.Postgres(
                    name: "integer[]",
                    kind: SidecarValues.Kind.Array,
                    internalName: "_int4",
                    element: TestSidecars.Postgres(),
                    bindName: "integer[]"
                )
            )
            .ShouldBe("    p0.DataTypeName = \"pg_catalog._int4\";\n");

    [Fact]
    public void Append_ArrayOfADomain_BindsTheArraysOwnName() =>
        DataTypeNameOf(
                TestSidecars.Postgres(
                    name: "email[]",
                    kind: SidecarValues.Kind.Array,
                    schema: "public",
                    internalName: "_email",
                    element: Email,
                    bindName: "public.email[]"
                )
            )
            .ShouldBe("    p0.DataTypeName = \"public._email\";\n");

    [Fact]
    public void Append_Domain_BindsItsOwnName() =>
        DataTypeNameOf(Email with { BindName = "public.email" }).ShouldBe("    p0.DataTypeName = \"public.email\";\n");

    // Npgsql refuses a domain over a domain by its own name, but binds its innermost base.
    [Fact]
    public void Append_DomainOverADomain_BindsItsInnermostBase() =>
        DataTypeNameOf(WorkEmail with { BindName = "text" }).ShouldBe("    p0.DataTypeName = \"pg_catalog.text\";\n");

    // Npgsql 8 refuses the array of a domain over a domain by its own name.
    [Fact]
    public void Append_ArrayOfADomainOverADomain_BindsItsBindName() =>
        DataTypeNameOf(
                TestSidecars.Postgres(
                    name: "work_email[]",
                    kind: SidecarValues.Kind.Array,
                    schema: "public",
                    internalName: "_work_email",
                    element: WorkEmail,
                    bindName: "text[]"
                )
            )
            .ShouldBe("    p0.DataTypeName = \"text[]\";\n");

    [Fact]
    public void Append_CatalogChar_BindsCatalogChar() =>
        DataTypeNameOf(TestSidecars.Postgres(name: "\"char\"", internalName: "char", bindName: "char"))
            .ShouldBe("    p0.DataTypeName = \"pg_catalog.char\";\n");

    // The bindName differs from the canonical name, pg_catalog.thing, so a kind this version does not know shows which
    // one is bound.
    [Fact]
    public void Append_UnknownKind_BindsItsBindName() =>
        DataTypeNameOf(TestSidecars.Postgres(name: "thing", kind: "shell", internalName: "thing", bindName: "thing"))
            .ShouldBe("    p0.DataTypeName = \"thing\";\n");

    // A dot in the schema or the name would make the canonical name another type's.
    [Theory]
    [InlineData("my.schema", "t")]
    [InlineData("public", "a.b")]
    public void Append_DotInTheName_BindsItsBindName(string schema, string internalName) =>
        DataTypeNameOf(TestSidecars.Postgres(name: "t", schema: schema, internalName: internalName, bindName: "x.y"))
            .ShouldBe("    p0.DataTypeName = \"x.y\";\n");

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
            new MethodParameter(
                "email",
                "Email",
                "string",
                false,
                false,
                Described: Email with
                {
                    BindName = "public.email",
                }
            )
        );

        written.ShouldContain("    var p0 = new global::Npgsql.NpgsqlParameter<string>();\n");
    }

    [Fact]
    public void Append_NoBindName_SetsNoDataTypeName() =>
        Write(new MethodParameter("user_id", "UserId", "int", true, false)).ShouldNotContain("DataTypeName");

    [Fact]
    public void Append_NameWithASpaceAndAQuote_IsWrittenAsALiteral() =>
        DataTypeNameOf(
                TestSidecars.Postgres(
                    name: "\"Odd \"\"name\"\"\"",
                    kind: SidecarValues.Kind.Enum,
                    schema: "public",
                    internalName: "Odd \"name\"",
                    labels: ["a"],
                    bindName: "public.\"Odd \"\"name\"\"\""
                )
            )
            .ShouldBe("    p0.DataTypeName = \"public.Odd \\\"name\\\"\";\n");

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
    [InlineData(false, true, "string", false, "string")]
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

    // The domain public.email over text.
    private static readonly PostgresType Email = TestSidecars.Postgres(
        name: "email",
        kind: SidecarValues.Kind.Domain,
        schema: "public",
        internalName: "email",
        baseType: TestSidecars.Postgres(name: "text", internalName: "text")
    );

    // The domain public.work_email over public.email.
    private static readonly PostgresType WorkEmail = TestSidecars.Postgres(
        name: "work_email",
        kind: SidecarValues.Kind.Domain,
        schema: "public",
        internalName: "work_email",
        baseType: Email
    );

    // A parameter's type as the sidecar describes it, int4 bound by the name.
    private static PostgresType BoundAs(string bindName) => TestSidecars.Postgres(bindName: bindName);

    // The line of DataTypeName that a string parameter described as the type is bound with.
    private static string DataTypeNameOf(PostgresType described)
    {
        var written = Write(new MethodParameter("p", "P", "string", false, false, Described: described));
        var start = written.IndexOf("    p0.DataTypeName", StringComparison.Ordinal);
        return start < 0 ? written : written.Substring(start, written.IndexOf('\n', start) - start + 1);
    }

    private static string Write(MethodParameter bound, string value = "value")
    {
        var builder = new StringBuilder();
        ParameterBinder
            .For(DatabaseEngine.PostgreSql)
            .Append(builder, "    ", "command", "p0", "parameters", bound, new ConversionNames(value, "e1", "i1"));
        return builder.ToString();
    }
}
