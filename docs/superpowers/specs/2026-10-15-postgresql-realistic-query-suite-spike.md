# The spike of the realistic query suite - appendix

Date: 2026-10-15

Appendix to [the suite of realistic queries](2026-10-15-postgresql-realistic-query-suite-design.md).  A throwaway container test ran these queries against the fixture schema, `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`, on PostgreSQL 18, after the seed below; then it executed each one with literals cast to the recorded type of each parameter, in a transaction it rolled back, and looked for a `NULL` in a column recorded `nullable: false`.  It found none.  The test class was not kept; its seed, its queries with their sample values, and its output are here as they ran.

## Seed and queries

```csharp
    private const string Seed = """
        CREATE TABLE IF NOT EXISTS categories (id integer PRIMARY KEY, parent_id integer REFERENCES categories (id), name text NOT NULL);
        INSERT INTO categories VALUES (1, NULL, 'root'), (2, 1, 'child'), (3, 2, 'leaf');
        CREATE TABLE IF NOT EXISTS events (id bigint NOT NULL, user_id integer NOT NULL, at timestamptz NOT NULL, kind text NOT NULL) PARTITION BY RANGE (at);
        CREATE TABLE IF NOT EXISTS events_old PARTITION OF events FOR VALUES FROM (MINVALUE) TO ('2025-01-01');
        CREATE TABLE IF NOT EXISTS events_new PARTITION OF events FOR VALUES FROM ('2025-01-01') TO (MAXVALUE);
        INSERT INTO users (name, email, status, tags) VALUES ('alice', 'a@x', 'active', '{a,b}'), ('bob', NULL, 'disabled', '{}'), ('carol', 'c@x', 'active', '{c}');
        INSERT INTO orders (user_id, total, note, placed_at) VALUES (1, 10, 'first', '2024-01-05'), (1, 20, NULL, '2024-02-05'), (3, 5, 'x', '2024-03-01');
        INSERT INTO billing.invoices VALUES (1, 1, 10, '2024-01-06');
        INSERT INTO bookings (user_id, during, slots) VALUES (1, '[2024-01-01,2024-01-02)', NULL);
        INSERT INTO samples (id, flag, label, document) VALUES (1, true, 's1', '{"kind":"k","items":[1,2]}'), (2, false, NULL, NULL);
        INSERT INTO events VALUES (1, 1, '2024-06-01', 'login'), (2, 2, '2025-06-01', 'logout');
        """;

    // (label, sql, parameter literals by name)
    private static readonly (string Label, string Sql, Dictionary<string, string> Values)[] Queries =
    [
        ("01 recursive CTE tree", """
            WITH RECURSIVE tree AS (
                SELECT id, parent_id, name, 1 AS depth FROM categories WHERE parent_id IS NULL
                UNION ALL
                SELECT c.id, c.parent_id, c.name, t.depth + 1 FROM categories c JOIN tree t ON c.parent_id = t.id
            )
            SELECT id, parent_id, name, depth FROM tree ORDER BY depth, name
            """, new()),
        ("02 CTE aggregate left-joined, paged", """
            WITH totals AS (SELECT user_id, sum(total) AS spent, count(*) AS n FROM orders GROUP BY user_id)
            SELECT u.id, u.name, u.email, coalesce(t.spent, 0) AS spent, t.n, t.user_id
            FROM users u LEFT JOIN totals t ON t.user_id = u.id
            WHERE u.deleted_at IS NULL
            ORDER BY spent DESC
            LIMIT @limit OFFSET @offset
            """, new() { ["limit"] = "10", ["offset"] = "0" }),
        ("03 CTE referenced twice, self left join", """
            WITH recent AS (SELECT * FROM orders WHERE placed_at > @since)
            SELECT a.id, a.user_id, a.total, b.id AS next_id, b.total AS next_total
            FROM recent a LEFT JOIN recent b ON b.user_id = a.user_id AND b.placed_at > a.placed_at
            """, new() { ["since"] = "'2000-01-01'" }),
        ("04 UNION distinct, mixed types, ORDER BY LIMIT", """
            SELECT id, name AS label, 'user' AS kind FROM users WHERE name ILIKE @q
            UNION
            SELECT id, note, 'order' FROM orders WHERE note ILIKE @q
            ORDER BY label
            LIMIT 20
            """, new() { ["q"] = "'%'" }),
        ("05 UNION ALL in CTE joined to table", """
            WITH activity AS (
                SELECT user_id, placed_at AS at, 'order' AS kind FROM orders
                UNION ALL
                SELECT user_id, at, kind FROM events
            )
            SELECT u.id, u.name, a.at, a.kind FROM users u JOIN activity a ON a.user_id = u.id
            """, new()),
        ("06 EXCEPT / INTERSECT", """
            SELECT user_id FROM orders EXCEPT SELECT user_id FROM bookings
            """, new()),
        ("07 scalar subquery in select list", """
            SELECT u.id, u.name, (SELECT max(o.placed_at) FROM orders o WHERE o.user_id = u.id) AS last_order,
                   (SELECT o.note FROM orders o WHERE o.user_id = u.id ORDER BY o.placed_at LIMIT 1) AS first_note
            FROM users u
            """, new()),
        ("08 IN subquery", """
            SELECT id, name FROM users WHERE id IN (SELECT user_id FROM orders WHERE total > @min)
            """, new() { ["min"] = "1" }),
        ("09 CROSS JOIN LATERAL top-1", """
            SELECT u.id, u.name, lo.id AS order_id, lo.total
            FROM users u
            CROSS JOIN LATERAL (SELECT o.id, o.total FROM orders o WHERE o.user_id = u.id ORDER BY o.placed_at DESC LIMIT 1) lo
            """, new()),
        ("10 LEFT JOIN LATERAL top-1", """
            SELECT u.id, u.name, lo.id AS order_id, lo.total
            FROM users u
            LEFT JOIN LATERAL (SELECT o.id, o.total FROM orders o WHERE o.user_id = u.id ORDER BY o.placed_at DESC LIMIT 1) lo ON true
            """, new()),
        ("11 unnest WITH ORDINALITY", """
            SELECT u.id, t.tag, t.ord FROM users u CROSS JOIN LATERAL unnest(u.tags) WITH ORDINALITY AS t(tag, ord)
            """, new()),
        ("12 jsonb operators and set-returning", """
            SELECT s.id, s.document ->> 'kind' AS kind, e.value
            FROM samples s CROSS JOIN LATERAL jsonb_array_elements(s.document -> 'items') e
            WHERE s.document @> @filter AND s.document ? @key
            """, new() { ["filter"] = "'{}'", ["key"] = "'kind'" }),
        ("13 json_agg / jsonb_build_object over left join", """
            SELECT u.id, jsonb_build_object('id', u.id, 'name', u.name) AS obj,
                   coalesce(json_agg(o.total) FILTER (WHERE o.id IS NOT NULL), '[]') AS totals
            FROM users u LEFT JOIN orders o ON o.user_id = u.id
            GROUP BY u.id
            """, new()),
        ("14 array_agg of ROW", """
            SELECT u.id, array_agg(ROW(o.id, o.total)) AS orders FROM users u JOIN orders o ON o.user_id = u.id GROUP BY u.id
            """, new()),
        ("15 array_agg / string_agg", """
            SELECT user_id, array_agg(id ORDER BY placed_at) AS ids, string_agg(note, ', ') AS notes FROM orders GROUP BY user_id
            """, new()),
        ("16 window functions with named window", """
            SELECT o.id, o.user_id, o.total,
                   sum(o.total) OVER (PARTITION BY o.user_id ORDER BY o.placed_at) AS running,
                   lag(o.total) OVER w AS prev, rank() OVER w AS rnk
            FROM orders o
            WINDOW w AS (PARTITION BY o.user_id ORDER BY o.placed_at)
            """, new()),
        ("17 DISTINCT ON latest per user", """
            SELECT DISTINCT ON (o.user_id) o.user_id, o.id, o.total FROM orders o ORDER BY o.user_id, o.placed_at DESC
            """, new()),
        ("18 upsert ON CONFLICT RETURNING", """
            INSERT INTO samples (id, flag, label) VALUES (@id, @flag, @label)
            ON CONFLICT (id) DO UPDATE SET label = EXCLUDED.label, flag = EXCLUDED.flag
            RETURNING id, flag, label, (xmax = 0) AS inserted
            """, new() { ["id"] = "1", ["flag"] = "true", ["label"] = "'z'" }),
        ("19 INSERT ... SELECT RETURNING", """
            INSERT INTO orders (user_id, total, note) SELECT id, @total, @note FROM users WHERE status = 'active' RETURNING id, user_id, note
            """, new() { ["total"] = "1", ["note"] = "'n'" }),
        ("20 multi-row VALUES", """
            INSERT INTO orders (user_id, total) VALUES (@u1, @t1), (@u2, @t2) RETURNING id
            """, new() { ["u1"] = "1", ["t1"] = "1", ["u2"] = "3", ["t2"] = "2" }),
        ("21 bulk insert via unnest arrays", """
            INSERT INTO orders (user_id, total) SELECT * FROM unnest(@user_ids::int[], @totals::numeric[]) RETURNING id, user_id, total
            """, new() { ["user_ids"] = "'{1,3}'", ["totals"] = "'{1,2}'" }),
        ("22 data-modifying CTE", """
            WITH moved AS (DELETE FROM orders WHERE placed_at < @cutoff RETURNING *)
            INSERT INTO billing.invoices (id, order_id, amount, issued_on)
            SELECT id::int + 100, id, total, current_date FROM moved
            RETURNING id, order_id
            """, new() { ["cutoff"] = "'2030-01-01'" }),
        ("23 UPDATE FROM aggregate subquery RETURNING", """
            UPDATE users u SET balance = s.spent
            FROM (SELECT user_id, sum(total) AS spent FROM orders GROUP BY user_id) s
            WHERE s.user_id = u.id
            RETURNING u.id, u.balance, s.spent
            """, new()),
        ("24 DELETE USING RETURNING joined column", """
            DELETE FROM orders o USING users u WHERE u.id = o.user_id AND u.status = @status RETURNING o.id, u.name
            """, new() { ["status"] = "'active'" }),
        ("25 = ANY(@ids) and enum array", """
            SELECT id, name FROM users WHERE id = ANY(@ids) AND status = ANY(@statuses)
            """, new() { ["ids"] = "'{1,2,3}'", ["statuses"] = "'{active}'" }),
        ("26 ILIKE concat + optional enum filter + LIMIT", """
            SELECT id, name FROM users
            WHERE name ILIKE '%' || @q || '%' AND (@status::user_status IS NULL OR status = @status)
            LIMIT @limit
            """, new() { ["q"] = "'a'", ["status"] = "NULL", ["limit"] = "5" }),
        ("27 interval arithmetic without cast", """
            SELECT id FROM orders WHERE placed_at > now() - @age
            """, new() { ["age"] = "'1999-01-01'" }),
        ("28 range overlap with params", """
            SELECT id, during FROM bookings WHERE during && tstzrange(@from, @to)
            """, new() { ["from"] = "'2000-01-01'", ["to"] = "'2030-01-01'" }),
        ("29 COALESCE / CASE / GREATEST", """
            SELECT u.id, coalesce(u.email, 'none') AS email, CASE WHEN u.deleted_at IS NULL THEN 'live' ELSE 'gone' END AS state,
                   greatest(u.balance, 0) AS bal, u.email::text AS raw_email
            FROM users u
            """, new()),
        ("30 partitioned table, left join, param prune", """
            SELECT e.id, e.kind, u.name FROM events e LEFT JOIN users u ON u.id = e.user_id WHERE e.at >= @from
            """, new() { ["from"] = "'2000-01-01'" }),
        ("31 partitioned table on the nullable side", """
            SELECT u.id, u.name, e.kind, e.at FROM users u LEFT JOIN events e ON e.user_id = u.id
            """, new()),
        ("32 self-join tree parent name", """
            SELECT c.id, c.name, p.id AS parent_id, p.name AS parent_name FROM categories c LEFT JOIN categories p ON p.id = c.parent_id
            """, new()),
        ("33 reporting: CTE + window + group by", """
            WITH monthly AS (
                SELECT o.user_id, date_trunc('month', o.placed_at) AS month, sum(o.total) AS total
                FROM orders o WHERE o.placed_at >= @from GROUP BY 1, 2
            )
            SELECT u.id, u.name, m.month, m.total, m.total - lag(m.total) OVER (PARTITION BY u.id ORDER BY m.month) AS change
            FROM users u JOIN monthly m ON m.user_id = u.id
            ORDER BY u.id, m.month
            """, new() { ["from"] = "'2000-01-01'" }),
        ("34 single table, gate word only in subquery, ORDER BY LIMIT", """
            SELECT id, name, status FROM users
            WHERE NOT EXISTS (SELECT 1 FROM orders o LEFT JOIN billing.invoices i ON i.order_id = o.id WHERE o.user_id = users.id AND i.id IS NULL)
            ORDER BY name LIMIT 10
            """, new()),
        ("35 count FILTER", """
            SELECT count(*) FILTER (WHERE status = 'active') AS active, count(*) AS total, avg(balance) AS avg_balance FROM users
            """, new()),
        ("36 TABLE and VALUES statements", """
            TABLE categories
            """, new()),
        ("37 VALUES statement", """
            VALUES (1, 'a'), (2, NULL)
            """, new()),
        ("38 DISTINCT subquery over left join", """
            SELECT x.id, x.name, x.total FROM (SELECT DISTINCT u.id, u.name, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id) x
            """, new()),
        ("39 parameter used as two types", """
            SELECT id FROM users WHERE name = @x OR id = @x
            """, new() { ["x"] = "'1'" }),
        ("40 row_number dedupe in subquery", """
            SELECT s.id, s.name, s.total FROM (
                SELECT u.id, u.name, o.total, row_number() OVER (PARTITION BY u.id ORDER BY o.placed_at DESC) AS rn
                FROM users u LEFT JOIN orders o ON o.user_id = u.id
            ) s WHERE s.rn = 1
            """, new()),
        ("41 inner join only, no gate", """
            SELECT u.id, u.name, o.id AS order_id, o.note, i.amount
            FROM users u JOIN orders o ON o.user_id = u.id JOIN billing.invoices i ON i.order_id = o.id
            WHERE u.id = @id
            """, new() { ["id"] = "1" }),
        ("42 view joined left", """
            SELECT v.user_id, v.name, v.order_id, a.email FROM user_orders v LEFT JOIN active_users a ON a.id = v.user_id
            """, new()),
        ("43 EXISTS as boolean column", """
            SELECT u.id, EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id) AS has_orders FROM users u
            """, new()),
        ("44 right join + coalesce key", """
            SELECT coalesce(u.id, o.user_id) AS uid, u.name, o.id AS order_id FROM users u RIGHT JOIN orders o ON o.user_id = u.id
            """, new()),
    ];
```

## Output

`oracle: n rows` is the number of rows the query returned over the seed.  A wrong `false` would have printed a line starting `!!!`; none did.

```text
=== 01 recursive CTE tree
  kind=Rows plan=not-needed match=no-origin
  col 0 id: integer nullable=null src=no-origin origin=-
  col 1 parent_id: integer nullable=null src=no-origin origin=-
  col 2 name: text nullable=null src=no-origin origin=-
  col 3 depth: integer nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 02 CTE aggregate left-joined, paged
  kind=Rows plan=walked match=no-origin
  param @limit: bigint (inferred)
  param @offset: bigint (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 email: public.email_t nullable=True src=catalog origin=public.users.email
  col 3 spent: numeric nullable=null src=no-origin origin=-
  col 4 n: bigint nullable=null src=no-origin origin=-
  col 5 user_id: integer nullable=True src=outer-join origin=public.orders.user_id
  oracle: 3 rows

=== 03 CTE referenced twice, self left join
  kind=Rows plan=walked match=names-differ
  param @since: timestamp with time zone (inferred)
  col 0 id: bigint nullable=True src=heuristic origin=public.orders.id
  col 1 user_id: integer nullable=True src=heuristic origin=public.orders.user_id
  col 2 total: numeric(18,2) nullable=True src=heuristic origin=public.orders.total
  col 3 next_id: bigint nullable=True src=heuristic origin=public.orders.id
  col 4 next_total: numeric(18,2) nullable=True src=heuristic origin=public.orders.total
  oracle: 3 rows

=== 04 UNION distinct, mixed types, ORDER BY LIMIT
  kind=Rows plan=not-needed match=no-origin
  param @q: text (inferred)
  col 0 id: bigint nullable=null src=no-origin origin=-
  col 1 label: character varying nullable=null src=no-origin origin=-
  col 2 kind: text nullable=null src=no-origin origin=-
  oracle: 5 rows

=== 05 UNION ALL in CTE joined to table
  kind=Rows plan=not-needed match=no-origin
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 at: timestamp with time zone nullable=null src=no-origin origin=-
  col 3 kind: text nullable=null src=no-origin origin=-
  oracle: 5 rows

=== 06 EXCEPT / INTERSECT
  kind=Rows plan=not-needed match=no-origin
  col 0 user_id: integer nullable=null src=no-origin origin=-
  oracle: 1 rows

=== 07 scalar subquery in select list
  kind=Rows plan=not-needed match=no-origin
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 last_order: timestamp with time zone nullable=null src=no-origin origin=-
  col 3 first_note: text nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 08 IN subquery
  kind=Rows plan=not-needed match=columns-differ
  param @min: numeric (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  oracle: 2 rows

=== 09 CROSS JOIN LATERAL top-1
  kind=Rows plan=not-needed match=several-tables
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 order_id: bigint nullable=False src=catalog origin=public.orders.id
  col 3 total: numeric(18,2) nullable=False src=catalog origin=public.orders.total
  oracle: 2 rows

=== 10 LEFT JOIN LATERAL top-1
  kind=Rows plan=walked match=several-tables
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 order_id: bigint nullable=True src=outer-join origin=public.orders.id
  col 3 total: numeric(18,2) nullable=True src=outer-join origin=public.orders.total
  oracle: 3 rows

=== 11 unnest WITH ORDINALITY
  kind=Rows plan=not-needed match=no-origin
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 tag: text nullable=null src=no-origin origin=-
  col 2 ord: bigint nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 12 jsonb operators and set-returning
  kind=Rows plan=not-needed match=no-origin
  param @filter: jsonb (inferred)
  param @key: text (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.samples.id
  col 1 kind: text nullable=null src=no-origin origin=-
  col 2 value: jsonb nullable=null src=no-origin origin=-
  oracle: 2 rows

=== 13 json_agg / jsonb_build_object over left join
  kind=Rows plan=walked match=no-origin
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 obj: jsonb nullable=null src=no-origin origin=-
  col 2 totals: json nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 14 array_agg of ROW
  FAIL SQLSRC229 args=[orders | Q | record[]]

=== 15 array_agg / string_agg
  kind=Rows plan=not-needed match=no-origin
  col 0 user_id: integer nullable=False src=catalog origin=public.orders.user_id
  col 1 ids: bigint[] nullable=null src=no-origin origin=-
  col 2 notes: text nullable=null src=no-origin origin=-
  oracle: 2 rows

=== 16 window functions with named window
  kind=Rows plan=not-needed match=no-origin
  col 0 id: bigint nullable=False src=catalog origin=public.orders.id
  col 1 user_id: integer nullable=False src=catalog origin=public.orders.user_id
  col 2 total: numeric(18,2) nullable=False src=catalog origin=public.orders.total
  col 3 running: numeric nullable=null src=no-origin origin=-
  col 4 prev: numeric nullable=null src=no-origin origin=-
  col 5 rnk: bigint nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 17 DISTINCT ON latest per user
  kind=Rows plan=not-needed match=columns-differ
  col 0 user_id: integer nullable=False src=catalog origin=public.orders.user_id
  col 1 id: bigint nullable=False src=catalog origin=public.orders.id
  col 2 total: numeric(18,2) nullable=False src=catalog origin=public.orders.total
  oracle: 2 rows

=== 18 upsert ON CONFLICT RETURNING
  kind=Rows plan=not-needed match=no-origin
  param @id: integer (inferred)
  param @flag: boolean (inferred)
  param @label: text (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.samples.id
  col 1 flag: boolean nullable=False src=catalog origin=public.samples.flag
  col 2 label: text nullable=True src=catalog origin=public.samples.label
  col 3 inserted: boolean nullable=null src=no-origin origin=-
  oracle: 1 rows

=== 19 INSERT ... SELECT RETURNING
  kind=Rows plan=not-needed match=columns-differ
  param @total: numeric (inferred)
  param @note: text (inferred)
  col 0 id: bigint nullable=False src=catalog origin=public.orders.id
  col 1 user_id: integer nullable=False src=catalog origin=public.orders.user_id
  col 2 note: text nullable=True src=catalog origin=public.orders.note
  oracle: 2 rows

=== 20 multi-row VALUES
  kind=Rows plan=not-needed match=columns-differ
  param @u1: integer (inferred)
  param @t1: numeric (inferred)
  param @u2: integer (inferred)
  param @t2: numeric (inferred)
  col 0 id: bigint nullable=False src=catalog origin=public.orders.id
  oracle: 2 rows

=== 21 bulk insert via unnest arrays
  kind=Rows plan=not-needed match=columns-differ
  param @user_ids: integer[] (inferred)
  param @totals: numeric[] (inferred)
  col 0 id: bigint nullable=False src=catalog origin=public.orders.id
  col 1 user_id: integer nullable=False src=catalog origin=public.orders.user_id
  col 2 total: numeric(18,2) nullable=False src=catalog origin=public.orders.total
  oracle: 2 rows

=== 22 data-modifying CTE
  kind=Rows plan=not-needed match=columns-differ
  param @cutoff: timestamp with time zone (inferred)
  col 0 id: integer nullable=False src=catalog origin=billing.invoices.id
  col 1 order_id: bigint nullable=False src=catalog origin=billing.invoices.order_id
  oracle: 3 rows

=== 23 UPDATE FROM aggregate subquery RETURNING
  kind=Rows plan=not-needed match=no-origin
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 balance: numeric(18,2) nullable=False src=catalog origin=public.users.balance
  col 2 spent: numeric nullable=null src=no-origin origin=-
  oracle: 2 rows

=== 24 DELETE USING RETURNING joined column
  kind=Rows plan=not-needed match=several-tables
  param @status: public.user_status (inferred)
  col 0 id: bigint nullable=False src=catalog origin=public.orders.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  oracle: 3 rows

=== 25 = ANY(@ids) and enum array
  kind=Rows plan=not-needed match=columns-differ
  param @ids: integer[] (inferred)
  param @statuses: public.user_status[] (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  oracle: 2 rows

=== 26 ILIKE concat + optional enum filter + LIMIT
  kind=Rows plan=not-needed match=columns-differ
  param @q: text (inferred)
  param @status: public.user_status (inferred)
  param @limit: bigint (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  oracle: 2 rows

=== 27 interval arithmetic without cast
  FAIL SQLSRC225 args=[Q | operator does not exist: timestamp with time zone > interval]
    server: 42883: operator does not exist: timestamp with time zone > interval
    server: hint: No operator matches the given name and argument types. You might need to add explicit type casts.
    server: at character 39

=== 28 range overlap with params
  kind=Rows plan=not-needed match=columns-differ
  param @from: timestamp with time zone (inferred)
  param @to: timestamp with time zone (inferred)
  col 0 id: bigint nullable=False src=catalog origin=public.bookings.id
  col 1 during: tstzrange nullable=False src=catalog origin=public.bookings.during
  oracle: 1 rows

=== 29 COALESCE / CASE / GREATEST
  kind=Rows plan=not-needed match=no-origin
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 email: character varying nullable=null src=no-origin origin=-
  col 2 state: text nullable=null src=no-origin origin=-
  col 3 bal: numeric nullable=null src=no-origin origin=-
  col 4 raw_email: text nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 30 partitioned table, left join, param prune
  kind=Rows plan=walked match=several-tables
  param @from: timestamp with time zone (inferred)
  col 0 id: bigint nullable=True src=heuristic origin=public.events.id
  col 1 kind: text nullable=True src=heuristic origin=public.events.kind
  col 2 name: character varying(100) nullable=True src=outer-join origin=public.users.name
  oracle: 2 rows

=== 31 partitioned table on the nullable side
  kind=Rows plan=walked match=several-tables
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 kind: text nullable=True src=heuristic origin=public.events.kind
  col 3 at: timestamp with time zone nullable=True src=heuristic origin=public.events.at
  oracle: 3 rows

=== 32 self-join tree parent name
  kind=Rows plan=walked match=names-differ
  col 0 id: integer nullable=False src=catalog origin=public.categories.id
  col 1 name: text nullable=False src=catalog origin=public.categories.name
  col 2 parent_id: integer nullable=True src=outer-join origin=public.categories.id
  col 3 parent_name: text nullable=True src=outer-join origin=public.categories.name
  oracle: 3 rows

=== 33 reporting: CTE + window + group by
  kind=Rows plan=not-needed match=no-origin
  param @from: timestamp with time zone (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 month: timestamp with time zone nullable=null src=no-origin origin=-
  col 3 total: numeric nullable=null src=no-origin origin=-
  col 4 change: numeric nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 34 single table, gate word only in subquery, ORDER BY LIMIT
  kind=Rows plan=walked match=columns-differ
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 status: public.user_status nullable=False src=catalog origin=public.users.status
  oracle: 1 rows

=== 35 count FILTER
  kind=Rows plan=not-needed match=no-origin
  col 0 active: bigint nullable=null src=no-origin origin=-
  col 1 total: bigint nullable=null src=no-origin origin=-
  col 2 avg_balance: numeric nullable=null src=no-origin origin=-
  oracle: 1 rows

=== 36 TABLE and VALUES statements
  kind=Rows plan=not-needed match=matched
  col 0 id: integer nullable=False src=catalog origin=public.categories.id
  col 1 parent_id: integer nullable=True src=catalog origin=public.categories.parent_id
  col 2 name: text nullable=False src=catalog origin=public.categories.name
  oracle: 3 rows

=== 37 VALUES statement
  kind=Rows plan=not-needed match=no-origin
  col 0 column1: integer nullable=null src=no-origin origin=-
  col 1 column2: text nullable=null src=no-origin origin=-
  oracle: 2 rows

=== 38 DISTINCT subquery over left join
  kind=Rows plan=walked match=several-tables
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 total: numeric(18,2) nullable=True src=outer-join origin=public.orders.total
  oracle: 4 rows

=== 39 parameter used as two types
  FAIL SQLSRC225 args=[Q | operator does not exist: integer = text]
    server: 42883: operator does not exist: integer = text
    server: hint: No operator matches the given name and argument types. You might need to add explicit type casts.
    server: at character 44

=== 40 row_number dedupe in subquery
  kind=Rows plan=walked match=several-tables
  col 0 id: integer nullable=True src=heuristic origin=public.users.id
  col 1 name: character varying(100) nullable=True src=heuristic origin=public.users.name
  col 2 total: numeric(18,2) nullable=True src=heuristic origin=public.orders.total
  oracle: 3 rows

=== 41 inner join only, no gate
  kind=Rows plan=not-needed match=several-tables
  param @id: integer (inferred)
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 name: character varying(100) nullable=False src=catalog origin=public.users.name
  col 2 order_id: bigint nullable=False src=catalog origin=public.orders.id
  col 3 note: text nullable=True src=catalog origin=public.orders.note
  col 4 amount: numeric(18,2) nullable=False src=catalog origin=billing.invoices.amount
  oracle: 1 rows

=== 42 view joined left
  kind=Rows plan=walked match=several-tables
  col 0 user_id: integer nullable=null src=view origin=public.user_orders.user_id
  col 1 name: character varying(100) nullable=null src=view origin=public.user_orders.name
  col 2 order_id: bigint nullable=True src=outer-join origin=public.user_orders.order_id
  col 3 email: public.email_t nullable=True src=outer-join origin=public.active_users.email
  oracle: 4 rows

=== 43 EXISTS as boolean column
  kind=Rows plan=not-needed match=no-origin
  col 0 id: integer nullable=False src=catalog origin=public.users.id
  col 1 has_orders: boolean nullable=null src=no-origin origin=-
  oracle: 3 rows

=== 44 right join + coalesce key
  kind=Rows plan=walked match=no-origin
  col 0 uid: integer nullable=null src=no-origin origin=-
  col 1 name: character varying(100) nullable=True src=outer-join origin=public.users.name
  col 2 order_id: bigint nullable=False src=catalog origin=public.orders.id
  oracle: 3 rows

```
