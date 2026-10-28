using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Billing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of Queries/Billing/, run on the seed of the database "billing" through the methods that InvoiceQueries
// gets, into their sealed class models, whose timestamptz is a DateTime by the folder's type override, in an IList by
// the folder's collection type.  One test gives the method an open connection and the other a closed one, which the
// method opens.
[Collection(UsesPostgres.Name)]
public sealed class InvoiceQueryTests(PostgresDatabases postgres)
{
    [Fact]
    public async Task ListInvoices_EveryInvoice_ReadsTheTableModel()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.Billing(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var invoices = await connection.ListInvoices(cancellationToken: token);

        // The class has no equality of its own, so its elements are compared by their properties, apart from the
        // collection they are in.
        invoices
            .ToArray()
            .ShouldBeEquivalentTo(
                new[]
                {
                    new BillingInvoicesRow
                    {
                        Id = 1,
                        AccountId = 1,
                        Number = "INV-0001",
                        Amount = 120.50m,
                        IssuedOn = new DateOnly(2024, 1, 15),
                        PaidAt = new DateTime(2024, 1, 20, 10, 30, 0, DateTimeKind.Utc),
                    },
                    new BillingInvoicesRow
                    {
                        Id = 2,
                        AccountId = 1,
                        Number = "INV-0002",
                        Amount = 80.00m,
                        IssuedOn = new DateOnly(2024, 2, 15),
                        PaidAt = new DateTime(2024, 2, 18, 8, 15, 0, DateTimeKind.Utc),
                    },
                    new BillingInvoicesRow
                    {
                        Id = 3,
                        AccountId = 2,
                        Number = "INV-0003",
                        Amount = 45.25m,
                        IssuedOn = new DateOnly(2024, 3, 15),
                        PaidAt = null,
                    },
                }
            );
        invoices.Take(2).ShouldAllBe(static invoice => invoice.PaidAt!.Value.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetInvoice_OfTheUnpaidInvoice_ReadsTheInvoice()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.Billing(postgres);
        await using var connection = source.CreateConnection();

        var invoices = await connection.GetInvoice(new GetInvoiceArgs(3), cancellationToken: token);

        invoices
            .ShouldHaveSingleItem()
            .ShouldBeEquivalentTo(
                new GetInvoiceRow
                {
                    Id = 3,
                    Number = "INV-0003",
                    Amount = 45.25m,
                    PaidAt = null,
                }
            );
    }
}
