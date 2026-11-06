using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Repository.Tests.Describing.Postgres.Walk;

// The source of PlanNode, read as text: it names a plan's keys only through PlanKeys.
public sealed class PlanNodeSourceTests
{
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
