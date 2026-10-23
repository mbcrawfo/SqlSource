using System;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// The reading of a case's models: the model the generator names from its sidecar entry.  It reads the compiled models
// and needs no server.
public sealed class RealisticModelsTests
{
    [Fact]
    public void OutputModelOf_EntryThatMatchesATable_IsTheTableModel() =>
        RealisticModels.OutputModelOf(EntryOf("insert-customer-output")).ShouldBe(typeof(CustomersDto));

    [Fact]
    public void OutputModelOf_Entry_IsTheQueryModel() =>
        RealisticModels.OutputModelOf(EntryOf("categories-right-join-products")).ShouldBe(typeof(ListCatalogDto));

    [Fact]
    public void OutputModelOf_EntryWithoutAModel_ThrowsNamingTheType()
    {
        var entry = EntryOf("categories-right-join-products") with { Name = "Missing" };

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticModels.OutputModelOf(entry));

        thrown.Message.ShouldContain($"{typeof(RealisticModels).Namespace}.MissingDto");
    }

    [Fact]
    public void InputModelOf_EntryWithParameters_IsTheQueryModel() =>
        RealisticModels.InputModelOf(EntryOf("insert-customer-output")).ShouldBe(typeof(CreateCustomerArgs));

    [Fact]
    public void InputModelOf_EntryWithoutParameters_IsNull() =>
        RealisticModels.InputModelOf(EntryOf("categories-right-join-products")).ShouldBeNull();

    private static SidecarEntry EntryOf(string name) => RealisticModels.EntryOf(RealisticCases.ReadNamed(name));
}
