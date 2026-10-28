using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each case's output model is as nullable as its truth, or wider: a column the truth says can be NULL has a property
// that can hold null.  Each column is paired with the property at its position, which must be named after it, so a
// reordering cannot pass.  It reads the compiled model and the truth, never the sidecar's nullable, so it can fail
// where "describe --check" passes, and it needs no server.
public sealed class RealisticModelNullabilityTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void Model_Case_IsAsNullableAsItsTruthOrWider(string name) =>
        RealisticCases.Models.ShouldBeAsNullableAsItsTruth(RealisticCases.Corpus.ReadNamed(name));
}
