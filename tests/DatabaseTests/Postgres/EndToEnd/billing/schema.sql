-- The schema of the database "billing", which the end-to-end tests describe the queries that name it against and run
-- them on.  PostgresContainer and tools/describe-end-to-end.sh apply it, then seed.sql.  A change to it is followed by
-- a run of tools/describe-end-to-end.sh, which writes the sidecars again.

CREATE SCHEMA billing;

CREATE TABLE billing.invoices (
    id integer PRIMARY KEY,
    account_id integer NOT NULL,
    number text NOT NULL,
    amount numeric(12, 2) NOT NULL,
    issued_on date NOT NULL,
    paid_at timestamptz
);
