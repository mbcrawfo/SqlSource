using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// A SQL Server type from a row of either procedure: the system type name and the facets verbatim, and the user type
// when the row names one.  The rows are those of the table of types in
// docs/superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md.
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

    // A column of a result set, as sys.dm_exec_describe_first_result_set describes it.
    [Theory]
    [InlineData("bigint", 8, 19, 0)]
    [InlineData("decimal(18,4)", 9, 18, 4)]
    [InlineData("nvarchar(50)", 100, 0, 0)]
    [InlineData("varchar(max)", -1, 0, 0)]
    [InlineData("timestamp", 8, 0, 0)]
    public void FromColumn_SystemType_IsTheNameAndFacetsVerbatim(string name, int length, int precision, int scale)
    {
        var type = SqlServerTypes.FromColumn(Column(name, length, precision, scale, null, null, null));

        type.ShouldBe(new SqlServerType(name, length, precision, scale, null));
        type.BindName.ShouldBeNull();
    }

    [Fact]
    public void FromColumn_AliasType_HoldsItsSchemaAndName()
    {
        var type = SqlServerTypes.FromColumn(Column("nvarchar(320)", 640, 0, 0, "dbo", "Email", null));

        type.ShouldBe(new SqlServerType("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null)));
        SqlServerTypes.Spelled(type).ShouldBe("dbo.Email");
    }

    [Fact]
    public void FromColumn_ClrType_HoldsItsAssemblyQualifiedName()
    {
        var name = "Microsoft.SqlServer.Types.SqlHierarchyId, " + TypesAssembly;

        var type = SqlServerTypes.FromColumn(Column("hierarchyid", 892, 0, 0, "sys", "hierarchyid", name));

        type.ShouldBe(new SqlServerType("hierarchyid", 892, 0, 0, new SqlServerUserType("sys", "hierarchyid", name)));
        SqlServerTypes.Spelled(type).ShouldBe("hierarchyid");
    }

    // A user type that is not of sys is declared by its schema and name, each in brackets: dbo.User does not declare,
    // since User is a keyword, and the server refuses it with 156.
    [Theory]
    [InlineData("dbo", "User", "[dbo].[User]")]
    [InlineData("dbo", "Email", "[dbo].[Email]")]
    [InlineData("my schema", "odd ]type", "[my schema].[odd ]]type]")]
    public void Declared_UserTypeNotOfSys_IsItsSchemaAndNameInBrackets(string schema, string name, string declared)
    {
        var type = new SqlServerType("nvarchar(50)", 100, 0, 0, new SqlServerUserType(schema, name, null));

        SqlServerTypes.Declared(type).ShouldBe(declared);
        SqlServerTypes.Spelled(type).ShouldBe(schema + "." + name);
    }

    // sysname and hierarchyid are user types of sys, declared by their system type names.
    [Fact]
    public void Declared_UserTypesOfSys_AreTheirSystemTypeNames()
    {
        var name = "Microsoft.SqlServer.Types.SqlHierarchyId, " + TypesAssembly;

        SqlServerTypes
            .Declared(new SqlServerType("nvarchar(128)", 256, 0, 0, new SqlServerUserType("sys", "sysname", null)))
            .ShouldBe("nvarchar(128)");
        SqlServerTypes
            .Declared(new SqlServerType("hierarchyid", 892, 0, 0, new SqlServerUserType("sys", "hierarchyid", name)))
            .ShouldBe("hierarchyid");
    }

    // In a case-sensitive database SYS is a schema of a user, apart from sys: the server reports its own as sys.
    [Fact]
    public void Declared_UserTypeOfASchemaSys_IsDeclaredByItsNames() =>
        SqlServerTypes
            .Declared(new SqlServerType("money", 8, 19, 4, new SqlServerUserType("SYS", "money2", null)))
            .ShouldBe("[SYS].[money2]");

    [Fact]
    public void Spelled_UserTypeOfASchemaSys_NamesTheUserType() =>
        SqlServerTypes
            .Spelled(new SqlServerType("money", 8, 19, 4, new SqlServerUserType("SYS", "money2", null)))
            .ShouldBe("SYS.money2");

    [Fact]
    public void Declared_SystemType_IsItsName() =>
        SqlServerTypes.Declared(new SqlServerType("decimal(18,4)", 9, 18, 4, null)).ShouldBe("decimal(18,4)");

    [Fact]
    public void FromColumn_RowWithoutAName_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() =>
            SqlServerTypes.FromColumn(Column(null, 4, 10, 0, null, null, null))
        );

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

    // A visible column of an expression with the type the test gives.
    private static SqlServerColumnRow Column(
        string? name,
        int length,
        int precision,
        int scale,
        string? userSchema,
        string? userName,
        string? assemblyQualifiedName
    ) =>
        new(
            false,
            1,
            "c",
            true,
            name,
            length,
            precision,
            scale,
            userSchema,
            userName,
            assemblyQualifiedName,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
}
