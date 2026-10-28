-- The schema of the database "billing", which the end-to-end tests describe the queries that name it against and run
-- them on.  SqlServerContainer and tools/describe-end-to-end.sh apply it, then seed.sql, batch by batch between the
-- lines that hold GO alone.  Every batch runs on SQL Server 2017.  A change to it is followed by a run of
-- tools/describe-end-to-end.sh, which writes the sidecars again.

-- A schema of its own, so that a table model's name has its prefix.
CREATE SCHEMA billing;
GO

CREATE TABLE billing.invoices (
    id int PRIMARY KEY,
    account_id int NOT NULL,
    number varchar(20) NOT NULL,
    amount decimal(12, 2) NOT NULL,
    issued_on date NOT NULL,
    paid_at datetime2(0) NULL
);
