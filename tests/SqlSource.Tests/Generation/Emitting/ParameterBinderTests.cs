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

    private static string Write(DatabaseEngine engine, MethodParameter bound)
    {
        var builder = new StringBuilder();
        ParameterBinder.For(engine).Append(builder, "    ", "command", "p0", "parameters", bound);
        return builder.ToString();
    }
}
