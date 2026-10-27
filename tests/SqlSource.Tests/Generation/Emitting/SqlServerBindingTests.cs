using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Types.Maps;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class SqlServerBindingTests
{
    // The binding by value alone of a parameter called name, nullable, that has no described type the binding can use.
    private const string ByValue = """
            var p0 = command.CreateParameter();
            p0.ParameterName = "name";
            p0.Value = (object?)parameters.Name ?? global::System.DBNull.Value;
            command.Parameters.Add(p0);

        """;

    [Fact]
    public void Append_NullableNVarChar_WritesTypeSizeAndTheOverLongRule() =>
        Write(
                new MethodParameter(
                    "name",
                    "Name",
                    "string",
                    false,
                    true,
                    Described: TestSidecars.SqlServer("nvarchar(50)", 100, 0)
                )
            )
            .ShouldBe(
                """
                    var p0 = (global::Microsoft.Data.SqlClient.SqlParameter)command.CreateParameter();
                    p0.ParameterName = "name";
                    p0.SqlDbType = global::System.Data.SqlDbType.NVarChar;
                    p0.Size = 50;
                    p0.Value = (object?)parameters.Name ?? global::System.DBNull.Value;
                    if (p0.Value is string { Length: > 50 })
                    {
                        p0.Size = -1;
                    }
                    command.Parameters.Add(p0);

                """
            );

    // The base name is matched ignoring case, as the server matches it.
    [Theory]
    [InlineData("char(5)", "Char")]
    [InlineData("varchar(5)", "VarChar")]
    [InlineData("nchar(5)", "NChar")]
    [InlineData("nvarchar(5)", "NVarChar")]
    [InlineData("NVarChar(5)", "NVarChar")]
    [InlineData("sysname", "NVarChar")]
    [InlineData("binary(5)", "Binary")]
    [InlineData("varbinary(5)", "VarBinary")]
    [InlineData("decimal(10,2)", "Decimal")]
    [InlineData("numeric(10,2)", "Decimal")]
    [InlineData("datetime2(7)", "DateTime2")]
    [InlineData("time(7)", "Time")]
    [InlineData("datetimeoffset(7)", "DateTimeOffset")]
    [InlineData("json", "NVarChar")]
    [InlineData("timestamp", "Timestamp")]
    [InlineData("rowversion", "Timestamp")]
    [InlineData("bigint", "BigInt")]
    [InlineData("int", "Int")]
    [InlineData("smallint", "SmallInt")]
    [InlineData("tinyint", "TinyInt")]
    [InlineData("bit", "Bit")]
    [InlineData("float", "Float")]
    [InlineData("real", "Real")]
    [InlineData("money", "Money")]
    [InlineData("smallmoney", "SmallMoney")]
    [InlineData("date", "Date")]
    [InlineData("datetime", "DateTime")]
    [InlineData("smalldatetime", "SmallDateTime")]
    [InlineData("uniqueidentifier", "UniqueIdentifier")]
    [InlineData("text", "Text")]
    [InlineData("ntext", "NText")]
    [InlineData("xml", "Xml")]
    [InlineData("image", "Image")]
    [InlineData("sql_variant", "Variant")]
    public void Append_EachBaseType_SetsItsSqlDbType(string name, string sqlDbType) =>
        Write(Described(TestSidecars.SqlServer(name, 10, 10, 2)))
            .ShouldContain("    p0.SqlDbType = global::System.Data.SqlDbType." + sqlDbType + ";\n");

    // Every SQL Server type the map reads, but vector, which SqlClient binds by no SqlDbType, has one that SqlClient
    // has.
    [Fact]
    public void SqlDbTypeOf_EveryKeyOfTheMap_IsAMemberOfSqlDbType()
    {
        foreach (var key in SqlServerTypeMap.Keys.Where(key => key != "vector"))
        {
            var sqlDbType = SqlServerBinding.SqlDbTypeOf(key);
            _ = sqlDbType.ShouldNotBeNull(key);
            Enum.TryParse<SqlDbType>(sqlDbType, out _).ShouldBeTrue(key);
        }

        SqlServerBinding.SqlDbTypeOf("vector").ShouldBeNull();
    }

    // SqlClient refuses a value of a CLR type without its UdtTypeName, so the type is a Udt of its name, with no
    // facet, and a null is DBNull.Value, which the server takes for the type.
    [Fact]
    public void Append_NullableHierarchyId_SetsUdtAndItsTypeName() =>
        Write(
                new MethodParameter(
                    "node",
                    "Node",
                    "global::Microsoft.SqlServer.Types.SqlHierarchyId",
                    true,
                    true,
                    Described: TestSidecars.SqlServer(
                        "hierarchyid",
                        892,
                        0,
                        0,
                        SystemClrType("hierarchyid", "SqlHierarchyId")
                    )
                )
            )
            .ShouldBe(
                """
                    var p0 = (global::Microsoft.Data.SqlClient.SqlParameter)command.CreateParameter();
                    p0.ParameterName = "node";
                    p0.SqlDbType = global::System.Data.SqlDbType.Udt;
                    p0.UdtTypeName = "hierarchyid";
                    p0.Value = (object?)parameters.Node ?? global::System.DBNull.Value;
                    command.Parameters.Add(p0);

                """
            );

    // Each CLR type of sys is a Udt of its name, with or without the user type the tool writes for it.
    [Theory]
    [InlineData("hierarchyid", "SqlHierarchyId", true, "hierarchyid")]
    [InlineData("geography", "SqlGeography", true, "geography")]
    [InlineData("geometry", "SqlGeometry", true, "geometry")]
    [InlineData("geometry", "SqlGeometry", false, "geometry")]
    [InlineData("Geography", "SqlGeography", false, "geography")]
    public void Append_SystemClrType_IsAUdtOfItsName(string name, string clrName, bool hasUserType, string udtTypeName)
    {
        var userType = hasUserType ? SystemClrType(udtTypeName, clrName) : null;

        var binding = Write(Described(TestSidecars.SqlServer(name, -1, 0, 0, userType)));

        binding.ShouldContain(
            "    p0.SqlDbType = global::System.Data.SqlDbType.Udt;\n"
                + "    p0.UdtTypeName = \""
                + udtTypeName
                + "\";\n"
                + "    p0.Value = "
        );
        binding.ShouldNotContain("Size");
    }

    // A CLR type of a user is a Udt of schema.name, a part in brackets when it holds more than letters, digits and
    // underscores, and never a row of the system type its name shares: dbo.Money is no money.
    [Theory]
    [InlineData("dbo", "Money", "dbo.Money")]
    [InlineData("app", "Point_2", "app.Point_2")]
    [InlineData("my schema", "Odd.Type]", "[my schema].[Odd.Type]]]")]
    [InlineData(null, "Money", "Money")]
    public void Append_ClrTypeOfAUser_IsAUdtOfItsSchemaAndName(string? schema, string name, string udtTypeName)
    {
        var userType = new SqlServerUserType(schema, name, "App.Types." + name + ", App.Types");

        var binding = Write(Described(TestSidecars.SqlServer(name, -1, 0, 0, userType)));

        binding.ShouldContain(
            "    p0.SqlDbType = global::System.Data.SqlDbType.Udt;\n"
                + "    p0.UdtTypeName = "
                + "\""
                + udtTypeName
                + "\";\n"
                + "    p0.Value = (object?)parameters.Name ?? global::System.DBNull.Value;\n"
                + "    command.Parameters.Add(p0);\n"
        );
        binding.ShouldNotContain("SqlDbType.Money");
    }

    // A converter's result is what is sent as the CLR type, and a null never reaches the converter.
    [Fact]
    public void Append_ConvertedClrType_SendsTheConvertedValueAsTheUdt()
    {
        var write = new Conversion(
            "global::App.Paths.ToDatabase",
            "global::Microsoft.SqlServer.Types.SqlHierarchyId",
            true,
            false,
            null,
            false
        );

        Write(
                new MethodParameter(
                    "path",
                    "Path",
                    "global::App.Path",
                    false,
                    true,
                    write,
                    TestSidecars.SqlServer("hierarchyid", 892, 0, 0, SystemClrType("hierarchyid", "SqlHierarchyId"))
                )
            )
            .ShouldContain(
                "    p0.UdtTypeName = \"hierarchyid\";\n"
                    + "    p0.Value = parameters.Path is { } value ? (object)global::App.Paths.ToDatabase(value) : "
                    + "global::System.DBNull.Value;\n"
                    + "    command.Parameters.Add(p0);\n"
            );
    }

    [Theory]
    [InlineData("decimal(12,4)")]
    [InlineData("numeric(12,4)")]
    public void Append_Decimal_SetsPrecisionAndScale(string name)
    {
        var binding = Write(Described(TestSidecars.SqlServer(name, 9, 12, 4)));

        binding.ShouldContain("    p0.Precision = 12;\n    p0.Scale = 4;\n");
        binding.ShouldNotContain("Size");
        binding.ShouldNotContain("Round");
    }

    // SqlClient takes a Scale of 0 as not set and takes the value's, so a decimal of scale 0 is rounded to a whole
    // number, half away from zero, as the server rounds it, and the value sent has scale 0.
    [Theory]
    [InlineData("decimal(12,0)", 12)]
    [InlineData("numeric(5,0)", 5)]
    public void Append_DecimalOfScale0_RoundsTheValueToAWholeNumber(string name, int precision) =>
        Write(Described(TestSidecars.SqlServer(name, 9, precision, 0)))
            .ShouldBe(
                $$"""
                    var p0 = (global::Microsoft.Data.SqlClient.SqlParameter)command.CreateParameter();
                    p0.ParameterName = "name";
                    p0.SqlDbType = global::System.Data.SqlDbType.Decimal;
                    p0.Precision = {{precision.ToString(CultureInfo.InvariantCulture)}};
                    p0.Scale = 0;
                    p0.Value = (object?)parameters.Name ?? global::System.DBNull.Value;
                    if (p0.Value is decimal)
                    {
                        p0.Value = decimal.Round((decimal)p0.Value, global::System.MidpointRounding.AwayFromZero);
                    }
                    command.Parameters.Add(p0);

                """
            );

    // The value sent is tested, so a converted parameter is rounded as converted.
    [Fact]
    public void Append_ConvertedDecimalOfScale0_RoundsTheConvertedValue()
    {
        var write = new Conversion("global::App.Cents.ToDatabase", "decimal", true, false, null, false);

        Write(
                new MethodParameter(
                    "cents",
                    "Cents",
                    "global::App.Cents",
                    false,
                    true,
                    write,
                    TestSidecars.SqlServer("decimal(12,0)", 9, 12, 0)
                )
            )
            .ShouldContain(
                "    p0.Value = parameters.Cents is { } value ? (object)global::App.Cents.ToDatabase(value) : "
                    + "global::System.DBNull.Value;\n"
                    + "    if (p0.Value is decimal)\n"
                    + "    {\n"
                    + "        p0.Value = decimal.Round((decimal)p0.Value, "
                    + "global::System.MidpointRounding.AwayFromZero);\n"
                    + "    }\n"
                    + "    command.Parameters.Add(p0);\n"
            );
    }

    // Scale 0 counts as not set in SqlClient, and one from 1 to 6 makes it truncate the value, so every scale is 7.
    [Theory]
    [InlineData("datetime2(0)", 0)]
    [InlineData("datetime2(3)", 3)]
    [InlineData("time(0)", 0)]
    [InlineData("datetimeoffset(2)", 2)]
    public void Append_DateTime2OfAnyScale_SetsScale7(string name, int scale)
    {
        var binding = Write(Described(TestSidecars.SqlServer(name, 8, 27, scale)));

        binding.ShouldContain("    p0.Scale = 7;\n");
        binding.ShouldNotContain("Precision");
    }

    [Theory]
    [InlineData("varchar(max)", "VarChar")]
    [InlineData("nvarchar(max)", "NVarChar")]
    [InlineData("varbinary(max)", "VarBinary")]
    public void Append_VarcharMax_SetsSizeMinusOneAndNoOverLongRule(string name, string sqlDbType)
    {
        var binding = Write(Described(TestSidecars.SqlServer(name, -1, 0)));

        binding.ShouldContain(
            "    p0.SqlDbType = global::System.Data.SqlDbType." + sqlDbType + ";\n    p0.Size = -1;\n"
        );
        binding.ShouldNotContain("if (");
    }

    // A value longer than the described length of a fixed-length type is bound at max of its variable-length type,
    // since char(max) is no type, so the server sees it whole where SqlClient would cut it.
    [Theory]
    [InlineData("char(5)", 5, "Char", 5, "string", "VarChar")]
    [InlineData("nchar(5)", 10, "NChar", 5, "string", "NVarChar")]
    [InlineData("binary(4)", 4, "Binary", 4, "byte[]", "VarBinary")]
    public void Append_FixedChar_RebindsAnOverLongValueAsVarCharMax(
        string name,
        int maxLength,
        string sqlDbType,
        int size,
        string valueType,
        string wider
    ) =>
        ShouldBindAnOverLongValueAtMax(
            Write(Described(TestSidecars.SqlServer(name, maxLength, 0))),
            sqlDbType,
            size,
            valueType,
            "        p0.SqlDbType = global::System.Data.SqlDbType." + wider + ";\n"
        );

    // A value longer than the described length of a variable-length type is bound at max of the same type.
    [Theory]
    [InlineData("varchar(5)", 5, "VarChar", 5, "string")]
    [InlineData("nvarchar(5)", 10, "NVarChar", 5, "string")]
    [InlineData("varbinary(4)", 4, "VarBinary", 4, "byte[]")]
    public void Append_BoundedVariableLength_BindsAnOverLongValueAtMax(
        string name,
        int maxLength,
        string sqlDbType,
        int size,
        string valueType
    ) =>
        ShouldBindAnOverLongValueAtMax(
            Write(Described(TestSidecars.SqlServer(name, maxLength, 0))),
            sqlDbType,
            size,
            valueType,
            string.Empty
        );

    [Fact]
    public void Append_Json_BindsNVarCharMax()
    {
        var binding = Write(Described(TestSidecars.SqlServer("json", -1, 0)));

        binding.ShouldContain("    p0.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n    p0.Size = -1;\n");
        binding.ShouldNotContain("if (");
    }

    [Theory]
    [InlineData("timestamp")]
    [InlineData("rowversion")]
    public void Append_Timestamp_SetsNoSize(string name)
    {
        var binding = Write(Described(TestSidecars.SqlServer(name, 8, 0)));

        binding.ShouldContain("    p0.SqlDbType = global::System.Data.SqlDbType.Timestamp;\n");
        binding.ShouldNotContain("Size");
        binding.ShouldNotContain("if (");
    }

    // SqlClient takes a vector's type and dimension from its SqlVector<float> value alone, and sends DBNull.Value as
    // nvarchar(4000), so a NULL is the null vector of the described dimension.
    [Fact]
    public void Append_NullableVector_SendsCreateNullOfItsDimension()
    {
        var binding = Write(
            new MethodParameter(
                "embedding",
                "Embedding",
                "global::Microsoft.Data.SqlTypes.SqlVector<float>",
                true,
                true,
                Described: TestSidecars.SqlServer("vector(3)", 20, 0)
            )
        );

        binding.ShouldContain(
            "    p0.Value = (object?)parameters.Embedding ?? "
                + "global::Microsoft.Data.SqlTypes.SqlVector<float>.CreateNull(3);\n"
        );
        binding.ShouldNotContain("SqlDbType");
        binding.ShouldNotContain("Size");
    }

    [Fact]
    public void Append_NotNullVector_SendsTheValue()
    {
        var binding = Write(
            new MethodParameter(
                "embedding",
                "Embedding",
                "global::Microsoft.Data.SqlTypes.SqlVector<float>",
                true,
                false,
                Described: TestSidecars.SqlServer("vector(3)", 20, 0)
            )
        );

        binding.ShouldContain("    p0.Value = parameters.Embedding;\n");
        binding.ShouldNotContain("SqlDbType");
    }

    // A converted vector never gives its converter a null, and sends the null vector of its dimension for one.
    [Fact]
    public void Append_ConvertedNullableVector_SendsCreateNullOfItsDimension()
    {
        var write = new Conversion(
            "global::App.Embeddings.ToDatabase",
            "global::Microsoft.Data.SqlTypes.SqlVector<float>",
            true,
            false,
            null,
            false
        );

        Write(
                new MethodParameter(
                    "embedding",
                    "Embedding",
                    "global::App.Embedding",
                    false,
                    true,
                    write,
                    TestSidecars.SqlServer("vector(3)", 20, 0)
                )
            )
            .ShouldContain(
                "    p0.Value = parameters.Embedding is { } value ? (object)global::App.Embeddings.ToDatabase(value) : "
                    + "global::Microsoft.Data.SqlTypes.SqlVector<float>.CreateNull(3);\n"
            );
    }

    // An alias type's name is its base type's.
    [Fact]
    public void Append_AliasType_BindsAsItsBase()
    {
        var binding = Write(
            Described(TestSidecars.SqlServer("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null)))
        );

        binding.ShouldContain("    p0.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n    p0.Size = 320;\n");
        binding.ShouldContain("    if (p0.Value is string { Length: > 320 })\n");
    }

    // The converter's result is what is sent, so the rule of the over-long value tests it and not the property.
    [Fact]
    public void Append_ConvertedParameter_BindsTheDatabaseTypeAndChecksTheConvertedValue()
    {
        var write = new Conversion("global::App.Email.ToDatabase", "string", false, false, null, false);

        var binding = Write(
            new MethodParameter(
                "email",
                "Email",
                "global::App.Email",
                false,
                false,
                write,
                TestSidecars.SqlServer("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null))
            )
        );

        binding.ShouldContain(
            "    p0.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n"
                + "    p0.Size = 320;\n"
                + "    p0.Value = parameters.Email is { } value ? (object)global::App.Email.ToDatabase(value) : "
                + "global::System.DBNull.Value;\n"
                + "    if (p0.Value is string { Length: > 320 })\n"
        );
    }

    [Fact]
    public void Append_NoDescribedType_BindsByValue() =>
        Write(new MethodParameter("name", "Name", "string", false, true)).ShouldBe(ByValue);

    // A type with no row of the binding, as a type of a newer server, a vector whose dimension cannot be read and
    // facets no SqlParameter can hold, all from a sidecar edited by hand but for the first, is bound by value.
    [Theory]
    [InlineData("newtype", 892, 0, 0)]
    [InlineData("vector", 20, 0, 0)]
    [InlineData("vector(x)", 20, 0, 0)]
    [InlineData("decimal(300,4)", 17, 300, 4)]
    [InlineData("nvarchar(0)", 0, 0, 0)]
    public void Append_TypeWithoutABinding_BindsByValue(string name, int maxLength, int precision, int scale) =>
        Write(Described(TestSidecars.SqlServer(name, maxLength, precision, scale))).ShouldBe(ByValue);

    [Fact]
    public void Append_PostgresType_BindsByValue() => Write(Described(TestSidecars.Postgres("text"))).ShouldBe(ByValue);

    // The binding sets the type and the size, and then, for a value of the C# type longer than the size, these lines
    // of the type and Size -1, just before the parameter is added.
    private static void ShouldBindAnOverLongValueAtMax(
        string binding,
        string sqlDbType,
        int size,
        string valueType,
        string typeLine
    )
    {
        var sizeText = size.ToString(CultureInfo.InvariantCulture);
        binding.ShouldContain(
            "    p0.SqlDbType = global::System.Data.SqlDbType." + sqlDbType + ";\n    p0.Size = " + sizeText + ";\n"
        );
        binding.ShouldContain(
            "    if (p0.Value is "
                + valueType
                + " { Length: > "
                + sizeText
                + " })\n    {\n"
                + typeLine
                + "        p0.Size = -1;\n    }\n    command.Parameters.Add(p0);\n"
        );
    }

    // The user type the tool writes for a CLR type of sys.
    private static SqlServerUserType SystemClrType(string name, string clrName) =>
        new(
            "sys",
            name,
            "Microsoft.SqlServer.Types."
                + clrName
                + ", Microsoft.SqlServer.Types, Version=11.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91"
        );

    private static MethodParameter Described(SidecarType type) =>
        new("name", "Name", "string", false, true, Described: type);

    private static string Write(MethodParameter bound)
    {
        var builder = new StringBuilder();
        ParameterBinder
            .For(DatabaseEngine.SqlServer)
            .Append(builder, "    ", "command", "p0", "parameters", bound, new ConversionNames("value", "e1", "i1"));
        return builder.ToString();
    }
}
