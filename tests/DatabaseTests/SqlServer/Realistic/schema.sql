-- The schema of the database "realistic", which the corpus of realistic queries in tests/DatabaseTests/SqlServer is
-- described and executed against.  SqlServerContainer applies it, then seed.sql, once for the run, through
-- EndToEndFiles: each batch between the lines that hold GO alone runs as one command of SqlClient, with
-- QUOTED_IDENTIFIER on, which the persisted computed column of dbo.products needs.  It stands apart from the fixture
-- schema: a change here moves no capture of the fixtures.  Every batch runs on SQL Server 2017; what 2017 lacks is
-- made by a batch that runs only on a later major.  A column is added only when a case needs it.

-- An alias type that allows NULL, so a column of it is nullable unless it says otherwise.
CREATE TYPE dbo.Email FROM nvarchar(254) NULL;
GO

CREATE SCHEMA billing;
GO

-- The numbers of the invoices, which the default of billing.invoices.number draws.
CREATE SEQUENCE billing.invoice_numbers AS int START WITH 1000;

CREATE TABLE dbo.customers (
    id int IDENTITY PRIMARY KEY,
    name nvarchar(100) NOT NULL,
    email dbo.Email NULL,
    phone varchar(20) NULL,
    company nvarchar(100) NULL,
    status varchar(10) NOT NULL CHECK (status IN ('active', 'suspended', 'closed')),
    -- A Windows name of a time zone, as AT TIME ZONE takes it.
    time_zone varchar(50) NULL,
    created_at datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    closed_at datetime2(0) NULL
);

-- The tree, for recursive queries and self-joins.
CREATE TABLE dbo.categories (
    id int PRIMARY KEY,
    parent_id int NULL REFERENCES dbo.categories (id),
    name nvarchar(50) NOT NULL
);

-- A computed column and a persisted one; attributes holds a JSON object as text.
CREATE TABLE dbo.products (
    id int IDENTITY PRIMARY KEY,
    sku varchar(20) NOT NULL UNIQUE,
    name nvarchar(100) NOT NULL,
    category_id int NULL REFERENCES dbo.categories (id),
    list_price decimal(10, 2) NOT NULL,
    discount_pct decimal(5, 2) NULL,
    sale_price AS (list_price * (1 - ISNULL(discount_pct, 0) / 100)),
    sku_prefix AS (LEFT(sku, 3)) PERSISTED,
    discontinued_at date NULL,
    attributes nvarchar(max) NULL
);

CREATE TABLE dbo.stock (
    product_id int PRIMARY KEY REFERENCES dbo.products (id),
    on_hand int NOT NULL,
    reorder_at int NULL,
    updated_at datetime2(0) NOT NULL
);

CREATE TABLE dbo.orders (
    id int IDENTITY PRIMARY KEY,
    customer_id int NOT NULL REFERENCES dbo.customers (id),
    placed_at datetime2(0) NOT NULL,
    status varchar(10) NOT NULL,
    ship_name nvarchar(100) NULL,
    ship_city nvarchar(50) NULL,
    shipped_at datetime2(0) NULL,
    note nvarchar(200) NULL,
    row_version rowversion NOT NULL
);

CREATE TABLE dbo.order_lines (
    order_id int NOT NULL REFERENCES dbo.orders (id),
    line_no int NOT NULL,
    product_id int NOT NULL REFERENCES dbo.products (id),
    quantity int NOT NULL,
    unit_price decimal(10, 2) NOT NULL,
    PRIMARY KEY (order_id, line_no)
);

CREATE TABLE billing.invoices (
    id int IDENTITY PRIMARY KEY,
    number int NOT NULL UNIQUE DEFAULT (NEXT VALUE FOR billing.invoice_numbers),
    order_id int NOT NULL REFERENCES dbo.orders (id),
    issued_at date NOT NULL,
    due_at date NULL,
    amount money NOT NULL,
    tax decimal(10, 2) NULL,
    voided_at datetime2(0) NULL
);

CREATE TABLE billing.payments (
    id int IDENTITY PRIMARY KEY,
    invoice_id int NOT NULL REFERENCES billing.invoices (id),
    paid_at datetime2(0) NOT NULL,
    amount money NOT NULL,
    method varchar(10) NOT NULL,
    reference nvarchar(50) NULL
);

-- A system-versioned table, whose period columns the server fills.  They are HIDDEN, so SELECT * leaves them out.
CREATE TABLE dbo.prices (
    product_id int NOT NULL PRIMARY KEY REFERENCES dbo.products (id),
    price decimal(10, 2) NOT NULL,
    valid_from datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    valid_to datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (valid_from, valid_to)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.prices_history));

-- A cart of a guest has no customer.
CREATE TABLE dbo.carts (
    id uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    customer_id int NULL REFERENCES dbo.customers (id),
    created_at datetime2(0) NOT NULL,
    checked_out_at datetime2(0) NULL
);

CREATE TABLE dbo.cart_items (
    cart_id uniqueidentifier NOT NULL REFERENCES dbo.carts (id),
    product_id int NOT NULL REFERENCES dbo.products (id),
    quantity int NOT NULL,
    added_at datetime2(0) NOT NULL,
    PRIMARY KEY (cart_id, product_id)
);
GO

-- A view over an outer join, of each customer and the latest of their orders: order_id, placed_at and status are NULL
-- for a customer with no order.
CREATE VIEW dbo.customer_last_orders AS
SELECT c.id AS customer_id, c.name, o.id AS order_id, o.placed_at, o.status
FROM dbo.customers AS c
LEFT JOIN dbo.orders AS o
    ON o.id = (
        SELECT TOP (1) l.id
        FROM dbo.orders AS l
        WHERE l.customer_id = c.id
        ORDER BY l.placed_at DESC, l.id DESC
    );
GO

-- A column of the type json, which SQL Server 2025 brings: the table is made on 2025 and later alone, by a text that
-- 2017 never parses.
IF CAST(SERVERPROPERTY('ProductMajorVersion') AS int) >= 17
    EXEC (N'
CREATE TABLE dbo.product_specs (
    product_id int NOT NULL PRIMARY KEY REFERENCES dbo.products (id),
    spec json NULL
);');
