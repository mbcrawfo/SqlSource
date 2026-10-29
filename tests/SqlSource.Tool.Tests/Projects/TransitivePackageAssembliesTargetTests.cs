using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

// The target SqlSourceFindTransitivePackageAssemblies of build/SqlSource.targets, run by name with the real
// "dotnet msbuild" on the fixture PackageAssemblies, whose project writes its references out as a build resolves them,
// so that nothing is restored or resolved.  Each test starts MSBuild once, which takes a second or two.
public sealed class TransitivePackageAssembliesTargetTests : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // Only the assemblies of Brought.Client, the one package the project does not list: not those of the packages it
    // lists, nor the framework's, nor a project's, nor one of no package.  Each is named once, by its file's name.
    [Fact]
    public async Task Run_ReferencesOfAPackageNotListed_AreTheValue()
    {
        var value = await ValueOfAsync(_fixtures.Copy("PackageAssemblies"));

        value.Split('|').ShouldBe(["Brought.Client", "Brought.Client.Extra"]);
    }

    // NuGet reads a package's id ignoring case, so mixed.case.package is the package Mixed.Case.Package.
    [Fact]
    public async Task Run_PackageListedInAnotherCase_IsNotInTheValue()
    {
        var value = await ValueOfAsync(_fixtures.Copy("PackageAssemblies"));

        value.Split('|').ShouldNotContain("Mixed");
    }

    // Run by name, nothing resolves the references of Single, so the value is empty, and no name is said to come from
    // a package alone.
    [Fact]
    public async Task Run_ProjectWithNoReferences_HasAnEmptyValue()
    {
        var value = await ValueOfAsync(_fixtures.Copy("Single"));

        value.ShouldBe("");
    }

    // The compiler reads a value up to its first # or ;, so a name's # is written %23, as the build's folders write it.
    [Fact]
    public async Task Run_AssemblyNameWithACharacterTheCompilerCuts_IsEscaped()
    {
        var project = _fixtures.Copy("PackageAssemblies");
        _ = _fixtures.WriteFile(
            "PackageAssemblies/Directory.Build.targets",
            """
            <Project>
                <ItemGroup>
                    <ReferencePath
                        Include="lib/Odd#Name.dll"
                        NuGetPackageId="Brought.Client"
                        ReferenceSourceTarget="ResolveAssemblyReference"
                    />
                </ItemGroup>
            </Project>
            """
        );

        var value = await ValueOfAsync(project);

        value.Split('|').ShouldContain("Odd%23Name");
    }

    // The value of the property after the target runs, as MSBuild prints a single property it is asked for.
    private static async Task<string> ValueOfAsync(string project)
    {
        var (exitCode, output) = await FixtureProjects.RunMSBuildAsync(
            project,
            ["-t:SqlSourceFindTransitivePackageAssemblies", "-getProperty:SqlSourceTransitivePackageAssemblies"]
        );

        exitCode.ShouldBe(0, output);
        return output.Trim();
    }
}
