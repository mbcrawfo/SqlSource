using System.IO;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// Each case's output model is as nullable as its truth, or wider: a column the truth says can be NULL has a property
// that can hold null.  It reads the compiled model and the truth, never the sidecar's nullable, so it can fail where
// "describe --check" passes, and it needs no server.
public sealed class RealisticModelNullabilityTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void Model_Case_IsAsNullableAsItsTruthOrWider(string name)
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, name));
        if (found.Refusal is not null)
        {
            Assert.Skip($"{found} is refused as {found.Refusal}, so it has no models.");
        }

        if (found.Models is { } refusal)
        {
            Assert.Skip($"{found} has no models: the generator gives {refusal.Id}.");
        }

        var model = RealisticModels.OutputModelOf(RealisticModels.EntryOf(found));
        var properties = RealisticModels.PropertiesOf(model);

        properties.Count.ShouldBe(
            found.Columns.Count,
            $"{found} has {found.Columns.Count} columns in its truth, and {model.Name} {properties.Count} properties"
        );
        for (var index = 0; index < found.Columns.Count; index++)
        {
            var column = found.Columns[index];
            var property = properties[index];
            if (column.Nullable && !RealisticModels.IsNullable(property))
            {
                Assert.Fail(
                    $"{found} column {index} {column.Name} can be NULL ({column.Why}), and the property "
                        + $"{model.Name}.{property.Name} of {property.PropertyType.Name} is not nullable"
                );
            }
        }
    }
}
