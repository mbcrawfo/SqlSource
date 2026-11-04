using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Billing;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Shapes;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The generated methods read by reflection: what each declares by its shape, its collection type, its suffix and its
// location.  It needs no server; MethodQueryTests, CollectionQueryTests and LocationQueryTests run the methods.
public sealed class MethodShapeTests
{
    // An extension method is internal by default, and a method on the type has the visibility of its marker.
    private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [Fact]
    public void TouchAccountQuietly_ShapeNone_ReturnsATask() =>
        typeof(MethodQueriesExtensions)
            .GetMethod(nameof(MethodQueriesExtensions.TouchAccountQuietly), Statics)
            .ShouldNotBeNull()
            .ReturnType.ShouldBe(typeof(Task));

    [Fact]
    public void CountNotesOfAsync_AsyncMethodSuffix_HasNoMethodWithoutTheSuffix()
    {
        _ = typeof(MethodQueriesExtensions)
            .GetMethod(nameof(MethodQueriesExtensions.CountNotesOfAsync), Statics)
            .ShouldNotBeNull();
        typeof(MethodQueriesExtensions).GetMethod("CountNotesOf", Statics).ShouldBeNull();
    }

    // Each query of Queries/Collections.sql, by its marker.
    [Theory]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsArray), typeof(CollectedAccount[]))]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsIEnumerable), typeof(IEnumerable<CollectedAccount>))]
    [InlineData(
        nameof(CollectionQueriesExtensions.ListAsIReadOnlyCollection),
        typeof(IReadOnlyCollection<CollectedAccount>)
    )]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsIReadOnlyList), typeof(IReadOnlyList<CollectedAccount>))]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsICollection), typeof(ICollection<CollectedAccount>))]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsIList), typeof(IList<CollectedAccount>))]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsList), typeof(List<CollectedAccount>))]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsImmutableArray), typeof(ImmutableArray<CollectedAccount>))]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsImmutableList), typeof(ImmutableList<CollectedAccount>))]
    [InlineData(nameof(CollectionQueriesExtensions.ListAsIImmutableList), typeof(IImmutableList<CollectedAccount>))]
    public void ReturnType_OfEachCollectionType_IsThatType(string method, Type rows) =>
        ReturnTypeOf(typeof(CollectionQueriesExtensions), method).ShouldBe(rows);

    // The most specific place that gives a collection type wins: a marker inside a query over its file header, the file
    // header over the settings files, and the folder's settings file over the project's.  Shapes.sql gives List in its
    // header and IReadOnlyList inside SearchAccounts.
    [Fact]
    public void ReturnType_OfEachLevel_IsThatLevels()
    {
        ReturnTypeOf(typeof(CollectionQueriesExtensions), nameof(CollectionQueriesExtensions.ListAsImmutableList))
            .ShouldBe(typeof(ImmutableList<CollectedAccount>));
        ReturnTypeOf(typeof(ShapeQueries), nameof(ShapeQueries.SearchAccounts))
            .ShouldBe(typeof(IReadOnlyList<SearchAccountsView>));
        ReturnTypeOf(typeof(ShapeQueries), nameof(ShapeQueries.ListAccountColumns))
            .ShouldBe(typeof(List<ListAccountColumnsView>));
        ReturnTypeOf(typeof(InvoiceQueriesExtensions), nameof(InvoiceQueriesExtensions.ListInvoices))
            .ShouldBe(typeof(IList<BillingInvoicesRow>));
        ReturnTypeOf(typeof(AccountQueriesExtensions), nameof(AccountQueriesExtensions.ListAccounts))
            .ShouldBe(typeof(IReadOnlyList<AccountsDto>));
    }

    [Fact]
    public void Method_OfEachLocation_HasItsAccessibility()
    {
        var extension = typeof(AccountQueriesExtensions).GetMethod(
            nameof(AccountQueriesExtensions.GetAccountName),
            Statics
        );
        extension.ShouldNotBeNull().IsAssembly.ShouldBeTrue();
        extension.IsDefined(typeof(ExtensionAttribute)).ShouldBeTrue();

        var onPublic = typeof(ShapeQueries).GetMethod(nameof(ShapeQueries.SearchAccounts));
        onPublic.ShouldNotBeNull().IsPublic.ShouldBeTrue();
        onPublic.IsDefined(typeof(ExtensionAttribute)).ShouldBeFalse();

        var onInternal = typeof(LocationQueries).GetMethod(nameof(LocationQueries.GetLocatedNameInternal), Statics);
        onInternal.ShouldNotBeNull().IsAssembly.ShouldBeTrue();
        onInternal.IsDefined(typeof(ExtensionAttribute)).ShouldBeFalse();

        var onPrivate = typeof(LocationQueries).GetMethod("GetLocatedNamePrivate", Statics);
        onPrivate.ShouldNotBeNull().IsPrivate.ShouldBeTrue();
        onPrivate.IsDefined(typeof(ExtensionAttribute)).ShouldBeFalse();

        var nested = typeof(LocationQueries.Sql);
        nested.IsNestedAssembly.ShouldBeTrue();
        var inNested = nested.GetMethod(nameof(LocationQueries.Sql.GetLocatedNameNested));
        inNested.ShouldNotBeNull().IsPublic.ShouldBeTrue();
        inNested.IsDefined(typeof(ExtensionAttribute)).ShouldBeFalse();
    }

    [Fact]
    public void ExtensionClass_OfATypeWithNoExtensionMethod_IsNotGenerated()
    {
        var assembly = typeof(MethodShapeTests).Assembly;

        _ = assembly.GetType(typeof(AccountQueries).FullName + "Extensions").ShouldNotBeNull();
        _ = assembly.GetType(typeof(LocationQueries).FullName + "Extensions").ShouldNotBeNull();
        assembly.GetType(typeof(ShapeQueries).FullName + "Extensions").ShouldBeNull();
    }

    // The type of the rows that a method declares, which it returns as a Task of it.
    private static Type ReturnTypeOf(Type type, string method)
    {
        var returned = type.GetMethod(method, Statics).ShouldNotBeNull().ReturnType;
        returned.GetGenericTypeDefinition().ShouldBe(typeof(Task<>));
        return returned.GetGenericArguments().ShouldHaveSingleItem();
    }
}
