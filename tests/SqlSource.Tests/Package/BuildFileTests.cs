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
    private static readonly string[] SdkProperties =
    [
        "DefaultItemExcludes",
        "DefaultExcludesInProjectFolder",
        "IntermediateOutputPath",
        "MSBuildProjectFile",
    ];

    // What the generator reads: each as a property of the project and as metadata of a file's item.
    private static readonly string[] Settings = ["SqlSourceDialect", "SqlSourceGeneratorParameters", "SqlSourceOutput"];

    // What the targets trim and the generator does not read.
    private static readonly string[] TrimmedOnly = ["SqlSourceDatabase"];

    private static readonly XDocument Props = Load("SqlSource.props");

    private static readonly XDocument Targets = Load("SqlSource.targets");

    [Fact]
    public void Props_PropertiesOfThePackage_ReachTheCompilerInEveryProject()
    {
        var items = Props.Descendants("CompilerVisibleProperty").ToList();

        items.Select(item => item.Attribute("Include").ShouldNotBeNull().Value).ShouldBe(Settings, ignoreOrder: true);

        // A project that sets SqlSourceIncludeFiles to false lists its own .sql files, and still needs the
        // properties.  So nothing may put a condition on the items.
        items.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // The SDK writes a section into the file the compiler reads for every item of the type that is named here,
    // whether the item has the metadata or not.  AdditionalFiles would be every .sql file of the project.
    // SqlSourceSettingsFile holds only the files that have any: see
    // Targets_FilesWithMetadata_AreTheItemsWhoseMetadataTheCompilerReads.
    [Fact]
    public void Props_MetadataOfASqlFile_ReachesTheCompilerInEveryProject()
    {
        var items = Props.Descendants("CompilerVisibleItemMetadata").ToList();

        items.ShouldAllBe(item => item.Attribute("Include")!.Value == "SqlSourceSettingsFile");
        items
            .Select(item => item.Attribute("MetadataName").ShouldNotBeNull().Value)
            .ShouldBe(Settings, ignoreOrder: true);
        items.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // The compiler reads a property, and the metadata of an item, from a file with one line for each.  A value on a
    // line of its own would arrive empty, so the package trims each one.  GenerateMSBuildEditorConfigFileCore is the
    // target of the SDK that writes the file.  A trim that runs before it sees a value wherever it was set, in
    // Directory.Build.targets or by another target, and runs in every build that writes the file, a design-time
    // build too.  A trim outside a target would see only what is set before NuGet imports the file.  The target that
    // collects the files with metadata runs there for the same reasons.
    [Fact]
    public void Targets_EveryTarget_RunsBeforeTheBuildWritesTheFileTheCompilerReads()
    {
        var root = Targets.Root.ShouldNotBeNull();

        root.Elements().ShouldAllBe(element => element.Name.LocalName == "Target");
        root.Elements()
            .Select(target => target.Attribute("BeforeTargets")?.Value)
            .ShouldAllBe(before => before == "GenerateMSBuildEditorConfigFileCore");
        root.Elements().SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // SqlSource.Tests.csproj writes its dialect and its generator parameters on lines of their own, and
    // tools/package-install sets one in Directory.Build.targets, so the end-to-end tests and the check of the
    // installed package show the trimming at work; this pins that each property has it.
    [Fact]
    public void Targets_EveryPropertyOfThePackage_IsTrimmed()
    {
        var trimmed = Targets
            .Descendants("Target")
            .Single(target => target.Attribute("Name")!.Value == "SqlSourceTrimProperties")
            .Descendants("PropertyGroup")
            .Elements()
            .ToList();

        trimmed.Select(element => element.Name.LocalName).ShouldBe(Settings.Concat(TrimmedOnly), ignoreOrder: true);
        trimmed.ShouldAllBe(element => element.Value == $"$({element.Name.LocalName}.Trim())");
        trimmed.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // The target runs once for each combination of values that the metadata has, with only the items of that
    // combination in reach.  Each value goes through a property so that it is never text inside a property
    // function: see Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction.  Most files have no metadata, and
    // they are one batch with nothing to trim: nothing is written back to them, and a value that is not set is not
    // written at all.
    [Fact]
    public void Targets_MetadataOfEverySqlFile_IsTrimmed()
    {
        var names = Settings.Concat(TrimmedOnly).ToList();
        var item = Targets.Descendants("AdditionalFiles").ShouldHaveSingleItem();
        var target = item.Ancestors("Target").ShouldHaveSingleItem();

        target.Attribute("Name").ShouldNotBeNull().Value.ShouldBe("SqlSourceTrimMetadataOfFiles");
        target
            .Attribute("Outputs")
            .ShouldNotBeNull()
            .Value.Split('|')
            .ShouldBe(names.Select(name => $"%(AdditionalFiles.{name})"), ignoreOrder: true);
        foreach (var name in names)
        {
            target.Descendants(name + "AsWritten").ShouldHaveSingleItem().Value.ShouldBe($"%(AdditionalFiles.{name})");
            var metadata = item.Elements(name).ShouldHaveSingleItem();
            metadata.Value.ShouldBe($"$({name}AsWritten.Trim())");
            metadata.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe($"'$({name}AsWritten)' != ''");
        }

        item.Attributes().ShouldBeEmpty();
        item.Elements().Count().ShouldBe(names.Count);
        item.Parent.ShouldNotBeNull()
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'" + string.Concat(names.Select(name => $"$({name}AsWritten)")) + "' != ''");
    }

    // The compiler reads the metadata from the items of SqlSourceSettingsFile, and the files without any are not
    // among them.  The collecting has to come after the trim: the values it copies are trimmed by then, and a file
    // whose values were only white space is left out.  MSBuild does not promise an order for two targets that hook
    // the same one, so the target says what it depends on.
    [Fact]
    public void Targets_FilesWithMetadata_AreTheItemsWhoseMetadataTheCompilerReads()
    {
        var item = Targets.Descendants("SqlSourceSettingsFile").ShouldHaveSingleItem();
        var target = item.Ancestors("Target").ShouldHaveSingleItem();

        item.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
        item.Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'" + string.Concat(Settings.Select(name => $"%(AdditionalFiles.{name})")) + "' != ''");
        target.Attribute("Name").ShouldNotBeNull().Value.ShouldBe("SqlSourceCollectSettingsFiles");
        target.Attribute("DependsOnTargets").ShouldNotBeNull().Value.ShouldBe("SqlSourceTrimMetadataOfFiles");
    }

    // A file that is removed has no timestamp left to compare, so the build after it compiles again only when an
    // input of the compiler changed.  The list of AdditionalFiles is not among the inputs the SDK keeps.  The package
    // writes a hash of the list to a file and names the file as an input, and it writes the file only when the hash
    // changed, or every build would compile.  tools/check-package-install.sh removes a file and shows this at work.
    [Fact]
    public void Targets_ListOfAdditionalFiles_IsAnInputOfTheCompiler()
    {
        var hash = Targets.Descendants("Hash").ShouldHaveSingleItem();
        var target = hash.Parent.ShouldNotBeNull();
        var write = target.Elements("WriteLinesToFile").ShouldHaveSingleItem();
        var input = target.Descendants("CustomAdditionalCompileInputs").ShouldHaveSingleItem();

        hash.Attribute("ItemsToHash").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
        var property = hash.Elements("Output").ShouldHaveSingleItem().Attribute("PropertyName").ShouldNotBeNull().Value;
        write.Attribute("Lines").ShouldNotBeNull().Value.ShouldBe($"$({property})");
        write.Attribute("WriteOnlyWhenDifferent").ShouldNotBeNull().Value.ShouldBe("true");
        write.Attribute("Overwrite").ShouldNotBeNull().Value.ShouldBe("true");
        input.Attribute("Include").ShouldNotBeNull().Value.ShouldBe(write.Attribute("File").ShouldNotBeNull().Value);
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
