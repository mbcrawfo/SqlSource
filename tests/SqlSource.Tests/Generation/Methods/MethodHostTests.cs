using System;
using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodHostTests
{
    [Theory]
    [InlineData(nameof(MethodPlacement.ExtensionClass), nameof(MethodAccessibility.Public), true)]
    [InlineData(nameof(MethodPlacement.Public), nameof(MethodAccessibility.Public), false)]
    [InlineData(nameof(MethodPlacement.Internal), nameof(MethodAccessibility.Internal), false)]
    [InlineData(nameof(MethodPlacement.Private), nameof(MethodAccessibility.Private), false)]
    public void Of_EachPlacement_GivesItsHost(string placementName, string accessibilityName, bool isExtension) =>
        MethodHost
            .Of(Enum.Parse<MethodPlacement>(placementName))
            .ShouldBe(new MethodHost(Enum.Parse<MethodAccessibility>(accessibilityName), isExtension));

    [Fact]
    public void Equality_TwoHostsOfOnePlacement_AreEqual() =>
        MethodHost.Of(MethodPlacement.Internal).ShouldBe(MethodHost.Of(MethodPlacement.Internal));
}
