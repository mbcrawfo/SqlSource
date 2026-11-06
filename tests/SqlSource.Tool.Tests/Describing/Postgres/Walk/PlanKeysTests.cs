using System.Linq;
using System.Reflection;
using Shouldly;
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
}
