using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class DatabaseNameTests
{
    [Fact]
    public void Comparer_NamesThatDifferInCase_AreEqual()
    {
        DatabaseName.Comparer.Equals("Billing", "billing").ShouldBeTrue();
        DatabaseName.Comparer.Equals("billing", "billing2").ShouldBeFalse();
        DatabaseName.Comparer.GetHashCode("BILLING").ShouldBe(DatabaseName.Comparer.GetHashCode("billing"));
    }
}
