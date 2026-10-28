-- The rows of the database "billing": three invoices, the third of them unpaid.  Every value is written out, so that
-- a test can name it.  SqlServerContainer and tools/describe-end-to-end.sh apply this after schema.sql, and a change
-- to it is followed by a run of tools/describe-end-to-end.sh.

INSERT INTO billing.invoices (id, account_id, number, amount, issued_on, paid_at) VALUES
    (1, 1, 'INV-0001', 120.50, '2024-01-15', '2024-01-20 10:30:00'),
    (2, 1, 'INV-0002', 80.00, '2024-02-15', '2024-02-18 08:15:00'),
    (3, 2, 'INV-0003', 45.25, '2024-03-15', NULL);
