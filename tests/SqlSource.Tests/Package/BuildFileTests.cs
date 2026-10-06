using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Package;

// build/SqlSource.props and build/SqlSource.targets are the MSBuild files that the package puts into every project
// that references it.  The end-to-end tests show them working for this project; these read the files for what this
// project cannot show.
public partial class BuildFileTests
{
    private const string Prefix = "SqlSource";

    // The properties of the SDK that the files read.  Every other property in them belongs to the package.
    private static readonly string[] SdkProperties = ["DefaultItemExcludes", "DefaultExcludesInProjectFolder"];

    private static readonly XDocument Props = Load("SqlSource.props");

    private static readonly XDocument Targets = Load("SqlSource.targets");

    [Fact]
    public void Props_PropertiesOfThePackage_ReachTheCompilerInEveryProject()
    {
        var items = Props.Descendants("CompilerVisibleProperty").ToList();

        items
            .Select(item => item.Attribute("Include").ShouldNotBeNull().Value)
            .ShouldBe(["SqlSourceTokenValidation", "SqlSourceDialect"]);

        // A project that sets SqlSourceIncludeFiles to false lists its own .sql files, and still needs the
        // properties.  So nothing may put a condition on the items.
        items.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    [Fact]
    public void Props_DialectMetadataOfASqlFile_ReachesTheCompilerInEveryProject()
    {
        var item = Props.Descendants("CompilerVisibleItemMetadata").ShouldHaveSingleItem();

        item.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("AdditionalFiles");
        item.Attribute("MetadataName").ShouldNotBeNull().Value.ShouldBe("SqlSourceDialect");
        ConditionsAround(item).ShouldBeEmpty();
    }

    // The compiler reads a property, and the metadata of an item, from a file with one line for each.  A value on a
    // line of its own would arrive empty.  SqlSource.Tests.csproj writes both of its dialects that way, so the
    // end-to-end tests show the trimming at work; this pins that each value has it.
    [Theory]
    [InlineData("SqlSourceTokenValidation")]
    [InlineData("SqlSourceDialect")]
    public void Targets_PropertyOfThePackage_IsTrimmed(string property)
    {
        var element = Targets.Descendants(property).ShouldHaveSingleItem();

        element.Value.ShouldBe($"$({property}.Trim())");
        element
            .Parent.ShouldNotBeNull()
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe($"'$({property})' != ''");
    }

    [Fact]
    public void Targets_DialectMetadataOfEverySqlFile_IsTrimmed()
    {
        var item = Targets.Descendants("AdditionalFiles").ShouldHaveSingleItem();

        item.Attribute("Update").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
        item.Attribute("SqlSourceDialect")
            .ShouldNotBeNull()
            .Value.ShouldBe("$([System.String]::Copy('%(SqlSourceDialect)').Trim())");
        ConditionsAround(item).ShouldBeEmpty();
    }

    [Fact]
    public void Props_SqlFilesOfTheProject_AreLeftOutWhenSqlSourceIncludeFilesIsFalse()
    {
        var item = Props.Descendants("AdditionalFiles").ShouldHaveSingleItem();

        item.Attribute("Condition").ShouldBeNull();
        item.Parent.ShouldNotBeNull()
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'$(SqlSourceIncludeFiles)' != 'false'");
    }

    [Theory]
    [InlineData("SqlSource.props")]
    [InlineData("SqlSource.targets")]
    public void BuildFile_EveryPropertyOfThePackage_StartsWithSqlSource(string file)
    {
        var document = file == "SqlSource.props" ? Props : Targets;

        var names = PropertiesSet(document)
            .Concat(PropertiesShownToTheCompiler(document))
            .Concat(PropertiesRead(document))
            .Except(SdkProperties)
            .Distinct()
            .ToList();

        names.ShouldNotBeEmpty();
        names.ShouldAllBe(name => name.StartsWith(Prefix, StringComparison.Ordinal));
    }

    private static IEnumerable<XAttribute> ConditionsAround(XElement element) =>
        element.AncestorsAndSelf().SelectMany(ancestor => ancestor.Attributes("Condition"));

    private static XDocument Load(string file) => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "build", file));

    private static IEnumerable<string> PropertiesSet(XDocument document) =>
        document.Descendants("PropertyGroup").Elements().Select(element => element.Name.LocalName);

    private static IEnumerable<string> PropertiesShownToTheCompiler(XDocument document) =>
        document
            .Descendants("CompilerVisibleProperty")
            .SelectMany(element => element.Attribute("Include").ShouldNotBeNull().Value.Split(';'))
            .Select(name => name.Trim())
            .Where(name => name.Length > 0);

    // A comment is not read: it names properties in prose, without the $( ) of a reference.
    private static IEnumerable<string> PropertiesRead(XDocument document) =>
        document
            .Descendants()
            .SelectMany(element =>
                element
                    .Attributes()
                    .Select(attribute => attribute.Value)
                    .Concat(element.Nodes().OfType<XText>().Select(text => text.Value))
            )
            .SelectMany(value => PropertyReference().Matches(value))
            .Select(match => match.Groups["name"].Value);

    [GeneratedRegex(@"\$\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex PropertyReference();
}
