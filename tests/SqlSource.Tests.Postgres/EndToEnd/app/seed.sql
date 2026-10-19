-- The rows of the database "app".  PostgresContainer and tools/describe-end-to-end.sh apply this after schema.sql, and
-- a change to it is followed by a run of tools/describe-end-to-end.sh.  Every value is written out, created_at
-- included, so that a test can name it.
--
-- accounts: alice (1) is active and has two notes, bob (2) is closed, with closed_at set, and has one, and carol (3)
-- is suspended and has none, which gives a LEFT JOIN of notes its NULLs.
--
-- all_types: row 1 has a value in every column, chosen to show the range of its type, and row 2 has NULL in every
-- column but id.  Row 1 holds no value that Npgsql cannot read: no NaN or infinity in a numeric, no timestamptz
-- infinity, no interval with months or years, and no NULL element in an array.  Its values are exact in binary (1.5,
-- -2.25) and its timetz has an offset, so that a test can compare them as they are.

INSERT INTO accounts (id, name, email, status, created_at, closed_at) OVERRIDING SYSTEM VALUE VALUES
    (1, 'alice', 'alice@example.com', 'active', '2024-01-01 09:00:00+00', NULL),
    (2, 'bob', 'bob@example.com', 'closed', '2024-02-01 09:30:00+00', '2024-09-01 17:45:00+00'),
    (3, 'carol', 'carol@example.com', 'suspended', '2024-03-01 10:00:00+00', NULL);

SELECT setval(pg_get_serial_sequence('accounts', 'id'), 3);

INSERT INTO notes (id, account_id, kind, body, written_at) OVERRIDING SYSTEM VALUE VALUES
    (1, 1, 'call', 'Asked for a larger plan.', '2024-01-10 11:00:00+00'),
    (2, 1, 'email', 'Sent the quote.', '2024-01-12 14:20:00+00'),
    (3, 2, 'visit', 'Closed the account on site.', '2024-09-01 17:30:00+00');

SELECT setval(pg_get_serial_sequence('notes', 'id'), 3);

INSERT INTO all_types (
    id,
    bool_value, int2_value, int4_value, int8_value, float4_value, float8_value, numeric_value, money_value,
    text_value, varchar_value, bpchar_value, name_value, char_value, citext_value, bytea_value,
    date_value, time_value, timetz_value, timestamp_value, timestamptz_value, interval_value,
    uuid_value, json_value, jsonb_value, jsonpath_value, xml_value,
    inet_value, cidr_value, macaddr_value, macaddr8_value,
    bit1_value, bit8_value, varbit_value,
    point_value, line_value, lseg_value, box_value, path_value, polygon_value, circle_value,
    tsvector_value, tsquery_value,
    oid_value, xid_value, cid_value, regtype_value, regconfig_value, xid8_value, tid_value, pg_lsn_value,
    int2vector_value, oidvector_value, refcursor_value,
    hstore_value, ltree_value, lquery_value, ltxtquery_value,
    int4range_value, int8range_value, numrange_value, tsrange_value, tstzrange_value, daterange_value,
    int4multirange_value, int8multirange_value, nummultirange_value, tsmultirange_value, tstzmultirange_value,
    datemultirange_value,
    int4_array_value, text_array_value, date_array_value, status_array_value, email_value, status_value
) VALUES (
    1,
    true, -32768, 2147483647, -9223372036854775808, 1.5, -2.25, 12345678.1234, 1234.56,
    'Grüße, 日本語', 'Ünïcödé', 'héllo', 'a_name', 'A', 'Mixed Case', '\xdeadbeef00ff',
    '2024-03-15', '13:45:30.123456', '13:45:30.123456+02', '2024-03-15 13:45:30.123456',
    '2024-03-15 13:45:30.123456+00', '3 days 04:05:06.789012',
    '123e4567-e89b-12d3-a456-426614174000', '{"name": "Zoë", "tags": ["a", "b"]}',
    '{"name": "Zoë", "tags": ["a", "b"]}', '$.name', '<root><item id="1">Zoë</item></root>',
    '192.168.1.10', '192.168.100.128/25', '08:00:2b:01:02:03', '08:00:2b:01:02:03:04:05',
    B'1', B'10101010', B'1011001',
    '(1.5,-2.25)', '{1,-1,0}', '[(0,0),(1.5,2.5)]', '(3,4),(1,2)', '[(0,0),(1,1),(2,0)]', '((0,0),(4,0),(4,3))',
    '<(1,2),3>',
    '''fat'':2 ''rat'':3', '''fat'' & ''rat''',
    '4294967295', '4294967295', '12345', 'integer'::regtype, 'simple'::regconfig, '18446744073709551615', '(0,1)',
    '16/B374D848',
    '1 2 3', '4 5 6', 'cursor_1',
    '"k1"=>"v1", "k2"=>"Zoë", "k3"=>NULL', 'Top.Science.Astronomy', 'Top.*{0,2}.Astronomy', 'Science & Astronomy',
    '[1,10)', '[1,100)', '(1.5,10.25]', '[2024-01-01 00:00:00.5,2024-02-01 00:00:00)',
    '[2024-01-01 00:00:00+00,2024-02-01 00:00:00+00)', '[2024-01-01,2024-02-01)',
    '{[1,5),[10,20)}', '{[1,5),[10,20)}', '{(1.5,2.5],[4,5)}',
    '{[2024-01-01 00:00:00,2024-01-02 00:00:00),[2024-02-01 00:00:00,2024-02-02 00:00:00)}',
    '{[2024-01-01 00:00:00+00,2024-01-02 00:00:00+00),[2024-02-01 00:00:00+00,2024-02-02 00:00:00+00)}',
    '{[2024-01-01,2024-01-05),[2024-02-01,2024-02-05)}',
    '{1,2,3}', '{a,"b c",Zoë}', '{2024-01-01,2024-12-31}', '{active,closed}', 'zoe@example.com', 'suspended'
);

INSERT INTO all_types (id) VALUES (2);
