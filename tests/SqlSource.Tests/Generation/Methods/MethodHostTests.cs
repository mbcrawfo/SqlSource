using System;
using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodHostTests
{
    [Theory]
    [InlineData(nameof(MemberLocation.ExtensionClass), true)]
    [InlineData(nameof(MemberLocation.Class), false)]
    [InlineData(nameof(MemberLocation.NestedClass), false)]
    public void IsExtension_EachLocation_IsTheExtensionClassAlone(string locationName, bool isExtension) =>
        new MethodHost(Visibility.Public, Enum.Parse<MemberLocation>(locationName), null).IsExtension.ShouldBe(
            isExtension
        );

    [Fact]
    public void Default_IsAPublicMethodOfTheExtensionClass() =>
        default(MethodHost).ShouldBe(new MethodHost(Visibility.Public, MemberLocation.ExtensionClass, null));
}
