# TD-0034: the redaction holes that can close - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the holes of TD-0034 that need no owner decision reversed: a short or run-word password that stands as a password key's value, a value split over two strings of a log event in any order, and the PostgreSQL password file read as Npgsql reads it.  Rewrite TD-0034 to what remains.

**Architecture:** `ConnectionSecret` keeps the parts it drops for length or for equalling a run word as `KeyedParts`, which `Redaction` finds only as the value of a password key.  `JoinedRedaction` gains a linear search of every ordered pair of texts, which `SessionRunLog` asks for.  `PasswordFile` matches entries as Npgsql's `PgPassFile` does and stops at the first sure match, and names the one default file of the running system.

**Tech Stack:** C#, .NET 8, xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`, and Npgsql 10.0.3's `PgPassFile`, `PostgresEnvironment` and `NpgsqlConnector.GetPassword`.

## Global Constraints

- The owner's decisions stand: a login's name in a SQL Server message, what a raw bundle keeps, and an entry searched for whole forms only (`EntrySecretCheck` does not change).
- A capture is not searched pair by pair: a false finding there ends the run with `SQLSRC200`, as the owner ruled out for entries.  Only log events get pairs.
- A password file that is a pipe stays unread.  Giving the driver the tool's copy needs `NpgsqlConnection.ProvidePasswordCallback`, which Npgsql 10.0.3 marks obsolete, or a data source password provider, which never sees the host and user the driver resolves, so the tool would choose the password in place of the driver.  This is the brief's "if it can be done safely", judged no.
- A password file entry is given when it may match: a field the tool cannot know, the user Npgsql takes from Kerberos or the system, matches any entry.  A sure match ends the search, and a malformed line ends it too, since Npgsql throws there.

## Review Focus

- `Password=pg;` and `Password=postgres;` print `Password=***;`, while `query: GetUser, database app, postgres 16.4` and `user "postgres"` keep the word.
- A whole or a part split over two strings with others between, in either order, is marked in a log event; a capture is unchanged.
- The pair search is linear in the number of strings: a log event can hold thousands of catalog strings.
- `PasswordFile` matches as `PgPassFile.Entry.IsMatch` does: ordinal, `*` as any, a null database matching any entry, the host split from its port as `TrySplitHostPort` splits it.

---

### Task 1: A short or run-word password as a key's value

**Files:**
- Modify: `src/SqlSource.Tool/Describing/ConnectionSecret.cs`, `src/SqlSource.Tool/Describing/Redaction.cs`, `src/SqlSource.Tool/Describing/JoinedRedaction.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/ConnectionSecretTests.cs`, `RedactionTests.cs`, `JoinedRedactionTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`

- [ ] **Step 1:** Write tests: `KeyedParts` holds the short and run-word parts and drops blanks and wholes; `Redaction.Of` marks `Password=pg`, `PWD = 'pg'`, `"password":"postgres"`, `SSL Password=pg;` and `PGPASSWORD=pg`, and leaves `pg_class`, `user "postgres"` and `database app, postgres 16.4`; `IsIn` and `JoinedRedaction` find a keyed part over two texts in order.
- [ ] **Step 2:** Run them and see them fail.
- [ ] **Step 3:** Implement `ConnectionSecret.KeyedParts`, `Redaction.NextKeyed` (a token whose text before it, past one quote, white space, `=` or `:` and white space, ends in `password`, or in `pwd` or `psw` after no letter or digit, ignoring case), and use it in `Of`, `IsIn`, `OfLines` and `JoinedRedaction.Cut`.
- [ ] **Step 4:** Run the tests of `Describing/`, `Logging/` and `Commands/SecretTests`.

### Task 2: Every pair of a log event's strings

**Files:**
- Modify: `src/SqlSource.Tool/Describing/JoinedRedaction.cs`, `src/SqlSource.Tool/Logging/SessionRunLog.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/JoinedRedactionTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs`

- [ ] **Step 1:** Write tests: with pairs, a whole and a part split over two texts with a third between, and in reverse order, are marked in both halves; the same text is not paired with itself; a part's halves joined that are no token stay; without pairs nothing changes; a log event with a member's name and a later member's value, and with two values in reverse order, has the value removed.
- [ ] **Step 2:** Run them and see them fail.
- [ ] **Step 3:** Implement the pair search by splits: for each whole and part and each cut of it, the texts that end with its head and the texts that start with its tail, each marked when the other side has a text other than itself; a part's head and tail are checked as a token at their outer ends.  Forms with a line break are searched with the break as the cut.  `SessionRunLog` asks for pairs.
- [ ] **Step 4:** Run the tests of `Describing/` and `Logging/`.

### Task 3: The password file's matching entry and the system's default file

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PasswordFile.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PasswordFileTests.cs`, `PostgresDescriberTests.cs`

- [ ] **Step 1:** Write tests: entries before the first sure match that may match are given, and none after it; a host, port, database or user that differs skips an entry; `*` matches; no database in the string matches any; no user and no `PGUSER` may match any user but is no sure match; `host:port` and several hosts are split as Npgsql splits them; a malformed line ends the search; the default file is `.pgpass` in `HOME` on Unix and `postgresql/pgpass.conf` in `APPDATA` on Windows, alone.
- [ ] **Step 2:** Run them and see them fail.
- [ ] **Step 3:** Implement `PasswordFile.Target` and `PasswordFile.Passwords(text, target)`, `PathsOf(passfile, environment, windows)`, and pass them from `PostgresConnection.SecretsOf`.
- [ ] **Step 4:** Run the tests of `Describing/Postgres/`.

### Task 4: Documents

**Files:**
- Modify: `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`, `docs/tech-debt/README.md`, `docs/architecture/tool.md`, `site/reference/connections.md`, `site/reference/cli.md`, `site/guides/troubleshooting.md`, the remarks of `IQueryDescriber`, `SessionRunLog`, `CaptureJson` and `PostgresConnection`.

- [ ] **Step 1:** Rewrite TD-0034, its row and the limit to what remains: a short or run-word password outside a key, a keyed one split over strings that no joined search puts in order, a value over three or more strings out of order, a capture searched without pairs, a pipe, a provider, the owner's three decisions, and the false finds.
- [ ] **Step 2:** Run `./format.sh` and `./pre-commit-validation.sh`.
