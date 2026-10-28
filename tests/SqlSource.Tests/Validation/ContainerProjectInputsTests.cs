using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Validation;

// pre-commit-validation.sh skips a container project when no changed path can affect it, so a file that such a
// project includes at build time must never be one of the paths the scopes would let it skip.
public sealed class ContainerProjectInputsTests
{
    private const string FixtureProjects = "tests/SqlSource.Tool.Tests/Fixtures/Projects/";

    // The check must be able to fail, or an empty answer proves nothing: with the scopes of the fixture projects
    // taken out, the projects that link those fixtures name them.
    [Fact]
    public void Refused_ScopesWithoutTheFixtureProjectLines_NamesTheLinkedFixtures()
    {
        var text = File.ReadAllText(RepositoryRoot.Path("tools", "test-scopes.txt"));
        var lines = text.Split('\n').Where(line => !line.Contains(" " + FixtureProjects, StringComparison.Ordinal));
        var scopes = TestScopes.Parse(string.Join('\n', lines));

        var refused = ProjectIncludes.Refused(scopes, RepositoryRoot.Path());

        refused.ShouldContain(line =>
            line.StartsWith($"SqlSource.DatabaseTests.Postgres: {FixtureProjects}Single/", StringComparison.Ordinal)
        );
        refused.ShouldContain(line =>
            line.StartsWith($"SqlSource.DatabaseTests.SqlServer: {FixtureProjects}SqlServer/", StringComparison.Ordinal)
        );
    }

    // global.json reaches the describer projects only through tests/Fixtures.props, so this holds only while the check
    // reads the files that an Import names.
    [Fact]
    public void Refused_ScopesWithGlobalJsonNone_NamesItForBothDescriberProjects()
    {
        var text = File.ReadAllText(RepositoryRoot.Path("tools", "test-scopes.txt"));
        var scopes = TestScopes.Parse("none global.json\n" + text);

        var refused = ProjectIncludes.Refused(scopes, RepositoryRoot.Path());

        refused.ShouldContain("SqlSource.DatabaseTests.Postgres: global.json is None");
        refused.ShouldContain("SqlSource.DatabaseTests.SqlServer: global.json is None");
    }

    [Fact]
    public void Refused_RepositoryScopes_IsEmpty() =>
        ProjectIncludes.Refused(TestScopes.Repository, RepositoryRoot.Path()).ShouldBeEmpty();
}
