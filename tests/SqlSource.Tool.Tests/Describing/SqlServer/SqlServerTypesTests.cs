using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// A SQL Server type from a row of either procedure: the system type name and the facets verbatim, and the user type
// when the row names one.  The rows are those of the spike's table of types.
public sealed class SqlServerTypesTests
{
    private const string TypesAssembly =
        "Microsoft.SqlServer.Types, Version=11.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";

    [Theory]
    [InlineData("bigint", 8, 19, 0)]
    [InlineData("decimal(18,4)", 9, 18, 4)]
    [InlineData("time(7)", 5, 16, 7)]
    [InlineData("datetime2(0)", 6, 19, 0)]
    [InlineData("nvarchar(50)", 100, 0, 0)]
    [InlineData("varchar(max)", -1, 0, 0)]
    [InlineData("timestamp", 8, 0, 0)]
    [InlineData("uniqueidentifier", 16, 0, 0)]
    [InlineData("xml", -1, 0, 0)]
    [InlineData("sql_variant", 8016, 0, 0)]
    public void FromParameter_SystemType_IsTheNameAndFacetsVerbatim(string name, int length, int precision, int scale)
    {
        var type = SqlServerTypes.FromParameter(
            new SqlServerParameterRow("@p", name, length, precision, scale, null, null, null)
        );

        type.ShouldBe(new SqlServerType(name, length, precision, scale, null));
        type.BindName.ShouldBeNull();
        SqlServerTypes.Spelled(type).ShouldBe(name);
    }

    [Theory]
    [InlineData("bigint", 8, 19, 0)]
    [InlineData("decimal(18,4)", 9, 18, 4)]
    [InlineData("time(7)", 5, 16, 7)]
    [InlineData("datetime2(0)", 6, 19, 0)]
    [InlineData("nvarchar(50)", 100, 0, 0)]
    [InlineData("varchar(max)", -1, 0, 0)]
    [InlineData("timestamp", 8, 0, 0)]
    [InlineData("uniqueidentifier", 16, 0, 0)]
    [InlineData("xml", -1, 0, 0)]
    [InlineData("sql_variant", 8016, 0, 0)]
    public void FromDeclared_SystemType_IsTheNameAndFacetsVerbatim(string name, int length, int precision, int scale)
    {
        var type = SqlServerTypes.FromDeclared(
            new SqlServerDeclaredRow(1, name, length, precision, scale, null, null, null)
        );

        type.ShouldBe(new SqlServerType(name, length, precision, scale, null));
        type.BindName.ShouldBeNull();
    }

    // sysname is an alias of sys over nvarchar(128), so a message spells it by its system type.
    [Fact]
    public void FromParameter_Sysname_IsNvarcharWithTheUserTypeOfSys()
    {
        var type = SqlServerTypes.FromParameter(
            new SqlServerParameterRow("@p", "nvarchar(128)", 256, 0, 0, "sys", "sysname", null)
        );

        type.ShouldBe(new SqlServerType("nvarchar(128)", 256, 0, 0, new SqlServerUserType("sys", "sysname", null)));
        SqlServerTypes.Spelled(type).ShouldBe("nvarchar(128)");
    }

    [Fact]
    public void FromParameter_AliasType_HoldsItsSchemaAndName()
    {
        var type = SqlServerTypes.FromParameter(
            new SqlServerParameterRow("@email", "nvarchar(320)", 640, 0, 0, "dbo", "Email", null)
        );

        type.ShouldBe(new SqlServerType("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null)));
        SqlServerTypes.Spelled(type).ShouldBe("dbo.Email");
    }

    [Fact]
    public void FromDeclared_AliasType_HoldsItsSchemaAndName()
    {
        var type = SqlServerTypes.FromDeclared(
            new SqlServerDeclaredRow(1, "nvarchar(320)", 640, 0, 0, "dbo", "Email", null)
        );

        type.ShouldBe(new SqlServerType("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null)));
        SqlServerTypes.Spelled(type).ShouldBe("dbo.Email");
    }

    [Fact]
    public void FromDeclared_ClrType_HoldsItsAssemblyQualifiedName()
    {
        var name = "Microsoft.SqlServer.Types.SqlHierarchyId, " + TypesAssembly;

        var type = SqlServerTypes.FromDeclared(
            new SqlServerDeclaredRow(1, "hierarchyid", 892, 0, 0, "sys", "hierarchyid", name)
        );

        type.ShouldBe(new SqlServerType("hierarchyid", 892, 0, 0, new SqlServerUserType("sys", "hierarchyid", name)));
        SqlServerTypes.Spelled(type).ShouldBe("hierarchyid");
    }

    [Fact]
    public void FromParameter_ClrType_HoldsItsAssemblyQualifiedName()
    {
        var name = "Microsoft.SqlServer.Types.SqlHierarchyId, " + TypesAssembly;

        var type = SqlServerTypes.FromParameter(
            new SqlServerParameterRow("@h", "hierarchyid", 892, 0, 0, "sys", "hierarchyid", name)
        );

        type.UserType.ShouldBe(new SqlServerUserType("sys", "hierarchyid", name));
    }

    [Fact]
    public void FromParameter_RowWithoutAName_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() =>
            SqlServerTypes.FromParameter(new SqlServerParameterRow("@p", null, 4, 10, 0, null, null, null))
        );

    [Fact]
    public void FromDeclared_RowWithoutAName_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() =>
            SqlServerTypes.FromDeclared(new SqlServerDeclaredRow(1, null, 4, 10, 0, null, null, null))
        );

    // The server gives the facets of every type, so a row without one is a bug of the describer too.
    [Fact]
    public void FromParameter_RowWithoutAFacet_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() =>
            SqlServerTypes.FromParameter(new SqlServerParameterRow("@p", "int", 4, null, 0, null, null, null))
        );

    [Fact]
    public void FromDeclared_RowWithoutAFacet_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() =>
            SqlServerTypes.FromDeclared(new SqlServerDeclaredRow(1, "int", null, 10, 0, null, null, null))
        );
}
