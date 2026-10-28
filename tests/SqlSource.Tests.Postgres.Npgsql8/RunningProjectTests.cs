using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Postgres.Npgsql8;

// RepositoryRoot checks the reads of a project that tools/test-scopes.txt names, and finds it by the name of the
// assembly that runs.  An assembly named otherwise than its .csproj would read every file unchecked.
public sealed class RunningProjectTests
{
    [Fact]
    public void RunningProject_ThisProject_IsAContainerProjectOfTheScopes() =>
        TestScopes.Repository.Projects.ShouldContainKey(RepositoryRoot.RunningProject);
}
