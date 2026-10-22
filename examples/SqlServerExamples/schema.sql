-- The schema of the example, with a few rows: users, their orders and the lines of each order.
-- The service "schema" of docker-compose.yml runs it in the database examples when it creates that database, and
-- tools/describe-end-to-end.sh in a database of its own before it describes the queries.  A change to it is followed
-- by a run of that script, which writes the sidecars again.

CREATE TABLE users (
    id int IDENTITY PRIMARY KEY,
    name nvarchar(100) NOT NULL,
    email nvarchar(200) NOT NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- shipped_on is null until the order ships.
CREATE TABLE orders (
    id int IDENTITY PRIMARY KEY,
    user_id int NOT NULL REFERENCES users (id),
    status varchar(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'shipped', 'cancelled')),
    placed_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    shipped_on date NULL
);

CREATE TABLE order_lines (
    order_id int NOT NULL REFERENCES orders (id),
    line_number int NOT NULL,
    product nvarchar(100) NOT NULL,
    quantity int NOT NULL,
    unit_price decimal(10, 2) NOT NULL,
    PRIMARY KEY (order_id, line_number)
);

INSERT INTO users (name, email, created_at) VALUES
(N'Ada', N'ada@example.com', '2026-01-05T09:00:00'),
(N'Grace', N'grace@example.com', '2026-02-11T14:30:00');

-- Order 1 has shipped; orders 2 and 3 have not, so shipped_on holds a value and a NULL.
INSERT INTO orders (user_id, status, placed_at, shipped_on) VALUES
(1, 'shipped', '2026-03-01T10:00:00', '2026-03-02'),
(1, 'pending', '2026-03-20T08:15:00', NULL),
(2, 'cancelled', '2026-04-02T12:45:00', NULL);

INSERT INTO order_lines (order_id, line_number, product, quantity, unit_price) VALUES
(1, 1, N'Notebook', 2, 4.50),
(1, 2, N'Pen', 5, 1.20),
(2, 1, N'Desk lamp', 1, 29.99),
(3, 1, N'Stapler', 1, 12.00),
(3, 2, N'Staples', 3, 2.25);
