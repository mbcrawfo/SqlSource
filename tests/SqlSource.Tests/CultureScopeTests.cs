using System.Globalization;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests;

public sealed class CultureScopeTests
{
    [Fact]
    public void Dispose_AfterAnotherCulture_PutsBackTheOneItFound()
    {
        var found = CultureInfo.CurrentCulture;
        var other = new CultureInfo("sv-SE");

        using (new CultureScope(other))
        {
            CultureInfo.CurrentCulture.ShouldBe(other);
        }

        CultureInfo.CurrentCulture.ShouldBe(found);
    }
}
