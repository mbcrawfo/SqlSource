using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Package;

// build/SqlSource.props, build/SqlSource.targets and build/SqlSource.AfterSdk.targets are the MSBuild files that the
// package puts into every project that references it.  The end-to-end tests show them working for this project; these
// read the files for what this project cannot show.
public sealed partial class BuildFileTests
{
    private const string Prefix = "SqlSource";

    // The properties of the SDK that the files read.  Every other property in them belongs to the package.
    private static readonly string[] SdkProperties =
    [
        "DefaultItemExcludes",
        "DefaultExcludesInProjectFolder",
        "IntermediateOutputPath",
        "MSBuildProjectFile",
        "MSBuildProjectFullPath",
        "TargetFramework",
        "LangVersion",
        "DefineConstants",
        "AfterMicrosoftNETSdkTargets",
        "MSBuildThisFileDirectory",
        "MSBuildProjectDirectory",
        "BaseIntermediateOutputPath",
        "BaseOutputPath",
        "OutDir",
        "EmitCompilerGeneratedFiles",
        "CompilerGeneratedFilesOutputPath",
        "NuGetPackageRoot",
        "NuGetPackageFolders",
    ];

    // The target that writes the project manifest for the sqlsource tool.  It is the one target no build runs.
    private const string ManifestTarget = "SqlSourceWriteManifest";

    // The target that finds the settings files and gives them to the compiler.
    private const string SearchTarget = "SqlSourceFindSettingsFiles";

    // The target that a target of a project hooks when it adds a .sql file, so that the file is searched above and is
    // in the manifest.
    private const string ListTarget = "SqlSourceListFiles";

    // The folders the build writes into and the NuGet package folders, which the targets set and the compiler is
    // shown.  They are no setting: a project does not write them.
    private const string BuildFolders = "SqlSourceBuildFolders";

    // The target that sets them.
    private const string BuildFoldersTarget = "SqlSourceFindBuildFolders";

    // The names of the referenced assemblies that only a package the project does not list brings, which the targets
    // set and the compiler is shown.  They are no setting either.
    private const string PackageAssemblies = "SqlSourceTransitivePackageAssemblies";

    // The target that sets them.
    private const string PackageAssembliesTarget = "SqlSourceFindTransitivePackageAssemblies";

    // The item of that target that holds the NuGet package folders other than the global packages folder.
    private const string PackageFolder = "SqlSourcePackageFolder";

    private static readonly XDocument Props = Load("SqlSource.props");

    private static readonly XDocument Targets = Load("SqlSource.targets");

    private static readonly XDocument AfterSdk = Load("SqlSource.AfterSdk.targets");

    // What the search for settings files and the hash of AdditionalFiles hook in a build where
    // SqlSource.AfterSdk.targets is not imported.
    private const string SearchHook = "$(SqlSourceSettingsSearchHook)";

    // The sqlsource tool evaluates a project and asks for this property to find out whether the project uses
    // SqlSource.  A condition would make a project that uses it look like one that does not.
    [Fact]
    public void Props_Marker_SaysThatTheProjectUsesSqlSourceInEveryProject()
    {
        var marker = Props.Descendants("SqlSourceImported").ShouldHaveSingleItem();

        marker.Value.ShouldBe("true");
        marker.Parent.ShouldNotBeNull().Name.LocalName.ShouldBe("PropertyGroup");
        ConditionsAround(marker).ShouldBeEmpty();
    }

    // A source generator sees only the properties that are listed here, and the settings are in settings files, so
    // the compiler is shown the folders of the build and the assemblies that only a package brings, which the targets
    // set, alone.  No metadata is listed: the SDK would write a section into the file the compiler reads for every
    // item of a type that is.  A project that sets SqlSourceIncludeFiles to false lists its own .sql files, and still
    // needs the properties, so nothing may put a condition on the items.
    [Fact]
    public void Props_PropertiesTheCompilerIsShown_AreTheBuildsFoldersAndThePackageAssemblies()
    {
        var items = Props.Descendants("CompilerVisibleProperty").ToList();

        items
            .Select(item => item.Attribute("Include").ShouldNotBeNull().Value)
            .ShouldBe([BuildFolders, PackageAssemblies]);
        items.SelectMany(ConditionsAround).ShouldBeEmpty();
        Props.Descendants("CompilerVisibleItemMetadata").ShouldBeEmpty();
        Targets.Descendants("CompilerVisibleProperty").ShouldBeEmpty();
        Targets.Descendants("CompilerVisibleItemMetadata").ShouldBeEmpty();
    }

    // GenerateMSBuildEditorConfigFileCore is the target of the SDK that writes the file the compiler reads.  The
    // targets of the build's folders and of the package-brought assemblies run before it, when every property of the
    // SDK and of the artifacts layout is set and the references are resolved, and in every build that writes the
    // file, a design-time build too.  The search for settings files and the
    // hash hook it through a property, which SqlSource.AfterSdk.targets empties to run them later: see
    // Targets_SettingsSearch_RunsBeforeTheCompilerWhereverItIsHooked.  The target that writes the manifest is the one
    // that is not of a build, Targets_ManifestTarget_HooksNothing, and the hook for a target that adds .sql files
    // hooks nothing either, Targets_HookForFilesThatTargetsAdd_RunsBeforeTheSearchAndTheManifest.
    [Fact]
    public void Targets_EveryTargetOfABuild_RunsBeforeTheBuildWritesTheFileTheCompilerReads()
    {
        var targets = Targets
            .Root.ShouldNotBeNull()
            .Elements("Target")
            .Where(target => target.Attribute("Name")!.Value is not (ManifestTarget or ListTarget))
            .ToList();

        targets.Count.ShouldBe(4);
        targets
            .Select(target => target.Attribute("BeforeTargets")?.Value)
            .ShouldBe(
                ["GenerateMSBuildEditorConfigFileCore", "GenerateMSBuildEditorConfigFileCore", SearchHook, SearchHook],
                ignoreOrder: true
            );
        targets.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // A target of a project that adds a .sql file while the build runs hooks this one with BeforeTargets, wherever it
    // is declared: the search depends on it, so the file is searched above in a build, and the manifest depends on it
    // first, so the file is in the manifest that the tool runs by name.  It does nothing itself, and hooks nothing,
    // so that it runs only as what those two depend on.
    [Fact]
    public void Targets_HookForFilesThatTargetsAdd_RunsBeforeTheSearchAndTheManifest()
    {
        var hook = Target(ListTarget);

        hook.Attributes().Select(attribute => attribute.Name.LocalName).ShouldBe(["Name"]);
        hook.HasElements.ShouldBeFalse();
        Target(SearchTarget).Attribute("DependsOnTargets").ShouldNotBeNull().Value.Split(';').ShouldContain(ListTarget);
        Targets
            .Root.ShouldNotBeNull()
            .Elements("PropertyGroup")
            .Elements("SqlSourceManifestDependsOn")
            .First()
            .Value.Split(';')[0]
            .ShouldBe(ListTarget);
    }

    // A target's DependsOnTargets is read before the target runs, so the list that the manifest target depends on
    // is a property set where the file is imported.  The nesting of a sidecar under its .sql file is read from the
    // evaluated items by the IDEs, so it is outside every target too: see the comment in the file.  So is the
    // Content item of the settings files: see Targets_SettingsFiles_AreNeverCopied.  Nothing else is outside a
    // target, and the file has no other condition outside one: a second such element would be read by every
    // project, in every build.
    [Fact]
    public void Targets_Root_HoldsTargetsTheManifestsListTheNestingAndTheSettingsFiles()
    {
        var root = Targets.Root.ShouldNotBeNull();

        var outside = root.Elements().Where(element => element.Name.LocalName != "Target").ToList();
        outside.Select(element => element.Name.LocalName).ShouldBe(["PropertyGroup", "ItemGroup", "ItemGroup"]);

        var group = outside[0];
        group.Attributes().ShouldBeEmpty();

        var lines = group.Elements().ToList();
        lines
            .Select(line => line.Name.LocalName)
            .ShouldBe(["SqlSourceSettingsSearchHook", "SqlSourceManifestDependsOn", "SqlSourceManifestDependsOn"]);
        lines[0].Attributes().ShouldBeEmpty();
        lines[0].Value.ShouldBe("GenerateMSBuildEditorConfigFileCore");
        lines[1].Attributes().ShouldBeEmpty();
        lines[1].Value.ShouldBe(ListTarget + ";" + SearchTarget + ";" + BuildFoldersTarget);
        // A project that is not of the SDK has no TargetFramework, and no such target to depend on.
        lines[2].Attributes().ShouldHaveSingleItem().Name.LocalName.ShouldBe("Condition");
        lines[2].Attribute("Condition")!.Value.ShouldBe("'$(TargetFramework)' != ''");
        lines[2].Value.ShouldBe("$(SqlSourceManifestDependsOn);AddImplicitDefineConstants");

        // The item types an IDE may nest on: the SDK lists a sidecar under None, and the Web SDK under Content.  Update
        // changes the items that are already there and adds none, so a project without a sidecar is not given one.
        // Content is never copied: the Web SDK copies it to the output and publish folders.  None is copied only when a
        // project asks, as this one does for the examples of the sidecar format, and is left as the project has it.
        var nesting = outside[1];
        nesting.Attributes().ShouldBeEmpty();
        var items = nesting.Elements().ToList();
        items.Select(item => item.Name.LocalName).ShouldBe(["AdditionalFiles", "None", "Content"]);
        var nested = new Dictionary<string, string> { ["Update"] = "**/*.sql.json", ["DependentUpon"] = "%(Filename)" };
        var neverCopied = new Dictionary<string, string>(nested)
        {
            ["CopyToOutputDirectory"] = "Never",
            ["CopyToPublishDirectory"] = "Never",
        };
        foreach (var item in items)
        {
            item.Attributes()
                .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
                .ShouldBe(item.Name.LocalName == "Content" ? neverCopied : nested);
            item.HasElements.ShouldBeFalse();
        }

        outside[2].Attributes().ShouldBeEmpty();
    }

    // A settings file is an input of the generator alone.  The Web SDK lists a .json file under Content and copies
    // it to the output and publish folders.  The item is in the targets and not in the props: NuGet imports the
    // props before the Web SDK adds its Content, and Update changes only the items that are already listed.
    // tools/check-package-install.sh publishes a web project and shows this at work.
    [Fact]
    public void Targets_SettingsFiles_AreNeverCopied()
    {
        var group = Targets.Root.ShouldNotBeNull().Elements("ItemGroup").Last();

        var item = group.Elements().ShouldHaveSingleItem();
        item.Name.LocalName.ShouldBe("Content");
        item.Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Update"] = "**/sqlsource.json",
                    ["CopyToOutputDirectory"] = "Never",
                    ["CopyToPublishDirectory"] = "Never",
                }
            );
        Props.Descendants("Content").ShouldBeEmpty();
    }

    // A C# file under the folders the build writes C# files and its outputs into is the build's, and reaches no
    // settings file: the base folders, the intermediate and output folders, and the folder of the compiler's generated
    // files when it writes them.  So is one under the NuGet package folders, where a package that ships C# files as
    // content has its Compile items: the global packages folder and each folder of NuGetPackageFolders, a ;-parted
    // list that names it too, split as an item and left without it.  Each is the full path made against the project's
    // folder, with the separator that NormalizeDirectory ends it with, after a |, and is left out when it has no value.
    // The property is emptied first, so that it holds the build's folders whatever the project wrote.  The compiler
    // reads a value up to its first # or ;, and | parts the folders, so each folder's %, |, # and ; are escaped as
    // %25, %7C, %23 and %3B, % first, which BuildFolders decodes; MSBuild reads %XX in the file as the character it
    // stands for, so each escape is written with its % as %25.  The generator spells the name in BuildFolders, and
    // SettingsFileBuildFoldersTests runs it on it.  ManifestTargetTests runs the target.
    [Fact]
    public void Targets_BuildFolders_AreTheEscapedFullPathsOfEveryFolderTheBuildWritesInto()
    {
        var target = Target(BuildFoldersTarget);

        target
            .Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = BuildFoldersTarget,
                    ["BeforeTargets"] = "GenerateMSBuildEditorConfigFileCore",
                }
            );
        target.Elements().Select(element => element.Name.LocalName).ShouldBe(["ItemGroup", "PropertyGroup"]);
        var items = target.Elements("ItemGroup").ShouldHaveSingleItem().Elements().ToList();
        items
            .Select(item =>
                (
                    item.Name.LocalName,
                    string.Join(
                        " ",
                        item.Attributes().Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}")
                    ),
                    string.Join(" ", item.Elements().Select(metadata => $"{metadata.Name.LocalName}={metadata.Value}"))
                )
            )
            .ShouldBe([
                (PackageFolder, "Include=$(NuGetPackageFolders) Exclude=$(NuGetPackageRoot)", ""),
                (PackageFolder, "", "SqlSourceFolder=" + Escaped("%(Identity)")),
            ]);
        var lines = target.Elements("PropertyGroup").ShouldHaveSingleItem().Elements().ToList();
        lines.ShouldAllBe(line => line.Name.LocalName == "SqlSourceBuildFolders");
        lines
            .Select(line => (line.Value, line.Attribute("Condition")?.Value, line.Attributes().Count()))
            .ShouldBe([
                ("", null, 0),
                (Appended("BaseIntermediateOutputPath"), "'$(BaseIntermediateOutputPath)' != ''", 1),
                (Appended("IntermediateOutputPath"), "'$(IntermediateOutputPath)' != ''", 1),
                (Appended("BaseOutputPath"), "'$(BaseOutputPath)' != ''", 1),
                (Appended("OutDir"), "'$(OutDir)' != ''", 1),
                (
                    Appended("CompilerGeneratedFilesOutputPath"),
                    "'$(EmitCompilerGeneratedFiles)' == 'true' and '$(CompilerGeneratedFilesOutputPath)' != ''",
                    1
                ),
                (Appended("NuGetPackageRoot"), "'$(NuGetPackageRoot)' != ''", 1),
                (
                    $"$(SqlSourceBuildFolders)|@({PackageFolder}->'%(SqlSourceFolder)', '|')",
                    $"'@({PackageFolder})' != ''",
                    1
                ),
            ]);
    }

    // A short type name that binds to a type of an assembly that only a package the project does not list brings is
    // warned of, so the compiler is shown the names of those assemblies: each reference of a package, NuGetPackageId,
    // but the framework's and a project's, whose package is no PackageReference of the project, compared ignoring
    // case, by its file's name, once.  It has no DependsOnTargets: a build resolves the references before the file
    // the compiler reads is written, and run by name with nothing resolved it sets the property empty.  The
    // metadata is never the argument of a property function, so the names are joined with a /, which no file name
    // holds, and the list is escaped with the four Replace calls of the build's folders, which
    // TransitivePackageAssemblies decodes, before each / is made the | that parts the names.  The generator spells
    // the name in TransitivePackageAssemblies.  TransitivePackageAssembliesTargetTests runs the target.
    [Fact]
    public void Targets_PackageAssemblies_AreTheEscapedNamesOfTheReferencesOnlyAPackageBrings()
    {
        var target = Target(PackageAssembliesTarget);

        target
            .Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = PackageAssembliesTarget,
                    ["BeforeTargets"] = "GenerateMSBuildEditorConfigFileCore",
                }
            );
        target.Elements().Select(element => element.Name.LocalName).ShouldBe(["ItemGroup", "PropertyGroup"]);
        var items = target.Elements("ItemGroup").ShouldHaveSingleItem().Elements().ToList();
        items
            .Select(item =>
                (
                    item.Name.LocalName,
                    string.Join(
                        " ",
                        item.Attributes().Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}")
                    )
                )
            )
            .ShouldBe([
                (
                    "SqlSourcePackageAssembly",
                    "Include=@(ReferencePath) Condition='%(ReferencePath.NuGetPackageId)' != ''"
                        + " and '%(ReferencePath.FrameworkReferenceName)' == ''"
                        + " and '%(ReferencePath.ReferenceSourceTarget)' != 'ProjectReference'"
                ),
                ("SqlSourceListedPackage", "Include=@(PackageReference) NuGetPackageId=%(Identity)"),
                (
                    "SqlSourcePackageAssembly",
                    "Remove=@(SqlSourceListedPackage) MatchOnMetadata=NuGetPackageId"
                        + " MatchOnMetadataOptions=CaseInsensitive"
                ),
                ("SqlSourcePackageAssemblyName", "Include=@(SqlSourcePackageAssembly->'%(Filename)'->Distinct())"),
            ]);
        items.ShouldAllBe(item => !item.HasElements);
        var lines = target.Elements("PropertyGroup").ShouldHaveSingleItem().Elements().ToList();
        lines
            .Select(line => (line.Name.LocalName, line.Value, line.Attributes().Count()))
            .ShouldBe([
                ("SqlSourcePackageAssemblyNames", "@(SqlSourcePackageAssemblyName, '/')", 0),
                (
                    PackageAssemblies,
                    "$(SqlSourcePackageAssemblyNames" + EscapedForTheCompiler + ".Replace('/', '|'))",
                    0
                ),
            ]);
    }

    // A generator cannot read the disk, so every settings file that can apply to a .sql file or to a type is given
    // to the compiler, as the SDK gives it the .editorconfig files: every folder above a .sql file or a Compile item,
    // to the top of the drive, its full path made distinct (msbuild issue 4392), combined with the name, kept where
    // the file exists.  The search is not under SqlSourceIncludeFiles, so a project that lists its own .sql files
    // still gets its settings.  It runs in a target, so that a .sql file a target adds is searched above too, and
    // after SqlSourceListFiles, the hook such a target is given.  A file the project lists itself is not given twice.
    [Fact]
    public void Targets_SettingsFileSearch_FindsEverySqlsourceJsonAboveTheSqlFilesAndTheCompileFiles()
    {
        var target = Target(SearchTarget);

        target
            .Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = SearchTarget,
                    ["BeforeTargets"] = SearchHook,
                    ["DependsOnTargets"] = ListTarget,
                }
            );
        var items = target.Elements("ItemGroup").ShouldHaveSingleItem().Elements().ToList();
        items
            .Select(item =>
                (
                    item.Name.LocalName,
                    string.Join(
                        " ",
                        item.Attributes().Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}")
                    )
                )
            )
            .ShouldBe([
                ("SqlSourceSettingsSearchStart", "Include=@(AdditionalFiles) Condition='%(Extension)' == '.sql'"),
                ("SqlSourceSettingsSearchStart", "Include=@(Compile)"),
                (
                    "SqlSourceSettingsSearchFolder",
                    "Include=@(SqlSourceSettingsSearchStart->GetPathsOfAllDirectoriesAbove())"
                ),
                (
                    "SqlSourceSettingsFileFound",
                    "Include=@(SqlSourceSettingsSearchFolder->'%(FullPath)'->Distinct()"
                        + "->Combine('sqlsource.json')->Exists())"
                ),
                (
                    "AdditionalFiles",
                    "Include=@(SqlSourceSettingsFileFound) Exclude=@(AdditionalFiles->'%(FullPath)') Visible=false"
                ),
            ]);
        items.ShouldAllBe(item => !item.HasElements);
    }

    // The manifest lists the settings files the compiler is given, so that the tool reads the same ones: every
    // AdditionalFiles item named sqlsource.json, the search's and any the project lists, each once by its full path.
    // The Settings lines come after the File lines and before the Compile lines.
    [Fact]
    public void Targets_ManifestTarget_ListsTheSettingsFilesBetweenTheSqlFilesAndTheCompileFiles()
    {
        var target = Manifest();
        var files = target.Descendants("SqlSourceManifestSettingsFile").ShouldHaveSingleItem();
        var lines = target
            .Descendants("SqlSourceManifestLine")
            .Select(line => line.Attribute("Include")!.Value)
            .ToList();

        files.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
        files.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe("'%(Filename)%(Extension)' == 'sqlsource.json'");
        var settings = lines.IndexOf(
            "@(SqlSourceManifestSettingsFile->'%(FullPath)'->Distinct()->'Settings=%(Identity)')"
        );
        settings.ShouldBeGreaterThan(
            lines.FindIndex(line => line.StartsWith("@(SqlSourceManifestSqlFile->", StringComparison.Ordinal))
        );
        settings.ShouldBe(lines.IndexOf("@(Compile->'Compile=%(FullPath)')") - 1);
    }

    // The tool runs the target by name.  A hook would make every build write the manifest, and a condition would
    // make a run of the tool find no file.
    [Fact]
    public void Targets_ManifestTarget_HooksNothing()
    {
        var target = Manifest();

        target
            .Attributes()
            .Select(attribute => attribute.Name.LocalName)
            .ShouldBe(["Name", "DependsOnTargets"], ignoreOrder: true);
        target.Attribute("DependsOnTargets")!.Value.ShouldBe("$(SqlSourceManifestDependsOn)");
    }

    // Version 2 of the format: what the compiler is given, and no setting, since the tool reads the settings from the
    // files of the Settings lines.  A property goes through Escape, or a value that holds a semicolon would be
    // written as several lines.  The tool reads the build's folders for the C# files that reach no settings file.
    [Fact]
    public void Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder() =>
        Manifest()
            .Descendants("SqlSourceManifestLine")
            .Select(line => line.Attribute("Include").ShouldNotBeNull().Value)
            .ShouldBe([
                "SqlSourceManifest=2",
                "Project=$([MSBuild]::Escape($(MSBuildProjectFullPath)))",
                $"TargetFramework=$([MSBuild]::Escape({OnOneLine("TargetFramework")}))",
                $"LangVersion=$([MSBuild]::Escape({OnOneLine("LangVersion")}))",
                $"DefineConstants=$([MSBuild]::Escape({OnOneLine("DefineConstants")}))",
                $"{BuildFolders}=$([MSBuild]::Escape($({BuildFolders})))",
                "@(SqlSourceManifestSqlFile->'File=%(FullPath)')",
                "@(SqlSourceManifestSettingsFile->'%(FullPath)'->Distinct()->'Settings=%(Identity)')",
                "@(Compile->'Compile=%(FullPath)')",
            ]);

    // A project may write its constants one on each line.  A line break inside a value would end its line of the
    // manifest, so each property of the SDK is put on one line first.
    [Theory]
    [InlineData("TargetFramework")]
    [InlineData("LangVersion")]
    [InlineData("DefineConstants")]
    public void Targets_ManifestTarget_WritesAPropertyOfTheSdkOnOneLine(string name) =>
        Manifest()
            .Descendants("SqlSourceManifestLine")
            .Select(line => line.Attribute("Include").ShouldNotBeNull().Value)
            .ShouldContain($"{name}=$([MSBuild]::Escape({OnOneLine(name)}))");

    [Fact]
    public void Targets_ManifestTarget_ListsTheSqlFilesAndTheCompileFiles()
    {
        var target = Manifest();
        var files = target.Descendants("SqlSourceManifestSqlFile").ShouldHaveSingleItem();

        files.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
        // MSBuild compares ignoring case, as the generator does for the extension.
        files.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe("'%(Extension)' == '.sql'");
        target
            .Descendants("SqlSourceManifestLine")
            .Select(line => line.Attribute("Include")!.Value)
            .ShouldContain("@(Compile->'Compile=%(FullPath)')");
    }

    // The file is the one SqlSourceManifestFile names, which the tool sets to a file of its own.  A file under obj
    // belongs to the project, so it is added to FileWrites, which tells "dotnet clean" of it once a build runs the
    // target; a file the tool named is the tool's to delete.
    [Fact]
    public void Targets_ManifestTarget_WritesTheFileThatIsNamedOrOneUnderObj()
    {
        var target = Manifest();
        var write = target.Elements("WriteLinesToFile").ShouldHaveSingleItem();
        var file = target.Descendants("SqlSourceManifestFile").ShouldHaveSingleItem();
        var isDefault = target.Descendants("SqlSourceManifestIsDefault").ShouldHaveSingleItem();
        var written = target.Descendants("FileWrites").ShouldHaveSingleItem();

        write.Attribute("File").ShouldNotBeNull().Value.ShouldBe("$(SqlSourceManifestFile)");
        write.Attribute("Lines").ShouldNotBeNull().Value.ShouldBe("@(SqlSourceManifestLine)");
        write.Attribute("Overwrite").ShouldNotBeNull().Value.ShouldBe("true");
        write.Attribute("WriteOnlyWhenDifferent").ShouldNotBeNull().Value.ShouldBe("true");

        const string NotSet = "'$(SqlSourceManifestFile)' == ''";
        file.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe(NotSet);
        file.Value.ShouldBe("$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.manifest");
        isDefault.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe(NotSet);
        // The flag is read before the name is given its default, or it would never be set.
        isDefault.IsBefore(file).ShouldBeTrue();
        written.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("$(SqlSourceManifestFile)");
        written
            .Parent.ShouldNotBeNull()
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'$(SqlSourceManifestIsDefault)' == 'true'");
    }

    // The prefix keeps an item type of the package apart from those of the SDK and of other packages, as it does a
    // property.  FileWrites is the SDK's own, and the target adds to it.
    [Fact]
    public void Targets_ManifestTarget_MakesOnlyItemTypesThatStartWithSqlSource() =>
        Manifest()
            .Elements("ItemGroup")
            .Elements()
            .Select(item => item.Name.LocalName)
            .Where(name => name != "FileWrites")
            .Distinct()
            .ShouldBe(
                ["SqlSourceManifestLine", "SqlSourceManifestSqlFile", "SqlSourceManifestSettingsFile"],
                ignoreOrder: true
            );

    // The same for the item types of every target.  These are the SDK's own, which the targets add to.
    [Fact]
    public void Targets_EveryTarget_MakesOnlyItemTypesThatStartWithSqlSourceOrAreTheSdks() =>
        Targets
            .Descendants("Target")
            .Descendants("ItemGroup")
            .Elements()
            .Select(item => item.Name.LocalName)
            .Except(["AdditionalFiles", "CustomAdditionalCompileInputs", "FileWrites"])
            .ShouldAllBe(name => name.StartsWith(Prefix, StringComparison.Ordinal));

    // A file that is removed has no timestamp left to compare, so the build after it compiles again only when an
    // input of the compiler changed.  The list of AdditionalFiles is not among the inputs the SDK keeps.  The package
    // writes a hash of the list to a file and names the file as an input, and it writes the file only when the hash
    // changed, or every build would compile.  tools/check-package-install.sh removes a file and shows this at work.
    // The list holds the settings files only once the search has run, and MSBuild promises no order for two targets
    // that hook the same one, so the target says what it depends on.
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
        target.Attribute("DependsOnTargets").ShouldNotBeNull().Value.ShouldBe(SearchTarget);
        target.Attribute("BeforeTargets").ShouldNotBeNull().Value.ShouldBe(SearchHook);
    }

    // NuGet imports the package's targets before Directory.Build.targets, and MSBuild runs the targets that hook one
    // target in the order they are declared, so a target of the project that hooks CoreCompile to add a .sql file
    // would run after the search.  The props have the SDK import SqlSource.AfterSdk.targets last, through the list
    // the SDK keeps for that, and keep the list's value.  A condition would leave the late search out of a project.
    [Fact]
    public void Props_AfterSdkTargets_AreAddedToTheFilesTheSdkImportsLast()
    {
        var item = Props.Descendants("AfterMicrosoftNETSdkTargets").ShouldHaveSingleItem();

        item.Value.ShouldBe("$(AfterMicrosoftNETSdkTargets);$(MSBuildThisFileDirectory)SqlSource.AfterSdk.targets");
        item.Parent.ShouldNotBeNull().Name.LocalName.ShouldBe("PropertyGroup");
        ConditionsAround(item).ShouldBeEmpty();
    }

    // The search and the hash run just before the compiler, after every target of the project that hooks
    // CoreCompile, from the file the SDK imports last.  That file empties the property they hook in
    // SqlSource.targets, so they run once, from there.  Where it is not imported, they run before the file the
    // compiler reads is written, as the target of the build's folders does.  A design-time build runs CoreCompile, so
    // it runs them either way.
    [Fact]
    public void Targets_SettingsSearch_RunsBeforeTheCompilerWhereverItIsHooked()
    {
        var root = AfterSdk.Root.ShouldNotBeNull();

        root.Elements().Select(element => element.Name.LocalName).ShouldBe(["PropertyGroup", "Target"]);
        var hook = root.Element("PropertyGroup")!.Elements().ShouldHaveSingleItem();
        hook.Name.LocalName.ShouldBe("SqlSourceSettingsSearchHook");
        hook.Value.ShouldBeEmpty();
        var target = root.Element("Target")!;
        target
            .Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = "SqlSourceSettingsFilesBeforeCompile",
                    ["BeforeTargets"] = "CoreCompile",
                    ["DependsOnTargets"] = SearchTarget + ";SqlSourceTrackAdditionalFiles",
                }
            );
        target.HasElements.ShouldBeFalse();
        root.Descendants().SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // MSBuild puts metadata into an expression as text, before it reads the expression.  A path with a quote in it,
    // as the fixture OddPaths has, would end a quoted argument early, and the whole expression would be the value.
    [Fact]
    public void Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction() =>
        Values(Targets).ShouldAllBe(value => !MetadataInAPropertyFunction().IsMatch(value));

    // A .sql file's sidecar goes to the compiler with it.
    [Fact]
    public void Props_SqlFilesAndSidecarsOfTheProject_AreLeftOutWhenSqlSourceIncludeFilesIsFalse()
    {
        var items = Props.Descendants("AdditionalFiles").ToList();

        items.Select(item => item.Attribute("Include").ShouldNotBeNull().Value).ShouldBe(["**/*.sql", "**/*.sql.json"]);
        items.ShouldAllBe(item =>
            item.Attribute("Exclude")!.Value == "$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)"
        );
        items.ShouldAllBe(item => item.Attribute("Condition") == null);
        items
            .Select(item => item.Parent.ShouldNotBeNull())
            .Distinct()
            .ShouldHaveSingleItem()
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'$(SqlSourceIncludeFiles)' != 'false'");
    }

    [Theory]
    [InlineData("SqlSource.props")]
    [InlineData("SqlSource.targets")]
    [InlineData("SqlSource.AfterSdk.targets")]
    public void BuildFile_EveryPropertyOfThePackage_StartsWithSqlSource(string file)
    {
        var document = file switch
        {
            "SqlSource.props" => Props,
            "SqlSource.targets" => Targets,
            _ => AfterSdk,
        };

        var names = PropertiesSet(document)
            .Concat(PropertiesShownToTheCompiler(document))
            .Concat(PropertiesRead(document))
            .Except(SdkProperties)
            .Distinct()
            .ToList();

        names.ShouldNotBeEmpty();
        names.ShouldAllBe(name => name.StartsWith(Prefix, StringComparison.Ordinal));
    }

    private static XElement Manifest() => Target(ManifestTarget);

    // The property with a folder after a |, escaped.
    private static string Appended(string folder) => "$(SqlSourceBuildFolders)|" + Escaped($"$({folder})");

    // A folder's full path, with its %, |, # and ; escaped for the compiler, each % of an escape written %25 for
    // MSBuild.
    private static string Escaped(string folder) =>
        $"$([MSBuild]::NormalizeDirectory($(MSBuildProjectDirectory), {folder}){EscapedForTheCompiler})";

    // The %, |, # and ; of a value escaped for the compiler, % first, each % of an escape written %25 for MSBuild.
    private const string EscapedForTheCompiler =
        ".Replace('%', '%2525').Replace('|', '%257C').Replace('#', '%2523').Replace(';', '%253B')";

    private static XElement Target(string name) =>
        Targets.Descendants("Target").Single(target => target.Attribute("Name")!.Value == name);

    private static IEnumerable<XAttribute> ConditionsAround(XElement element) =>
        element.AncestorsAndSelf().SelectMany(ancestor => ancestor.Attributes("Condition"));

    // A property on one line: the white space around the value goes, and each run of white space inside it, a line
    // break among it, becomes one space.
    private static string OnOneLine(string property) =>
        $@"$([System.Text.RegularExpressions.Regex]::Replace($({property}), '\s+', ' ').Trim())";

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
