using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlServer.Types;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/ClrTypes.sql, run through the methods that ClrTypeQueries gets: hierarchyid, geography and
// geometry, written through a parameter and read back through a column, as the types of Microsoft.SqlServer.Types that
// the file's type override names.  SqlClient refuses a value of such a type unless its parameter names the type, so
// each is bound as a Udt of the type's name; bound by its value alone, every call with a value throws
// ArgumentException, "UdtTypeName property must be set for UDT parameters.".  The rows are those of a table variable.
[Collection(UsesSqlServer.Name)]
public sealed class ClrTypeQueryTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task WriteClrTypes_AValueOfEach_ReadsThemBack()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var written = await connection.WriteClrTypes(
            new WriteClrTypesArgs(
                SqlHierarchyId.Parse("/1/2/"),
                SqlGeography.Point(47.6, -122.3, 4326),
                SqlGeometry.Point(1, 2, 0)
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );

        written.Node.ShouldNotBeNull().ToString().ShouldBe("/1/2/");
        var place = written.Place.ShouldNotBeNull();
        place.Lat.Value.ShouldBe(47.6);
        place.Long.Value.ShouldBe(-122.3);
        place.STSrid.Value.ShouldBe(4326);
        var shape = written.Shape.ShouldNotBeNull();
        shape.STX.Value.ShouldBe(1);
        shape.STY.Value.ShouldBe(2);
        shape.STSrid.Value.ShouldBe(0);
    }

    // A null is sent as DBNull.Value, which the server takes as a NULL of each type, and read back as null.
    [Fact]
    public async Task WriteClrTypes_NullOfEach_ReadsNulls()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var written = await connection.WriteClrTypes(
            new WriteClrTypesArgs(null, null, null),
            cancellationToken: TestContext.Current.CancellationToken
        );

        written.Node.ShouldBeNull();
        written.Place.ShouldBeNull();
        written.Shape.ShouldBeNull();
    }

    // The parameter is a hierarchyid of the server, whose method IsDescendantOf compares it with each node.
    [Theory]
    [InlineData("/", new[] { "/", "/1/", "/1/1/", "/1/2/", "/2/" })]
    [InlineData("/1/", new[] { "/1/", "/1/1/", "/1/2/" })]
    [InlineData("/2/", new[] { "/2/" })]
    [InlineData("/3/", new string[0])]
    public async Task ListDescendants_ANode_ReadsItAndTheNodesBelowIt(string ancestor, string[] expected)
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var nodes = await connection.ListDescendants(
            new ListDescendantsArgs(SqlHierarchyId.Parse(ancestor)),
            cancellationToken: TestContext.Current.CancellationToken
        );

        nodes.Select(row => row.Node.ToString()).ShouldBe(expected);
    }
}
