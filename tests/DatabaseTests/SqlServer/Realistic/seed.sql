-- The rows of the database "realistic": fixed values, and no SYSUTCDATETIME() but where the server fills a period of
-- dbo.prices, so that every run sees the same rows and the cases that execute a query over them get the same answers.
-- The ids are given, through IDENTITY_INSERT, so that a comment or a sample can name them; an identity then goes on
-- from the highest.  The numbers of the invoices are the sequence's, from 1000.  A case may add rows; it removes none
-- of these.  Every batch runs on SQL Server 2017.
--
-- Each nullable path of the schema is held by a row:
--   Ana (1)      every contact field set; orders 1, 2, 6, 13 and 15; a cart she has not checked out
--   Ben (2)      no company; orders 3, 4, 8, 11 and 14; a cart that became order 8
--   Chen (3)     suspended; no email and no time zone; orders 5, cancelled, and 10; an open cart
--   Dana (4)     closed, with closed_at; no phone and no company; order 7
--   Eli (5)      no phone; no order and no cart
--   Fatima (6)   no email, phone, company or time zone; no order, and a cart with no items
--   Gus (7)      closed, with closed_at; no company; orders 9 and 12
-- Eli and Fatima have no order, so dbo.customer_last_orders gives them a NULL order_id, placed_at and status.
-- Category 1, 5 and 6 are roots, with a NULL parent_id; 6 has no product, and 3 is two below its root.  Product 6
-- has no category, no discount, no attributes and no stock row; 7 is in no order.  Order 8 has no ship_name,
-- ship_city or shipped_at; order 10 a ship_name and no ship_city; order 15 has no lines.  Orders 14 and 15 have no
-- invoice.  Invoice 13 has no payment, and 5, voided, has none either; invoice 3 has two.  Invoices 8 and 12 have no
-- due_at, and 8 and 10 no tax.  Payment 8 has no reference, and 4's is numeric but over the range of int.

SET IDENTITY_INSERT dbo.customers ON;

INSERT INTO dbo.customers (id, name, email, phone, company, status, time_zone, created_at, closed_at) VALUES
    (1, N'Ana Ruiz', N'ana.ruiz@example.com', '+44 20 7946 0101', N'Ruiz Design Ltd', 'active',
        'GMT Standard Time', '2024-01-10 09:00:00', NULL),
    (2, N'Ben Okafor', N'ben.okafor@example.com', '+1 202 555 0142', NULL, 'active',
        'Eastern Standard Time', '2024-01-22 14:30:00', NULL),
    (3, N'Chen Wei', NULL, '+44 161 496 0177', N'Bletchley Supplies', 'suspended',
        NULL, '2024-02-05 11:15:00', NULL),
    (4, N'Dana Kowalski', N'dana.k@example.com', NULL, NULL, 'closed',
        'Pacific Standard Time', '2024-03-01 16:45:00', '2024-09-30 17:00:00'),
    (5, N'Eli Haddad', N'eli@example.com', NULL, N'Haddad & Sons', 'active',
        'Tokyo Standard Time', '2024-04-12 08:20:00', NULL),
    (6, N'Fatima Noor', NULL, NULL, NULL, 'active',
        NULL, '2024-10-19 12:55:00', NULL),
    (7, N'Gus Lindqvist', N'gus@example.se', '+46 8 123 456 78', NULL, 'closed',
        'W. Europe Standard Time', '2024-07-30 10:00:00', '2024-10-20 12:00:00');

SET IDENTITY_INSERT dbo.customers OFF;

-- Electronics > Computers > Laptops and > Accessories; Home & Kitchen; Garden, which has no product.
INSERT INTO dbo.categories (id, parent_id, name) VALUES
    (1, NULL, N'Electronics'),
    (5, NULL, N'Home & Kitchen'),
    (6, NULL, N'Garden');

INSERT INTO dbo.categories (id, parent_id, name) VALUES
    (2, 1, N'Computers');

INSERT INTO dbo.categories (id, parent_id, name) VALUES
    (3, 2, N'Laptops'),
    (4, 2, N'Accessories');

-- Product 3's attributes have no "color", and 5's and 6's are NULL.  4 is discontinued.  6, a gift card, has no
-- category and no discount; 2, 4 and 7 have no discount either.
SET IDENTITY_INSERT dbo.products ON;

INSERT INTO dbo.products (id, sku, name, category_id, list_price, discount_pct, discontinued_at, attributes) VALUES
    (1, 'LAP-001', N'Ultrabook 14', 3, 1299.00, 10.00, NULL, N'{"color":"silver","weight_kg":1.2}'),
    (2, 'LAP-002', N'Workstation 16', 3, 2399.00, NULL, NULL, N'{"color":"black","ram_gb":32}'),
    (3, 'ACC-001', N'USB-C Hub', 4, 49.90, 15.00, NULL, N'{"ports":7}'),
    (4, 'ACC-002', N'Wireless Mouse', 4, 24.50, NULL, '2024-08-31', N'{"color":"white","buttons":5}'),
    (5, 'KIT-001', N'Espresso Machine', 5, 349.00, 5.00, NULL, NULL),
    (6, 'GFT-050', N'Gift Card 50', NULL, 50.00, NULL, NULL, NULL),
    (7, 'ACC-003', N'Laptop Sleeve', 4, 29.00, NULL, NULL, N'{"color":"navy","sizes":[13,14,16]}'),
    (8, 'KIT-002', N'Electric Kettle', 5, 59.00, 20.00, NULL, N'{"color":"steel","litres":1.7}');

SET IDENTITY_INSERT dbo.products OFF;

-- Product 6 has no stock row.  4, discontinued, and 7 have no reorder_at.
INSERT INTO dbo.stock (product_id, on_hand, reorder_at, updated_at) VALUES
    (1, 14, 5, '2024-10-15 06:00:00'),
    (2, 3, 2, '2024-10-15 06:00:00'),
    (3, 120, 25, '2024-10-15 06:00:00'),
    (4, 0, NULL, '2024-09-01 06:00:00'),
    (5, 8, 3, '2024-10-15 06:00:00'),
    (7, 40, NULL, '2024-10-15 06:00:00'),
    (8, 22, 10, '2024-10-15 06:00:00');

-- Order 8, a gift card sent by email, has no ship_name, ship_city or shipped_at; order 10 has a ship_name and no
-- ship_city.  Orders 5, cancelled, and 13 to 15, pending, were never shipped.  Most have no note.
SET IDENTITY_INSERT dbo.orders ON;

INSERT INTO dbo.orders (id, customer_id, placed_at, status, ship_name, ship_city, shipped_at, note) VALUES
    (1, 1, '2024-02-01 10:15:00', 'delivered', N'Ana Ruiz', N'London', '2024-02-02 09:00:00',
        N'Leave with the concierge'),
    (2, 1, '2024-03-14 16:40:00', 'delivered', N'Ana Ruiz', N'London', '2024-03-15 08:30:00', NULL),
    (3, 2, '2024-03-20 11:05:00', 'delivered', N'Ben Okafor', N'Washington', '2024-03-22 14:00:00', NULL),
    (4, 2, '2024-05-02 08:30:00', 'delivered', N'Mia Okafor', N'Baltimore', '2024-05-03 10:00:00',
        N'Gift, no prices on the slip'),
    (5, 3, '2024-05-18 19:45:00', 'cancelled', N'Chen Wei', N'Manchester', NULL, N'Cancelled by phone'),
    (6, 1, '2024-06-07 13:20:00', 'delivered', N'Ana Ruiz', N'Oxford', '2024-06-08 11:00:00', NULL),
    (7, 4, '2024-06-30 09:10:00', 'delivered', N'Dana Kowalski', N'Seattle', '2024-07-01 15:30:00', NULL),
    (8, 2, '2024-07-11 17:55:00', 'delivered', NULL, NULL, NULL, N'Gift card sent by email'),
    (9, 7, '2024-08-03 12:00:00', 'delivered', N'Gus Lindqvist', N'Stockholm', '2024-08-05 09:45:00', NULL),
    (10, 3, '2024-08-19 20:10:00', 'shipped', N'Bletchley Supplies', NULL, '2024-08-21 07:50:00',
        N'No city given; the courier has the postcode'),
    (11, 2, '2024-09-02 09:00:00', 'shipped', N'Ben Okafor', N'Washington', '2024-09-03 16:20:00', NULL),
    (12, 7, '2024-09-15 14:30:00', 'delivered', N'Gus Lindqvist', N'Uppsala', '2024-09-17 12:00:00', NULL),
    (13, 1, '2024-10-01 18:05:00', 'pending', N'Ana Ruiz', N'London', NULL, NULL),
    (14, 2, '2024-10-12 10:40:00', 'pending', N'Ben Okafor', N'Washington', NULL, NULL),
    (15, 1, '2024-10-14 21:15:00', 'pending', NULL, NULL, NULL, N'Draft, no lines yet');

SET IDENTITY_INSERT dbo.orders OFF;

-- Each unit_price is the product's sale price when the order was placed.  Order 15 has no lines, and product 7 is in
-- no order.
INSERT INTO dbo.order_lines (order_id, line_no, product_id, quantity, unit_price) VALUES
    (1, 1, 1, 1, 1169.10),
    (1, 2, 3, 1, 42.42),
    (2, 1, 4, 2, 24.50),
    (3, 1, 2, 1, 2399.00),
    (3, 2, 4, 1, 24.50),
    (3, 3, 3, 2, 42.42),
    (4, 1, 5, 1, 331.55),
    (5, 1, 8, 1, 47.20),
    (6, 1, 3, 1, 42.42),
    (6, 2, 8, 2, 47.20),
    (7, 1, 1, 1, 1169.10),
    (7, 2, 4, 1, 24.50),
    (8, 1, 6, 2, 50.00),
    (9, 1, 5, 1, 331.55),
    (10, 1, 3, 4, 42.42),
    (11, 1, 8, 1, 47.20),
    (11, 2, 3, 1, 42.42),
    (12, 1, 2, 1, 2399.00),
    (13, 1, 1, 1, 1169.10),
    (14, 1, 5, 2, 331.55);

-- An invoice for each of orders 1 to 13, of the order's total.  Orders 14 and 15 have none.  Invoice 5, of the
-- cancelled order, is voided.  Invoice 8 was paid at checkout and 12 is due on receipt, so neither has a due_at; 8,
-- of gift cards, and 10, to a business, have no tax.
SET IDENTITY_INSERT billing.invoices ON;

INSERT INTO billing.invoices (id, order_id, issued_at, due_at, amount, tax, voided_at) VALUES
    (1, 1, '2024-02-01', '2024-03-02', 1211.52, 201.92, NULL),
    (2, 2, '2024-03-14', '2024-04-13', 49.00, 8.17, NULL),
    (3, 3, '2024-03-20', '2024-04-19', 2508.34, 150.50, NULL),
    (4, 4, '2024-05-02', '2024-06-01', 331.55, 19.89, NULL),
    (5, 5, '2024-05-18', '2024-06-17', 47.20, 7.87, '2024-05-19 09:00:00'),
    (6, 6, '2024-06-07', '2024-07-07', 136.82, 22.80, NULL),
    (7, 7, '2024-06-30', '2024-07-30', 1193.60, 121.75, NULL),
    (8, 8, '2024-07-11', NULL, 100.00, NULL, NULL),
    (9, 9, '2024-08-03', '2024-09-02', 331.55, 66.31, NULL),
    (10, 10, '2024-08-19', '2024-09-18', 169.68, NULL, NULL),
    (11, 11, '2024-09-02', '2024-10-02', 89.62, 5.38, NULL),
    (12, 12, '2024-09-15', NULL, 2399.00, 479.80, NULL),
    (13, 13, '2024-10-01', '2024-10-31', 1169.10, 194.85, NULL);

SET IDENTITY_INSERT billing.invoices OFF;

-- Invoice 3 is paid in two parts and 12 in part; 5, voided, and 13 have no payment.  A transfer's reference is
-- numeric: 4's is over the range of int, and 10's has leading zeros.  A card's is not, and a voucher has none.
SET IDENTITY_INSERT billing.payments ON;

INSERT INTO billing.payments (id, invoice_id, paid_at, amount, method, reference) VALUES
    (1, 1, '2024-02-01 10:16:00', 1211.52, 'card', N'AUTH-58213'),
    (2, 2, '2024-03-20 09:00:00', 49.00, 'transfer', N'48213977'),
    (3, 3, '2024-03-20 11:06:00', 1000.00, 'card', N'AUTH-60117'),
    (4, 3, '2024-04-15 08:00:00', 1508.34, 'transfer', N'20240415001'),
    (5, 4, '2024-05-02 08:31:00', 331.55, 'card', N'AUTH-61540'),
    (6, 6, '2024-06-20 12:00:00', 136.82, 'transfer', N'51190042'),
    (7, 7, '2024-07-25 10:00:00', 1193.60, 'card', N'AUTH-64002'),
    (8, 8, '2024-07-11 17:56:00', 100.00, 'voucher', NULL),
    (9, 9, '2024-08-03 12:01:00', 331.55, 'card', N'AUTH-66310'),
    (10, 10, '2024-09-10 09:30:00', 169.68, 'transfer', N'00071234'),
    (11, 11, '2024-09-02 09:01:00', 89.62, 'paypal', N'PAYID-M3XQ7'),
    (12, 12, '2024-10-01 10:00:00', 1200.00, 'transfer', N'52000981');

SET IDENTITY_INSERT billing.payments OFF;

-- The price list, one row for each product.  Its periods are the server's: valid_from is when this batch ran.
INSERT INTO dbo.prices (product_id, price) VALUES
    (1, 1299.00),
    (2, 2399.00),
    (3, 49.90),
    (4, 24.50),
    (5, 349.00),
    (6, 50.00),
    (7, 29.00),
    (8, 59.00);

-- Cart ...3301 is Ana's, not checked out.  ...3302 is Ben's, checked out as order 8.  ...3303 is a guest's, with no
-- customer, and ...3304 is Fatima's, with no items.  ...3305 is Chen's, left open with no items, so a customer with
-- orders and an open cart has no email and no time zone.
INSERT INTO dbo.carts (id, customer_id, created_at, checked_out_at) VALUES
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3301', 1, '2024-10-15 08:00:00', NULL),
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3302', 2, '2024-07-11 17:40:00', '2024-07-11 17:55:00'),
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3303', NULL, '2024-10-18 22:10:00', NULL),
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3304', 6, '2024-10-19 13:00:00', NULL),
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3305', 3, '2024-09-20 15:30:00', NULL);

INSERT INTO dbo.cart_items (cart_id, product_id, quantity, added_at) VALUES
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3301', 7, 1, '2024-10-15 08:01:00'),
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3301', 3, 2, '2024-10-15 08:03:00'),
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3302', 6, 2, '2024-07-11 17:42:00'),
    ('3F2504E0-4F89-41D3-9A0C-0305E82C3303', 8, 1, '2024-10-18 22:11:00');

-- The specifications, on a server that has dbo.product_specs: products 1 and 2 have one, 5 a NULL one, and the others
-- no row.
IF CAST(SERVERPROPERTY('ProductMajorVersion') AS int) >= 17
    EXEC (N'
INSERT INTO dbo.product_specs (product_id, spec) VALUES
    (1, N''{"cpu":"8-core","ram_gb":16,"ports":["usb-c","hdmi"]}''),
    (2, N''{"cpu":"16-core","ram_gb":32,"gpu":"workstation"}''),
    (5, NULL);');
GO

-- Two prices change after the list was made, so dbo.prices_history holds the old ones.  The pause gives the old rows
-- a period that is not empty, which FOR SYSTEM_TIME would otherwise pass over.
WAITFOR DELAY '00:00:00.100';

UPDATE dbo.prices SET price = 1249.00 WHERE product_id = 1;

UPDATE dbo.prices SET price = 329.00 WHERE product_id = 5;
