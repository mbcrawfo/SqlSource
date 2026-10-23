using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// The reading of a case's models: the model the generator names from its sidecar entry.  It reads the compiled models
// and needs no server.  The tests of OutputModelOf and InputModelOf name this project's own generated models, which the
// other engine's project does not have, so they are not shared.
public sealed class RealisticModelsTests
{
    [Fact]
    public void OutputModelOf_EntryThatMatchesATable_IsTheTableModel() =>
        RealisticCases.Models.OutputModelOf(EntryOf("insert-customer-output")).ShouldBe(typeof(CustomersDto));

    [Fact]
    public void OutputModelOf_Entry_IsTheQueryModel() =>
        RealisticCases.Models.OutputModelOf(EntryOf("categories-right-join-products")).ShouldBe(typeof(ListCatalogDto));

    [Fact]
    public void OutputModelOf_EntryWithoutAModel_ThrowsNamingTheType()
    {
        var entry = EntryOf("categories-right-join-products") with { Name = "Missing" };

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.Models.OutputModelOf(entry));

        thrown.Message.ShouldContain($"{typeof(RealisticModelsTests).Namespace}.MissingDto");
    }

    [Fact]
    public void InputModelOf_EntryWithParameters_IsTheQueryModel() =>
        RealisticCases.Models.InputModelOf(EntryOf("insert-customer-output")).ShouldBe(typeof(CreateCustomerArgs));

    [Fact]
    public void InputModelOf_EntryWithoutParameters_IsNull() =>
        RealisticCases.Models.InputModelOf(EntryOf("categories-right-join-products")).ShouldBeNull();

    private static SidecarEntry EntryOf(string name) => RealisticModels.EntryOf(RealisticCases.Corpus.ReadNamed(name));
}
