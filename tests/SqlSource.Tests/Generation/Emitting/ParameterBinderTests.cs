using System;
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

    [Theory]
    [InlineData(nameof(DatabaseEngine.PostgreSql))]
    [InlineData(nameof(DatabaseEngine.SqlServer))]
    public void For_EachEngine_WritesTheBindingByValue(string engine) =>
        Write(Enum.Parse<DatabaseEngine>(engine), new MethodParameter("user_id", "UserId", true, false))
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
        foreach (var engine in new[] { DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer })
        {
            Write(engine, new MethodParameter("name", "Name", isValueType, isNullable))
                .ShouldContain("    p0.Value = (object?)parameters.Name ?? global::System.DBNull.Value;\n");
        }
    }

    [Fact]
    public void Append_NotNullValueTypeWithAConverter_SendsTheConvertedValue()
    {
        foreach (var engine in new[] { DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer })
        {
            Write(engine, new MethodParameter("id", "Id", true, false, ToLong))
                .ShouldContain("    p0.Value = global::App.Ids.ToLong(parameters.Id);\n");
        }
    }

    // The converter never sees a null, and the pattern variable has the name the method gave it.
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Append_NullableOrReferencePropertyWithAConverter_SendsDbNullForNull(bool isValueType, bool isNullable)
    {
        foreach (var engine in new[] { DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer })
        {
            Write(engine, new MethodParameter("id", "Id", isValueType, isNullable, ToLong), "v1")
                .ShouldContain(
                    "    p0.Value = parameters.Id is { } v1 ? (object)global::App.Ids.ToLong(v1) : "
                        + "global::System.DBNull.Value;\n"
                );
        }
    }

    private static string Write(DatabaseEngine engine, MethodParameter bound, string value = "value")
    {
        var builder = new StringBuilder();
        ParameterBinder.For(engine).Append(builder, "    ", "command", "p0", "parameters", bound, value);
        return builder.ToString();
    }
}
