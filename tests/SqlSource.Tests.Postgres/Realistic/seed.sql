-- The rows of the database "realistic": fixed values and no now(), so that every run sees the same rows and the
-- cases that execute a query over them get the same answers.  The ids of the accounts are the ones the server hands
-- out, 1 up in the order written here.  A case may add rows; it removes none of these.
--
-- Each nullable path of the schema is held by a row:
--   alice (1)  email, settings, orders, events in both partitions, a reservation
--   bob (2)    email and settings NULL; an order with no lines; events in 2024 alone; a reservation
--   carol (3)  closed, with closed_at; an order with no invoice; events in 2025 alone
--   dave (4)   no orders, no events, no reservations
--   erin (5)   no email, no settings, a NULL among her tags; no orders, no events, no reservations
-- Product 14 is in no order.  Event 7 has an array of items, one with no sku and one whose sku is no product; event
-- 8 is of an account 99 that is not in accounts, which events has no foreign key to.
-- Invoice 4, a second and unpaid one of order 3, has a NULL amount: the NOT NULL of the domain positive_amount is
-- not checked again for a NULL already typed as the domain, which the empty scalar subquery gives.

INSERT INTO accounts (name, email, status, tags, settings, created_at, closed_at) VALUES
    ('alice', 'alice@example.com', 'active', '{vip,beta}', '{"theme":"dark","limits":{"daily":5}}',
        '2024-01-01 09:00+00', NULL),
    ('bob', NULL, 'suspended', '{}', NULL, '2024-02-01 09:00+00', NULL),
    ('carol', 'carol@example.com', 'closed', '{beta}', '{"theme":"light"}',
        '2024-03-01 09:00+00', '2024-09-01 09:00+00'),
    ('dave', 'dave@example.com', 'active', '{}', NULL, '2024-04-01 09:00+00', NULL);

-- An element of an array can be NULL when the array cannot.
INSERT INTO accounts (name, email, status, tags, settings, created_at, closed_at) VALUES
    ('erin', NULL, 'active', '{trial,NULL}', NULL, '2024-05-01 09:00+00', NULL);

-- Three deep: 1 is the root, 2 its child, 3 the leaf.  4 is a second root with no children.
INSERT INTO categories (id, parent_id, name, slug) VALUES
    (1, NULL, 'Hardware', 'hardware'),
    (2, 1, 'Tools', 'tools'),
    (3, 2, 'Hand tools', 'hand-tools'),
    (4, NULL, 'Books', 'books');

-- 13 has no category; 12 is discontinued.
INSERT INTO products (id, category_id, sku, name, price, discontinued_on) VALUES
    (10, 3, 'HT-001', 'Hammer', 12.50, NULL),
    (11, 2, 'T-001', 'Drill', 89.00, NULL),
    (12, 3, 'HT-002', 'Chisel', 7.25, '2024-06-30'),
    (13, NULL, 'MISC-001', 'Gift card', 25.00, NULL);

-- 14 is in no order.
INSERT INTO products (id, category_id, sku, name, price, discontinued_on) VALUES
    (14, 2, 'T-002', 'Saw', 19.99, NULL);

-- Order 1 has every optional column set.  Order 2 has none, and 3 has no lines.  Order 4 has no invoice.
INSERT INTO orders (id, account_id, placed_at, shipped_at, note, coupon) VALUES
    (1, 1, '2024-05-01 10:00+00', '2024-05-03 10:00+00', 'leave at the door', 'SPRING'),
    (2, 1, '2024-06-01 10:00+00', NULL, NULL, NULL),
    (3, 2, '2024-07-01 10:00+00', NULL, NULL, NULL),
    (4, 3, '2024-08-01 10:00+00', '2024-08-02 10:00+00', 'gift', NULL);

INSERT INTO order_lines (order_id, line_no, product_id, quantity, unit_price) VALUES
    (1, 1, 10, 2, 12.50),
    (1, 2, 11, 1, 89.00),
    (2, 1, 12, 4, 7.25),
    (4, 1, 13, 1, 25.00);

-- Invoice 1 is paid, and 2 and 3 are not.
INSERT INTO billing.invoices (id, order_id, amount, issued_on, paid_on) VALUES
    (1, 1, 114.00, '2024-05-03', '2024-05-10'),
    (2, 2, 29.00, '2024-06-02', NULL),
    (3, 3, 10.00, '2024-07-02', NULL);

-- The domain's NOT NULL bypassed: amount is NULL.
INSERT INTO billing.invoices (id, order_id, amount, issued_on, paid_on) VALUES
    (4, 3, (SELECT i.amount FROM billing.invoices i WHERE false), '2024-07-15', NULL);

-- Alice has events in both partitions, bob in 2024 alone, carol in 2025 alone.  Event 3 has no "device" key, which
-- 1 and 2 have, and 6 has no payload.
INSERT INTO events (id, account_id, at, kind, payload) VALUES
    (1, 1, '2024-05-01 10:00+00', 'login', '{"ip":"10.0.0.1","device":"phone"}'),
    (2, 2, '2024-07-01 10:00+00', 'login', '{"ip":"10.0.0.2","device":"laptop"}'),
    (3, 1, '2024-11-01 10:00+00', 'purchase', '{"ip":"10.0.0.1"}'),
    (4, 1, '2025-02-01 10:00+00', 'login', '{"ip":"10.0.0.3","device":"phone"}'),
    (5, 3, '2025-03-01 10:00+00', 'logout', '{"ip":"10.0.0.4","device":"tablet"}'),
    (6, 3, '2025-04-01 10:00+00', 'logout', NULL);

-- Event 7's items: one with no sku, and one whose sku is no product.  Event 8's account 99 is not in accounts.
INSERT INTO events (id, account_id, at, kind, payload) VALUES
    (7, 1, '2025-05-01 10:00+00', 'purchase',
        '{"ip":"10.0.0.3","items":[{"sku":"HT-001","qty":2},{"sku":"BK-404","qty":1},{"gift_wrap":true}]}'),
    (8, 99, '2025-06-01 10:00+00', 'login', '{"ip":"10.0.0.9","device":"phone"}');

INSERT INTO reservations (id, account_id, during) VALUES
    (1, 1, '[2024-06-01 09:00+00, 2024-06-01 10:00+00)'),
    (2, 2, '[2024-06-01 09:30+00, 2024-06-01 11:00+00)');

-- The ids above are given, so the sequences are brought to them.
SELECT setval(pg_get_serial_sequence('orders', 'id'), (SELECT max(id) FROM orders));
SELECT setval(pg_get_serial_sequence('reservations', 'id'), (SELECT max(id) FROM reservations));
