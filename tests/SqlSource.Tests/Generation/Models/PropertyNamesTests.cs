using System;
using Shouldly;
using SqlSource.Generation.Models;
using SqlSource.Settings;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Models;

public sealed class PropertyNamesTests
{
    [Theory]
    [InlineData("created_at", "CreatedAt")]
    [InlineData("userId", "UserId")]
    [InlineData("ID", "ID")]
    [InlineData("api_v2_url", "ApiV2Url")]
    [InlineData("café", "Café")]
    [InlineData("名前", "名前")]
    [InlineData("_a__b_", "AB")]
    [InlineData("2fa", "2fa")]
    [InlineData("___", "")]
    [InlineData("a!", "A")]
    [InlineData("b?", "B")]
    [InlineData("c!?", "C!")]
    public void PascalCase_Name_IsPascalCase(string name, string expected) =>
        PropertyNames.PascalCase(name).ShouldBe(expected);

    // A name that is already PascalCase is given back as it is, with no new string.
    [Fact]
    public void PascalCase_NameAlreadyPascalCase_IsTheSameString()
    {
        const string Name = "CreatedAt";

        PropertyNames.PascalCase(Name).ShouldBeSameAs(Name);
    }

    [Theory]
    [InlineData("2fa", false)]
    [InlineData("", false)]
    [InlineData("Api-Url", false)]
    [InlineData("Café", true)]
    [InlineData("Id", true)]
    public void IsIdentifier_Name_IsWhetherItIsACSharpIdentifier(string name, bool expected) =>
        PropertyNames.IsIdentifier(name).ShouldBe(expected);

    [Theory]
    [InlineData("EqualityContract")]
    [InlineData("Equals")]
    [InlineData("GetHashCode")]
    [InlineData("ToString")]
    [InlineData("GetType")]
    [InlineData("MemberwiseClone")]
    [InlineData("Finalize")]
    [InlineData("ReferenceEquals")]
    [InlineData("PrintMembers")]
    [InlineData("Deconstruct")]
    [InlineData("Clone")]
    public void IsReserved_EachName_IsTrue(string name) => PropertyNames.IsReserved(name).ShouldBeTrue();

    [Theory]
    [InlineData("Id")]
    [InlineData("equals")]
    public void IsReserved_Name_IsFalse(string name) => PropertyNames.IsReserved(name).ShouldBeFalse();

    [Theory]
    [InlineData("public", "users", "Users")]
    [InlineData("public", "order_lines", "OrderLines")]
    [InlineData("billing", "invoices", "BillingInvoices")]
    [InlineData("Billing_Admin", "audit_log", "BillingAdminAuditLog")]
    [InlineData("Public", "users", "PublicUsers")]
    [InlineData("PUBLIC", "users", "PUBLICUsers")]
    public void FromTable_Table_IsTheSchemaUnlessItIsPublicThenTheTable(string schema, string table, string expected) =>
        PropertyNames.FromTable(new SidecarTable(schema, table), DatabaseEngine.PostgreSql).ShouldBe(expected);

    // The schema that is dropped is the engine's: public on PostgreSQL and dbo on SQL Server, each compared ordinally.
    [Theory]
    [InlineData("dbo", "orders", "SqlServer", "Orders")]
    [InlineData("dbo", "orders", "PostgreSql", "DboOrders")]
    [InlineData("public", "orders", "SqlServer", "PublicOrders")]
    [InlineData("public", "orders", "PostgreSql", "Orders")]
    [InlineData("billing", "invoices", "SqlServer", "BillingInvoices")]
    [InlineData("billing", "invoices", "PostgreSql", "BillingInvoices")]
    [InlineData("DBO", "orders", "SqlServer", "DBOOrders")]
    [InlineData("Dbo", "orders", "SqlServer", "DboOrders")]
    public void FromTable_TableOfAnEngine_DropsTheSchemaOfThatEngineAlone(
        string schema,
        string table,
        string engine,
        string expected
    ) =>
        PropertyNames.FromTable(new SidecarTable(schema, table), Enum.Parse<DatabaseEngine>(engine)).ShouldBe(expected);

    [Theory]
    [InlineData(null, "users")]
    [InlineData("public", null)]
    [InlineData(null, null)]
    [InlineData("public", "order-lines")]
    [InlineData("public", "")]
    [InlineData("public", "___")]
    [InlineData("public", "2fa")]
    [InlineData("my-schema", "orders")]
    [InlineData("", "orders")]
    [InlineData("2nd", "orders")]
    [InlineData("public", "users?")]
    [InlineData("public", "users!")]
    [InlineData("billing?", "invoices")]
    [InlineData("billing!", "invoices")]
    public void FromTable_PartThatGivesNoIdentifier_IsNull(string? schema, string? table) =>
        PropertyNames.FromTable(new SidecarTable(schema, table), DatabaseEngine.PostgreSql).ShouldBeNull();

    // The public schema gives no name, so it is not checked: a table whose name is an identifier needs no schema.
    [Fact]
    public void FromTable_PublicSchema_NeedsNothingOfTheSchema() =>
        PropertyNames.FromTable(new SidecarTable("public", "t"), DatabaseEngine.PostgreSql).ShouldBe("T");
}
