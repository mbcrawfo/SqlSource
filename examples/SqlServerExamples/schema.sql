-- The schema of the example, with a few rows: users, their orders, the lines of each order and the delivery of each
-- order that has arrived.
-- The service "schema" of docker-compose.yml runs it in the database examples when it creates that database, and
-- tools/describe-end-to-end.sh in a database of its own before it describes the queries.  A change to it is followed
-- by a run of that script, which writes the sidecars again.

-- An email address, an alias type over nvarchar.  CodeGen/Users.sql maps it to the C# type EmailAddress.  A table
-- can use the type only in a batch after the one that creates it, so GO ends this one.
CREATE TYPE dbo.Email FROM nvarchar(200);
GO

CREATE TABLE users (
    id int IDENTITY PRIMARY KEY,
    name nvarchar(100) NOT NULL,
    email dbo.Email NOT NULL,
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

-- Where and when an order arrived, the carrier's details, a JSON object, and its events, a JSON array.  json is a type
-- of SQL Server 2025, the version docker-compose.yml starts.  CodeGen/Deliveries.sql reads location as
-- NetTopologySuite's Geometry, details as the C# class DeliveryDetails and events as a JsonElement.
CREATE TABLE deliveries (
    order_id int PRIMARY KEY REFERENCES orders (id),
    delivered_at datetimeoffset NOT NULL,
    location geography NOT NULL,
    details json NOT NULL,
    events json NOT NULL
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

-- Order 1, which has shipped, arrived in London.
INSERT INTO deliveries (order_id, delivered_at, location, details, events) VALUES
(
    1,
    '2026-03-02T16:00:00+00:00',
    geography::Point(51.5072, -0.1276, 4326),
    N'{"Carrier": "Parcelforce", "TrackingNumber": "PF1234567"}',
    N'[{"Status": "Delivered"}]'
);
