using System;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests;

public sealed class UnexpectedEnumValueExceptionTests
{
    [Fact]
    public void Of_ValueOfNoMember_NamesTheValueAndTheEnum() =>
        UnexpectedEnumValueException
            .Of((DatabaseEngine)7)
            .Message.ShouldBe("The value 7 is no member of SqlSource.Settings.DatabaseEngine.");

    [Fact]
    public void Of_AnyValue_IsAnInvalidOperationException() =>
        UnexpectedEnumValueException.Of(DatabaseEngine.SqlServer).ShouldBeAssignableTo<InvalidOperationException>();
}
