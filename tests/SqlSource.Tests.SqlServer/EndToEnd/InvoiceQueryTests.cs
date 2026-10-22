using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Billing;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Billing/, run on the seed of the database "billing" into their sealed class models, whose date
// is a DateTime by the folder's type override.
[Collection(UsesSqlServer.Name)]
public sealed class InvoiceQueryTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task QueryAsync_ListInvoices_ReadsTheTableModel()
    {
        await using var connection = EndToEndSources.Billing(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

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
                    IssuedOn = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Unspecified),
                    PaidAt = new DateTime(2024, 1, 20, 10, 30, 0, DateTimeKind.Unspecified),
                },
                new BillingInvoicesRow
                {
                    Id = 2,
                    AccountId = 1,
                    Number = "INV-0002",
                    Amount = 80.00m,
                    IssuedOn = new DateTime(2024, 2, 15, 0, 0, 0, DateTimeKind.Unspecified),
                    PaidAt = new DateTime(2024, 2, 18, 8, 15, 0, DateTimeKind.Unspecified),
                },
                new BillingInvoicesRow
                {
                    Id = 3,
                    AccountId = 2,
                    Number = "INV-0003",
                    Amount = 45.25m,
                    IssuedOn = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Unspecified),
                    PaidAt = null,
                },
            }.ToList()
        );
    }

    [Fact]
    public async Task QuerySingleAsync_GetInvoice_ReadsTheUnpaidInvoice()
    {
        await using var connection = EndToEndSources.Billing(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

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
