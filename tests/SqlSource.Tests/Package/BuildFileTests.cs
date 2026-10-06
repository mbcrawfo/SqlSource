using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Package;

// build/SqlSource.props is the MSBuild file that the package puts into every project that references it.  The
// end-to-end tests show it working for this project; these read the file for what this project cannot show.
public class BuildFileTests
{
    private static readonly XDocument Props = XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "build", "SqlSource.props")
    );

    [Fact]
    public void Props_TokenValidationProperty_ReachesTheCompilerInEveryProject()
    {
        var item = Props.Descendants("CompilerVisibleProperty").ShouldHaveSingleItem();

        item.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("SqlSourceTokenValidation");

        // A project that sets EnableDefaultSqlSourceItems to false lists its own .sql files, and still needs the
        // property.  So nothing may put a condition on the item.
        item.AncestorsAndSelf().SelectMany(element => element.Attributes("Condition")).ShouldBeEmpty();
    }
}
