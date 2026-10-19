using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The reading of a case's models: the model the generator names from its sidecar entry, and its properties in their
// order with whether each can hold null.  It reads the compiled models and needs no server.
public sealed class RealisticModelsTests
{
    [Fact]
    public void PropertiesOf_Class_IsInDeclarationOrder() =>
        RealisticModels
            .PropertiesOf(typeof(Sample))
            .Select(static property => property.Name)
            .ShouldBe(["A", "B", "C", "D"]);

    [Fact]
    public void IsNullable_Property_IsWhetherItCanHoldNull() =>
        RealisticModels
            .PropertiesOf(typeof(Sample))
            .Select(RealisticModels.IsNullable)
            .ShouldBe([false, true, false, true]);

    [Fact]
    public void OutputModelOf_EntryThatMatchesATable_IsTheTableModel() =>
        RealisticModels.OutputModelOf(EntryOf("table-statement")).ShouldBe(typeof(BillingInvoicesDto));

    [Fact]
    public void OutputModelOf_Entry_IsTheQueryModel() =>
        RealisticModels.OutputModelOf(EntryOf("cte-chain")).ShouldBe(typeof(ListCustomerLifetimeValueDto));

    [Fact]
    public void OutputModelOf_EntryWithoutAModel_ThrowsNamingTheType()
    {
        var entry = EntryOf("cte-chain") with { Name = "Missing" };

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticModels.OutputModelOf(entry));

        thrown.Message.ShouldContain($"{typeof(RealisticModels).Namespace}.MissingDto");
    }

    [Fact]
    public void InputModelOf_EntryWithParameters_IsTheQueryModel() =>
        RealisticModels.InputModelOf(EntryOf("table-statement")).ShouldBe(typeof(ListLatestInvoicesArgs));

    [Fact]
    public void InputModelOf_EntryWithoutParameters_IsNull() =>
        RealisticModels.InputModelOf(EntryOf("cte-chain")).ShouldBeNull();

    private static Snapshot.SidecarEntry EntryOf(string name) =>
        RealisticModels.EntryOf(RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, name)));

    // A class with each kind of property a model of the generator has, which the tests read by reflection alone.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The tests read its properties by reflection."
    )]
    [SuppressMessage(
        "Major Code Smell",
        "S1144:Unused private types or members should be removed",
        Justification = "The tests read its properties by reflection."
    )]
    private sealed class Sample
    {
        public int A { get; init; }

        public int? B { get; init; }

        public string C { get; init; } = "";

        public string? D { get; init; }
    }
}
