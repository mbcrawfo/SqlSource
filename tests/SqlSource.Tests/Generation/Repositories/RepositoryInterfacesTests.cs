using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

public sealed class RepositoryInterfacesTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    public void Read_Value_IsOnUnlessFalse(string? value, bool isOn)
    {
        var options = new TestOptionsProvider(
            value is null ? null : new Dictionary<string, string?> { [RepositoryInterfaces.PropertyName] = value }
        );

        RepositoryInterfaces.Read(options.GlobalOptions).ShouldBe(isOn);
    }
}
