using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// RepositoryRoot checks the reads of a project that tools/test-scopes.txt names, and finds it by the name of the
// assembly that runs, which TestScopes.ProjectOf derives from the project line.  An assembly named otherwise than
// that derivation, by a project file that Directory.Build.props of tests/DatabaseTests/ does not reach, would read
// every file unchecked.
public sealed class RunningProjectTests
{
    [Fact]
    public void RunningProject_ThisProject_IsAContainerProjectOfTheScopes() =>
        TestScopes.Repository.Projects.ShouldContainKey(RepositoryRoot.RunningProject);
}
