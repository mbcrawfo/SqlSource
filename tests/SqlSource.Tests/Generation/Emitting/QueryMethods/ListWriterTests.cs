using Shouldly;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

public sealed class ListWriterTests
{
    [Theory]
    [InlineData(nameof(DatabaseEngine.PostgreSql), 65535)]
    [InlineData(nameof(DatabaseEngine.SqlServer), 2100)]
    public void ParameterLimitOf_EachEngine_IsTheMostParametersItTakes(string engineName, int expected) =>
        ListWriter.ParameterLimitOf(System.Enum.Parse<DatabaseEngine>(engineName)).ShouldBe(expected);
}
