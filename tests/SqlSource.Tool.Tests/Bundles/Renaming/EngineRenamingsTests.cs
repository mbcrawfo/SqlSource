using System;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

public sealed class EngineRenamingsTests
{
    [Fact]
    public void For_EachEngine_IsNotBuiltYet()
    {
        foreach (var engine in Enum.GetValues<DatabaseEngine>())
        {
            EngineRenamings.For(engine).ShouldBeNull(engine.ToString());
        }
    }

    [Fact]
    public void For_NoEngine_Throws() =>
        Should.Throw<UnexpectedEnumValueException>(() => EngineRenamings.For((DatabaseEngine)99));
}
