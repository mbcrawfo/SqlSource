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
    // line of its own would arrive empty, so the package trims each one.  GenerateMSBuildEditorConfigFileCore is the
    // target of the SDK that writes the file.  A trim that runs before it sees a value wherever it was set, in
    // Directory.Build.targets or by another target, and runs in every build that writes the file, a design-time
    // build too.  A trim outside a target would see only what is set before NuGet imports the file.
    [Fact]
    public void Targets_EveryTrim_RunsBeforeTheBuildWritesTheFileTheCompilerReads()
    {
        var root = Targets.Root.ShouldNotBeNull();

        root.Elements().ShouldAllBe(element => element.Name.LocalName == "Target");
        root.Elements()
            .Select(target => target.Attribute("BeforeTargets")?.Value)
            .ShouldAllBe(before => before == "GenerateMSBuildEditorConfigFileCore");
        root.Elements().SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // SqlSource.Tests.csproj writes its dialect and its setting for token validation on lines of their own, and
    // tools/package-install sets one in Directory.Build.targets, so the end-to-end tests and the check of the
    // installed package show the trimming at work; this pins that each property has it.
    [Theory]
    [InlineData("SqlSourceTokenValidation")]
    [InlineData("SqlSourceDialect")]
    public void Targets_PropertyOfThePackage_IsTrimmed(string property)
    {
        // The metadata of an item has the name of the property, so only a property group is looked at.
        var element = Targets.Descendants("PropertyGroup").Elements(property).ShouldHaveSingleItem();

        element.Value.ShouldBe($"$({property}.Trim())");
        ConditionsAround(element).ShouldBeEmpty();
    }

    // The target runs once for each value that the metadata has, and only the items with that value are in reach of
    // it.  The value goes through a property so that it is never text inside a property function: see
    // Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction.
    [Fact]
    public void Targets_DialectMetadataOfEverySqlFile_IsTrimmed()
    {
        var item = Targets.Descendants("AdditionalFiles").ShouldHaveSingleItem();
        var target = item.Ancestors("Target").ShouldHaveSingleItem();

        target.Attribute("Outputs").ShouldNotBeNull().Value.ShouldBe("%(AdditionalFiles.SqlSourceDialect)");
        target
            .Descendants("SqlSourceDialectAsWritten")
            .ShouldHaveSingleItem()
            .Value.ShouldBe("%(AdditionalFiles.SqlSourceDialect)");
        item.Attributes().ShouldBeEmpty();
        // Most files have no metadata, and they are one batch with nothing to trim.  It is not written back to them.
        item.Parent.ShouldNotBeNull()
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'$(SqlSourceDialectAsWritten)' != ''");
        item.Elements().ShouldHaveSingleItem().Name.LocalName.ShouldBe("SqlSourceDialect");
        item.Elements().ShouldHaveSingleItem().Value.ShouldBe("$(SqlSourceDialectAsWritten.Trim())");
    }

    // MSBuild puts metadata into an expression as text, before it reads the expression.  A value with a quote in it
    // would end a quoted argument early, and the whole expression would reach the compiler as the value: SQLSRC011
    // would then quote the expression and not what the project says.
    [Fact]
    public void Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction() =>
        Values(Targets).ShouldAllBe(value => !MetadataInAPropertyFunction().IsMatch(value));

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
        Values(document)
            .SelectMany(value => PropertyReference().Matches(value))
            .Select(match => match.Groups["name"].Value);

    private static IEnumerable<string> Values(XDocument document) =>
        document
            .Descendants()
            .SelectMany(element =>
                element
                    .Attributes()
                    .Select(attribute => attribute.Value)
                    .Concat(element.Nodes().OfType<XText>().Select(text => text.Value))
            );

    [GeneratedRegex(@"\$\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex PropertyReference();

    [GeneratedRegex(@"\$\([^)]*%\(")]
    private static partial Regex MetadataInAPropertyFunction();
}
