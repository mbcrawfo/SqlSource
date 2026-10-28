using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// The reading of a case's models: the model the generator names from its sidecar entry, and the method of its claim at
// CodeGen.  It reads the compiled models and needs no server.  The tests name this project's own generated models and
// methods, which the other engine's project does not have, so they are not shared.
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

    [Fact]
    public void MethodOf_Entry_IsTheMethodOfItsCodeGenClaim()
    {
        var method = RealisticCases.Models.MethodOf(
            RealisticCases.Corpus,
            Case("category-tree-recursive"),
            EntryOf("category-tree-recursive")
        );

        method.DeclaringType.ShouldBe(typeof(CategoryTreeRecursiveMethodExtensions));
        method.Name.ShouldBe(nameof(CategoryTreeRecursiveMethodExtensions.ListCategoryTree));
    }

    [Fact]
    public void MethodOf_EntryWithoutAMethod_ThrowsNamingIt()
    {
        var entry = EntryOf("category-tree-recursive") with { Name = "Missing" };

        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticCases.Models.MethodOf(RealisticCases.Corpus, Case("category-tree-recursive"), entry)
        );

        thrown.Message.ShouldContain(nameof(CategoryTreeRecursiveMethodExtensions));
        thrown.Message.ShouldContain("of realistic/category-tree-recursive has no method Missing");
    }

    private static RealisticCase Case(string name) => RealisticCases.Corpus.ReadNamed(name);

    private static SidecarEntry EntryOf(string name) => RealisticModels.EntryOf(Case(name));
}
