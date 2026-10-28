-- The rows of the database "app".  SqlServerContainer and tools/describe-end-to-end.sh apply this after schema.sql, and
-- a change to it is followed by a run of tools/describe-end-to-end.sh.  Every value is written out, created_at
-- included, so that a test can name it, and the ids are given through IDENTITY_INSERT, whose ON and OFF are in the
-- batch of their insert.
--
-- accounts: alice (1) is active and has two notes, bob (2) is closed, with closed_at set, and has one, and carol (3)
-- is suspended and has none, which gives a LEFT JOIN of notes its NULLs.  Note 1 alone has a remind_at.
--
-- external_refs: alice (1) and bob (2) each have an external id, beyond the range of an int, and carol (3) has none.
--
-- all_types: row 1 has a value in every column, chosen to show the range of its type and to be exact in it, so that a
-- test can compare it as it is read: float and real hold binary fractions, datetime ends in .003, which it holds
-- exactly, smalldatetime is on a whole minute, both written with a T, which no SET DATEFORMAT or language reads
-- otherwise, datetime2, time and datetimeoffset have all 7 digits, the text of char, varchar and text is within code
-- page 1252, and sql_variant holds an int.  Row 2 has NULL in every column but id and rowversion_value, which the
-- server fills in every row, as it does in row 1.

SET IDENTITY_INSERT dbo.accounts ON;

INSERT INTO dbo.accounts (id, name, email, status, opened_on, created_at, closed_at) VALUES
    (1, N'alice', N'alice@example.com', 'active', '2024-01-01', '2024-01-01 09:00:00 +00:00', NULL),
    (2, N'bob', N'bob@example.com', 'closed', '2024-02-01', '2024-02-01 09:30:00 +00:00', '2024-09-01 17:45:00 +00:00'),
    (3, N'carol', N'carol@example.com', 'suspended', '2024-03-01', '2024-03-01 10:00:00 +00:00', NULL);

SET IDENTITY_INSERT dbo.accounts OFF;
GO

SET IDENTITY_INSERT dbo.notes ON;

INSERT INTO dbo.notes (id, account_id, kind, body, written_at, remind_at) VALUES
    (1, 1, 'call', N'Asked for a larger plan.', '2024-01-10 11:00:00.000', '09:30:00'),
    (2, 1, 'email', N'Sent the quote.', '2024-01-12 14:20:00.000', NULL),
    (3, 2, 'visit', N'Closed the account on site.', '2024-09-01 17:30:00.000', NULL);

SET IDENTITY_INSERT dbo.notes OFF;
GO

INSERT INTO dbo.external_refs (id, account_id, external_id) VALUES
    (1, 1, 5000000001),
    (2, 2, 5000000002);
GO

INSERT INTO dbo.all_types (
    id,
    bigint_value, int_value, smallint_value, tinyint_value, bit_value, decimal_value, numeric_value, money_value,
    smallmoney_value, float_value, real_value,
    date_value, datetime_value, datetime2_value, smalldatetime_value, time_value, datetimeoffset_value,
    char_value, varchar_value, text_value, nchar_value, nvarchar_value, ntext_value, xml_value,
    binary_value, varbinary_value, image_value,
    uniqueidentifier_value, sql_variant_value, sysname_value
) VALUES (
    1,
    -9223372036854775808, 2147483647, -32768, 255, 1, 12345678.1234, -1234567.89, 922337203685477.5807,
    -214748.3648, -2.25, 1.5,
    '2024-03-15', '2024-03-15T13:45:30.003', '2024-03-15 13:45:30.1234567', '2024-03-15T13:46:00',
    '13:45:30.1234567', '2024-03-15 13:45:30.1234567 +02:00',
    'héllo', 'Grüße, café', 'Ünïcödé text', N'Zoë日本', N'Grüße, 日本語', N'Ζωή, 日本語',
    N'<root><item id="1">Zoë</item></root>',
    0xDEADBEEF, 0x00FF10EE2A, 0x89504E470D0A1A0A,
    '123e4567-e89b-12d3-a456-426614174000', CAST(42 AS int), N'a_name'
);

INSERT INTO dbo.all_types (id) VALUES (2);
