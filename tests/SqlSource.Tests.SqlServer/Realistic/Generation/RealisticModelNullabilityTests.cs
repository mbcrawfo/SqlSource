using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation.Models;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// Each case's output model is as nullable as its truth, or wider: a column the truth says can be NULL has a property
// that can hold null.  Each column is paired with the property at its position, which must be named after it, so a
// reordering cannot pass.  It reads the compiled model and the truth, never the sidecar's nullable, so it can fail
// where "describe --check" passes, and it needs no server.
public sealed class RealisticModelNullabilityTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void Model_Case_IsAsNullableAsItsTruthOrWider(string name)
    {
        var found = RealisticCases.ReadNamed(name);
        RealisticModels.SkipUnlessHasModels(found);

        var model = RealisticModels.OutputModelOf(RealisticModels.EntryOf(found));
        var properties = RealisticModels.PropertiesOf(model);

        properties.Count.ShouldBe(
            found.Columns.Count,
            $"{found} has {found.Columns.Count} columns in its truth, and {model.Name} {properties.Count} properties"
        );
        var failures = new List<string>();
        for (var index = 0; index < found.Columns.Count; index++)
        {
            var column = found.Columns[index];
            var property = properties[index];
            var expected = PropertyNames.PascalCase(column.Name);
            if (property.Name != expected)
            {
                failures.Add(
                    $"{found} column {index} {column.Name} is the property {model.Name}.{property.Name}, which "
                        + $"should be named {expected}"
                );
            }

            if (column.Nullable && !RealisticModels.IsNullable(property))
            {
                failures.Add(
                    $"{found} column {index} {column.Name} can be NULL ({column.Why}), and the property "
                        + $"{model.Name}.{property.Name} of {property.PropertyType.Name} is not nullable"
                );
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail(string.Join('\n', failures));
        }
    }
}
