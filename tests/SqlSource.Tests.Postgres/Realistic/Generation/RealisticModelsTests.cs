using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The reading of a case's models: the model the generator names from its sidecar entry.  It reads the compiled models
// and needs no server.
public sealed class RealisticModelsTests
{
    [Fact]
    public void OutputModelOf_EntryThatMatchesATable_IsTheTableModel() =>
        RealisticCases.Models.OutputModelOf(EntryOf("table-statement")).ShouldBe(typeof(BillingInvoicesDto));

    [Fact]
    public void OutputModelOf_Entry_IsTheQueryModel() =>
        RealisticCases.Models.OutputModelOf(EntryOf("cte-chain")).ShouldBe(typeof(ListCustomerLifetimeValueDto));

    [Fact]
    public void OutputModelOf_EntryWithoutAModel_ThrowsNamingTheType()
    {
        var entry = EntryOf("cte-chain") with { Name = "Missing" };

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.Models.OutputModelOf(entry));

        thrown.Message.ShouldContain($"{typeof(RealisticModelsTests).Namespace}.MissingDto");
    }

    [Fact]
    public void InputModelOf_EntryWithParameters_IsTheQueryModel() =>
        RealisticCases.Models.InputModelOf(EntryOf("table-statement")).ShouldBe(typeof(ListLatestInvoicesArgs));

    [Fact]
    public void InputModelOf_EntryWithoutParameters_IsNull() =>
        RealisticCases.Models.InputModelOf(EntryOf("cte-chain")).ShouldBeNull();

    private static SidecarEntry EntryOf(string name) => RealisticModels.EntryOf(RealisticCases.Corpus.ReadNamed(name));
}
