-- The queries of the orders, at the default output, repository: each query gets its models, an instance method of
-- OrderRepository, which opens its connection with the factory that the repository holds, and a member of the
-- interface IOrderRepository.

-- name: ListOrdersOfUser
-- summary: The orders of one user, in the order of their ids.
SELECT id, status
FROM orders
WHERE user_id = @userId
ORDER BY id;

-- name: CancelOrder -> rowcount
-- summary: Cancels an order that has not shipped.
-- rowcount: the method returns 1 when it cancels the order, and 0 when the order has shipped or does not exist.
UPDATE orders
SET status = 'cancelled'
WHERE id = @orderId AND shipped_at IS NULL;
