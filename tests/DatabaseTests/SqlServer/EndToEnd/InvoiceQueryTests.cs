using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Billing;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/Billing/, run through their generated methods on the seed of the database "billing" into their
// sealed class models, whose date is a DateTime by the folder's type override.
[Collection(UsesSqlServer.Name)]
public sealed class InvoiceQueryTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task ListInvoices_Seed_ReadsTheTableModel()
    {
        await using var connection = EndToEndSources.Billing(sqlServer);

        var invoices = (
            await connection.ListInvoices(cancellationToken: TestContext.Current.CancellationToken)
        ).ToList();

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
        invoices
            .Take(2)
            .ShouldAllBe(static invoice =>
                invoice.IssuedOn.Kind == DateTimeKind.Unspecified
                && invoice.PaidAt!.Value.Kind == DateTimeKind.Unspecified
            );
    }

    [Fact]
    public async Task GetInvoice_PaidInvoice_ReadsItsPayment()
    {
        await using var connection = EndToEndSources.Billing(sqlServer);

        var invoice = (
            await connection.GetInvoice(new GetInvoiceArgs(1), cancellationToken: TestContext.Current.CancellationToken)
        ).ShouldHaveSingleItem();

        invoice.ShouldBeEquivalentTo(
            new GetInvoiceRow
            {
                Id = 1,
                Number = "INV-0001",
                Amount = 120.50m,
                PaidAt = new DateTime(2024, 1, 20, 10, 30, 0, DateTimeKind.Unspecified),
            }
        );
        invoice.PaidAt.ShouldBe(new DateTime(2024, 1, 20, 10, 30, 0, DateTimeKind.Unspecified));
        invoice.PaidAt!.Value.Kind.ShouldBe(DateTimeKind.Unspecified);
    }

    [Fact]
    public async Task GetInvoice_UnpaidInvoice_ReadsANullPayment()
    {
        await using var connection = EndToEndSources.Billing(sqlServer);

        var invoice = (
            await connection.GetInvoice(new GetInvoiceArgs(3), cancellationToken: TestContext.Current.CancellationToken)
        ).ShouldHaveSingleItem();

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
