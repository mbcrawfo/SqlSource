using System;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public class RoslynVersionTests
{
    // This project exists to run the generator on the oldest compiler it supports.  If the pin in
    // Directory.Packages.props is raised, the support floor in README.md moves with it, and this number follows.
    [Fact]
    public void Tests_RunOnTheOldestSupportedRoslyn() =>
        typeof(Compilation).Assembly.GetName().Version.ShouldBe(new Version(4, 8, 0, 0));
}
