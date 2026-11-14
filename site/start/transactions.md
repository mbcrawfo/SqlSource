# Run queries in a transaction (or with a timeout)

At the end of this page, two queries run in one transaction that you commit or roll back, and a query has its own timeout.

<a id="queries-in-a-transaction"></a>

## Run queries in a transaction

A repository method takes no transaction, and each call gets a connection of its own.  So two calls of repository methods do not run in one transaction of yours.  Give each query that must run in the transaction the line `-- output: methods`.  The method of such a query takes your connection and your transaction.  By default, it is an `internal` extension method of the connection.

The sample is a second repository of the same application, `AccountRepository`.  It reads a table `accounts` with the columns `id`, `owner` and `balance`.  Its queries are in `AccountRepository.sql`, beside the class:

# [PostgreSQL](#tab/postgresql)

```sql postgres
-- name: GetAccount -> one-optional
SELECT id, owner, balance FROM accounts WHERE id = @accountId;

-- name: GetBalance -> one
-- output: methods
SELECT balance FROM accounts WHERE id = @accountId FOR UPDATE;

-- name: SetBalance -> none
-- output: methods
UPDATE accounts SET balance = @balance WHERE id = @accountId;
```

```csharp
using Npgsql;
using SqlSource;

[SqlSourceGenerate]
public partial class AccountRepository(NpgsqlDataSource dataSource)
{
    public async Task Withdraw(int accountId, decimal amount, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var account = await connection.GetBalance(
            new GetBalanceParams(accountId), transaction, cancellationToken: cancellationToken);
        await connection.SetBalance(
            new SetBalanceParams(account.Balance - amount, accountId), transaction, cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}

// Adds Withdraw to the generated interface, beside GetAccount
public partial interface IAccountRepository
{
    Task Withdraw(int accountId, decimal amount, CancellationToken cancellationToken = default);
}
```

# [SQL Server](#tab/sqlserver)

```sql mssql
-- name: GetAccount -> one-optional
SELECT id, owner, balance FROM accounts WHERE id = @accountId;

-- name: GetBalance -> one
-- output: methods
SELECT balance FROM accounts WITH (UPDLOCK) WHERE id = @accountId;

-- name: SetBalance -> none
-- output: methods
UPDATE accounts SET balance = @balance WHERE id = @accountId;
```

```csharp
using Microsoft.Data.SqlClient;
using SqlSource;

[SqlSourceGenerate]
public partial class AccountRepository(string connectionString)
{
    public async Task Withdraw(int accountId, decimal amount, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var account = await connection.GetBalance(
            new GetBalanceParams(accountId), transaction, cancellationToken: cancellationToken);
        await connection.SetBalance(
            new SetBalanceParams(account.Balance - amount, accountId), transaction, cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}

// Adds Withdraw to the generated interface, beside GetAccount
public partial interface IAccountRepository
{
    Task Withdraw(int accountId, decimal amount, CancellationToken cancellationToken = default);
}
```

---

`Withdraw` is a method of your own on the repository.  It opens a connection, begins the transaction, calls the two methods and commits.

- **Give every method the transaction.**  On SQL Server, a method that does not get it throws `InvalidOperationException`.
- **The `partial interface` adds `Withdraw` to `IAccountRepository`**, so code that takes the interface can call it.

<a id="timeout"></a>

## Set a command timeout

A repository method runs with the driver's default timeout.  If a query needs a timeout of its own, give it `-- output: methods` in the same way.  Its method takes the argument `commandTimeout`, in seconds:

# [PostgreSQL](#tab/postgresql)

```csharp
public async Task<decimal> GetBalanceSlowly(int accountId, CancellationToken cancellationToken = default)
{
    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
    var account = await connection.GetBalance(
        new GetBalanceParams(accountId), commandTimeout: 60, cancellationToken: cancellationToken);
    return account.Balance;
}
```

# [SQL Server](#tab/sqlserver)

```csharp
public async Task<decimal> GetBalanceSlowly(int accountId, CancellationToken cancellationToken = default)
{
    await using var connection = new SqlConnection(connectionString);
    var account = await connection.GetBalance(
        new GetBalanceParams(accountId), commandTimeout: 60, cancellationToken: cancellationToken);
    return account.Balance;
}
```

---

The method is part of `AccountRepository`.  A method of `-- output: methods` opens a closed connection itself, and closes it again before it returns.

<a id="two-outputs"></a>

## One file, two outputs

The output of a query says what SqlSource generates for it.  The default output, `repository`, gives a method of the repository, which opens its own connection.  The output `methods` gives a method that takes your connection.  One file can hold queries of both outputs: `GetAccount` stays a method of the repository.

## If it does not work

- `InvalidOperationException` on SQL Server, about a pending local transaction: give the method the transaction.
- [SQLSRC241](../diagnostics.md#sqlsrc241) for the new queries: describe them, as [Your first query](first-query.md#describe) shows.

## What you have now

A repository method of your own that runs two queries in one transaction, and a query with its own command timeout.

To insert many rows in one call and one transaction, see [Insert or update many rows in one call (bulk)](../guides/many-rows.md).  To compare all the outputs, see [Choose what is generated (output modes)](../guides/output-modes.md).

Rules: [transactions and timeouts](../reference/repositories.md#repository-transactions), [the connection](../reference/connection-methods.md#method-connection), [the signature](../reference/connection-methods.md#method-signature), [where the methods go](../reference/connection-methods.md#method-location), [the output](../reference/settings.md#setting-output), [the interface](../reference/repositories.md#repository-interface).

Next: [Test code that uses a repository](testing.md)
