using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using SqlSource.Generation.Types.MappedTypes;
using Xunit;

namespace SqlSource.Tests.Generation.Types.MappedTypes;

public sealed class TransitivePackageAssembliesTests
{
    [Fact]
    public void Of_Null_IsNone()
    {
        var assemblies = TransitivePackageAssemblies.Of(null);

        assemblies.ShouldBe(TransitivePackageAssemblies.None);
        assemblies.Names.Count.ShouldBe(0);
    }

    // A project whose references were not resolved, or that has none that a package alone brings.
    [Fact]
    public void Of_Empty_IsNone()
    {
        var assemblies = TransitivePackageAssemblies.Of("");

        assemblies.ShouldBe(TransitivePackageAssemblies.None);
        assemblies.Names.Count.ShouldBe(0);
    }

    // The targets write a name's #, as the compiler would cut the value there, as %23.  The names are sorted and made
    // distinct ignoring case, the first spelling kept, so that two builds of one list are equal; an empty entry names
    // nothing.
    [Fact]
    public void Of_Entries_AreDecodedSortedAndDistinct() =>
        TransitivePackageAssemblies.Of("Zeta|Odd%23Name|zeta||Alpha").Names.ShouldBe(["Alpha", "Odd#Name", "Zeta"]);

    // A build lists the references in the order it resolves them, which another build of the project may change.
    [Fact]
    public void Of_SameNamesInAnotherOrder_AreEqual() =>
        TransitivePackageAssemblies.Of("B|A|C").ShouldBe(TransitivePackageAssemblies.Of("C|A|B|b"));

    // Each of the four escapes the targets write is decoded, and %2523 is a name's own %23.
    [Theory]
    [InlineData("A%3BB", "A;B")]
    [InlineData("A%7CB", "A|B")]
    [InlineData("A%7cB", "A|B")]
    [InlineData("100%25", "100%")]
    [InlineData("A%2523B", "A%23B")]
    public void Of_EscapedEntry_IsDecoded(string value, string expected) =>
        TransitivePackageAssemblies.Of(value).Names.ShouldBe([expected]);

    // Only the four escapes are decoded; any other % is kept, and nothing the value holds makes the decoding throw.
    [Theory]
    [InlineData("Odd%2", "Odd%2")]
    [InlineData("Odd%", "Odd%")]
    [InlineData("Odd%zz", "Odd%zz")]
    [InlineData("Odd%20Name", "Odd%20Name")]
    public void Of_EntryWithABrokenEscape_IsKeptAsWritten(string value, string expected) =>
        TransitivePackageAssemblies.Of(value).Names.ShouldBe([expected]);

    // An assembly's name is compared ignoring case, as the runtime binds one.
    [Fact]
    public void Contains_IgnoresCase()
    {
        var assemblies = TransitivePackageAssemblies.Of("Microsoft.Identity.Client");

        assemblies.Contains("microsoft.identity.client").ShouldBeTrue();
        assemblies.Contains("Microsoft.Identity").ShouldBeFalse();
    }

    [Theory]
    [InlineData("Alpha")]
    [InlineData("Beta")]
    [InlineData("Gamma")]
    public void Contains_EachName_IsTrue(string name) =>
        TransitivePackageAssemblies.Of("Gamma|Alpha|Beta").Contains(name).ShouldBeTrue();

    [Fact]
    public void Contains_None_IsFalse() => TransitivePackageAssemblies.None.Contains("Alpha").ShouldBeFalse();

    [Fact]
    public void Read_GlobalOption_IsTheProperty() =>
        TransitivePackageAssemblies
            .Read(
                new Options(
                    new Dictionary<string, string>
                    {
                        ["build_property.SqlSourceTransitivePackageAssemblies"] =
                            "Microsoft.Identity.Client|Azure.Core",
                    }
                )
            )
            .Names.ShouldBe(["Azure.Core", "Microsoft.Identity.Client"]);

    [Fact]
    public void Read_NoProperty_IsNone() =>
        TransitivePackageAssemblies.Read(new Options([])).ShouldBe(TransitivePackageAssemblies.None);

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
