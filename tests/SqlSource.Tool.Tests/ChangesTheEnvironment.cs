using Xunit;

namespace SqlSource.Tool.Tests;

// The collection of the tests that change the environment of the test process, which every test class shares and
// every program that a test starts gets.  Its tests run while no other test does; each puts back what it changed.
[CollectionDefinition(nameof(ChangesTheEnvironment), DisableParallelization = true)]
public sealed class ChangesTheEnvironment;
