using System.Collections.Generic;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Billing;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Billing/Invoices.sql, claimed by InvoiceQueries, on the database billing: its two queries return lists, which
// refuse no count, and neither can raise an error.
internal static class InvoicesScenarios
{
    private static readonly Setup Billing = new(Source: "billing");

    private static readonly Setup BillingClosed = Billing with { Connection = ConnectionStart.Closed };

    private static readonly Setup BillingInTransaction = Billing with { Transaction = CallerTransaction.Begun };

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(InvoiceQueriesExtensions);
            return
            [
                Of(host, "ListInvoices", ScenarioTag.Ordinary, "open", Billing),
                Of(host, "ListInvoices", ScenarioTag.Ordinary, "closed", BillingClosed),
                Of(host, "ListInvoices", ScenarioTag.Ordinary, "in a transaction", BillingInTransaction),
                Of(host, "GetInvoice", ScenarioTag.Ordinary, "paid, open", Billing, new GetInvoiceArgs(1)),
                Of(host, "GetInvoice", ScenarioTag.Ordinary, "unpaid, closed", BillingClosed, new GetInvoiceArgs(3)),
                Of(host, "GetInvoice", ScenarioTag.Other, "no invoice, closed", BillingClosed, new GetInvoiceArgs(99)),
            ];
        }
    }
}
