using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class SystemSchemasTests
{
    [Theory]
    [InlineData("sys", true)]
    [InlineData("SYS", false)]
    [InlineData("Sys", false)]
    [InlineData("dbo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSqlServer_Schema_IsTheServersOwnSpellingAlone(string? schema, bool expected) =>
        SystemSchemas.IsSqlServer(schema).ShouldBe(expected);
}
