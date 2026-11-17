using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlServerExamples.Repository;

// Code that uses OrderRepository through its generated interface, IOrderRepository, so that a test of it can pass a
// fake in place of the repository and needs no database or connection.
public sealed class OrderDesk(IOrderRepository orders)
{
    public async Task<string[]> DescribeOrdersAsync(int userId, CancellationToken cancellationToken = default)
    {
        var ofUser = await orders
            .ListOrdersOfUser(new ListOrdersOfUserParams(userId), cancellationToken)
            .ConfigureAwait(false);
        return [.. ofUser.Select(static order => $"Order {order.Id}: {order.Status}")];
    }

    // CancelOrder is rowcount: 1 when it cancelled the order, and 0 when the order has shipped or does not exist.
    public async Task<bool> CancelAsync(int orderId, CancellationToken cancellationToken = default) =>
        await orders.CancelOrder(new CancelOrderParams(orderId), cancellationToken).ConfigureAwait(false) == 1;
}
