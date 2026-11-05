using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

public sealed class PlanKeysTests
{
    [Fact]
    public void All_IsEveryConstant()
    {
        var constants = typeof(PlanKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(static field => field.IsLiteral)
            .Select(static field => (string)field.GetRawConstantValue()!);

        PlanKeys.All.ShouldBe(constants, ignoreOrder: true);
    }

    [Fact]
    public void PlanNode_ReadsNoKeyLiteral()
    {
        // The code, without the comments: the documentation's `param name="Output"` names a parameter and not a key.
        var code = File.ReadLines(
                RepositoryRoot.Path("src", "SqlSource.Tool", "Describing", "Postgres", "Walk", "PlanNode.cs")
            )
            .Where(static line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        var source = string.Join('\n', code);

        foreach (var key in PlanKeys.All)
        {
            source.Contains($"\"{key}\"", StringComparison.Ordinal).ShouldBeFalse(key);
        }
    }
}
