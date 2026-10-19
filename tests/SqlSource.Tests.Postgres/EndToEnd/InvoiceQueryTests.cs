using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Tests.Postgres.EndToEnd.Billing;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The queries of Queries/Billing/, run on the seed of the database "billing" into their sealed class models, whose
// timestamptz is a DateTime by the folder's type override.
[Collection(UsesPostgres.Name)]
public sealed class InvoiceQueryTests(PostgresContainer postgres)
{
    [Fact]
    public async Task QueryAsync_ListInvoices_ReadsTheTableModel()
    {
        await using var source = EndToEndSources.Billing(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var invoices = (await connection.QueryAsync<BillingInvoicesRow>(InvoiceQueries.ListInvoices)).ToList();

        invoices.ShouldBeEquivalentTo(
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
            }.ToList()
        );
        invoices.Take(2).ShouldAllBe(static invoice => invoice.PaidAt!.Value.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task QuerySingleAsync_GetInvoice_ReadsTheUnpaidInvoice()
    {
        await using var source = EndToEndSources.Billing(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var invoice = await connection.QuerySingleAsync<GetInvoiceRow>(
            InvoiceQueries.GetInvoice,
            new GetInvoiceArgs(3)
        );

        invoice.ShouldBeEquivalentTo(
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
