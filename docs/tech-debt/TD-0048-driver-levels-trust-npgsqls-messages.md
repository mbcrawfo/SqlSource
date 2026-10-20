# TD-0048 - The driver's messages are forwarded at levels that trust Npgsql not to name the host, the user or the database

## Problem

A run with a log forwards Npgsql's own messages into it as `postgres.driver` events ([`NpgsqlRunLogger`](../../src/SqlSource.Tool/Describing/Postgres/NpgsqlRunLogger.cs)): the categories `Npgsql.Command` and `Npgsql.Transaction` from Debug up, and every other category from Warning up.  The log is a file that a user may attach to an issue, and it must hold no host, no user, no database's own name, no application name and no password.  The tool cannot make sure of that for a message of the driver:

- The connection's whole value is removed from a message ([`SessionRunLog`](../../src/SqlSource.Tool/Logging/SessionRunLog.cs)), and each of its passwords where it stands as a token, and no other part of it, as [TD-0034](TD-0034-redaction-of-a-describers-text-has-holes.md) says.  A message that names the host, the user or the database is written.
- What keeps those out is the choice of levels, and it rests on what Npgsql says at them.  At Debug, `Npgsql.Command` and `Npgsql.Transaction` say the text of a command, its statement and its duration; they are not expected to name an endpoint.  At Warning and above, the other categories say what went wrong with a connection, a copy or a replication stream, and none is expected to name the host, the user, the database or the application.  `Npgsql.Connection` is the category that names the endpoint of a failed connect, and its exception is dropped for that reason; its messages at Warning are forwarded.  `Npgsql.Exception` is forwarded from Warning up like any other category, and its events would carry `exception.Message`, which can name an endpoint; no test reaches one, since Npgsql 10.0.3 writes none for a refused or a rejected connect.
- Npgsql's text is not a contract.  A new version may add a message at one of these levels, or add the host to one, and nothing in the tool would see it.

## Why it exists

Sub-phase 3.6 wanted the driver's account of what it sent, to explain a run that went wrong, and the driver reports through `ILoggerFactory` only.  Forwarding it needs a level for each category, and the levels are a judgement about messages the tool does not write.  The alternative, to forward nothing, gives a log without the SQL the driver sent.

## Impact

Low.  Only a run with a log, by `--log` or by `SQLSOURCE_LOG`, forwards anything, and only a message that names the host, the user or the database, at a level the tool forwards, reaches the file.  Three container tests of sub-phase 3.6, in `DescribeCommandTests`, hold it for the Npgsql the tool pins, 10.0.3.  `Describe_WithLog_HoldsDriverEventsAndNoConnectionWord` describes through a connection whose user, database and application name are words of its own, and finds none of them, nor the host, anywhere in the log, which holds the driver's events of `Npgsql.Command`.  `Describe_ConnectionRefusedWithLog_IsSqlsrc224AndNothingPrintedOrLoggedHoldsTheHostOrTheUser` is refused by a host that is up, on port 1, with a role of its own, and checks the whole log, standard output and standard error for the host and the role.  `Describe_WrongPasswordWithLog_IsSqlsrc224AndNothingPrintedOrLoggedHoldsTheUserOrTheHost` is the same for the commonest real failure, a server that rejects the password of a role, and checks all three for the role, the host and the database.  The error of each holds the describer's own text and no driver's message, and Npgsql 10.0.3 writes no driver event for either, so what those two hold of the levels is for a version that writes one.

## Proposed fix

When Npgsql is updated, run the three tests against the new version and read the release notes for its logging: a message at Debug of `Npgsql.Command` or `Npgsql.Transaction`, or at Warning or above of any category, that names an endpoint, a user or a database is a reason to raise the level of its category, or to drop its message.  If any of the three ever fails, the levels are wrong and not the test.  A lasting fix is to forward by event id instead of by category: a list of the ids that are known to be safe, written in `NpgsqlRunLogger`, so that a message that is new to a version, and every Warning and above of `Npgsql.Exception` with its `exception.Message`, is dropped until someone reads it.

## Trigger

An update of Npgsql, past 10.0.3.
