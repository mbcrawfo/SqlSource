-- The schema of the example, with a few rows: users, their orders, the lines of each order and the delivery of each
-- order that has arrived.
-- docker-compose.yml runs it on the database's first start, and tools/describe-end-to-end.sh on a database of its
-- own before it describes the queries.  A change to it is followed by a run of that script, which writes the sidecars
-- again.

CREATE TYPE order_status AS ENUM ('pending', 'shipped', 'cancelled');

-- An email address, a domain over text.  CodeGen/Users.sql maps it to the C# type EmailAddress.
CREATE DOMAIN email AS text CHECK (VALUE LIKE '%_@_%');

CREATE TABLE users (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL,
    email email NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

-- shipped_at is null until the order ships.
CREATE TABLE orders (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id integer NOT NULL REFERENCES users (id),
    status order_status NOT NULL DEFAULT 'pending',
    placed_at timestamptz NOT NULL,
    shipped_at timestamptz,
    tags text[] NOT NULL DEFAULT '{}'
);

CREATE TABLE order_lines (
    order_id integer NOT NULL REFERENCES orders (id),
    line_number integer NOT NULL,
    product text NOT NULL,
    quantity integer NOT NULL,
    unit_price numeric(10, 2) NOT NULL,
    PRIMARY KEY (order_id, line_number)
);

-- PostGIS, which Postgis/Dockerfile installs, for the geography of a delivery.
CREATE EXTENSION postgis;

-- Where and when an order arrived, and the carrier's details, a JSON object.  CodeGen/Deliveries.sql reads location as
-- NetTopologySuite's Geometry and details as the C# class DeliveryDetails.
CREATE TABLE deliveries (
    order_id integer PRIMARY KEY REFERENCES orders (id),
    delivered_at timestamptz NOT NULL,
    location geography(Point, 4326) NOT NULL,
    details jsonb NOT NULL
);

INSERT INTO users (name, email, created_at) VALUES
('Ada', 'ada@example.com', '2026-01-05 09:00:00+00'),
('Grace', 'grace@example.com', '2026-02-11 14:30:00+00');

-- Order 1 has shipped; orders 2 and 3 have not, so shipped_at holds a value and a NULL.
INSERT INTO orders (user_id, status, placed_at, shipped_at, tags) VALUES
(1, 'shipped', '2026-03-01 10:00:00+00', '2026-03-02 16:00:00+00', '{gift}'),
(1, 'pending', '2026-03-20 08:15:00+00', NULL, '{}'),
(2, 'cancelled', '2026-04-02 12:45:00+00', NULL, '{priority,gift}');

INSERT INTO order_lines (order_id, line_number, product, quantity, unit_price) VALUES
(1, 1, 'Notebook', 2, 4.50),
(1, 2, 'Pen', 5, 1.20),
(2, 1, 'Desk lamp', 1, 29.99),
(3, 1, 'Stapler', 1, 12.00),
(3, 2, 'Staples', 3, 2.25);

-- Order 1, which has shipped, arrived in London.
INSERT INTO deliveries (order_id, delivered_at, location, details) VALUES
(
    1,
    '2026-03-02 16:00:00+00',
    'SRID=4326;POINT(-0.1276 51.5072)',
    '{"Carrier": "Parcelforce", "TrackingNumber": "PF1234567"}'
);
