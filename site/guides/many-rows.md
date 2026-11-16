# Insert or update many rows in one call (bulk)

Run one query for many rows in one call, such as a bulk insert or a batch of updates, with a collection method.

**Use this when** your code holds many items, such as new users, and each item needs the same insert or update.  Use it too when a failure must name the item that the database refused.

<a id="insert-many-rows"></a>

## Insert many rows in one call

```sql postgres
-- name: AddUser -> one
-- generator: collection-method
INSERT INTO users (name, email)
VALUES (@name, @email)
RETURNING id;
```

```csharp
[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);

// elsewhere
var newUsers = signups.Select(signup => new AddUserParams(signup.Name, signup.Email));
AddUserDto[] added = await users.AddUser(newUsers, cancellationToken);
```

**What you get.**  `-- generator: collection-method` gives `AddUser` a second method.  It takes the input model of every user, and adds them all in one transaction of its own.  It returns their ids in the order of the users.

**The rules you need.**

- **On SQL Server, the same query runs with `OUTPUT inserted.id` before `VALUES`, in place of `RETURNING id`.**  See [collection methods](../reference/collection-methods.md#collection-methods).
- **To run the method in a transaction that your code began**, see [The transaction it runs in](#transaction), below, and [transactions](../reference/collection-methods.md#collection-transactions).
- **To find the user that the database refused**, see [When one item fails](#failures), below, and [`CollectionItemException`](../reference/exceptions.md#collection-item-exception).

<a id="transaction"></a>

## The transaction it runs in

Read this to know which transaction a collection method runs in, or to run it in a transaction of your own.

```sql postgres
-- name: AddUser -> one
-- generator: collection-method
INSERT INTO users (name, email)
VALUES (@name, @email)
RETURNING id;
```

```csharp
// At the output methods
await connection.OpenAsync(cancellationToken);
await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
AddUserDto[] added = await connection.AddUser(newUsers, transaction, cancellationToken: cancellationToken);
// Run the other queries of the transaction here, each with the transaction.
await transaction.CommitAsync(cancellationToken);
```

**What you get.**  `AddUser` adds every user in your transaction.  It neither commits the transaction nor rolls it back: your code does.

**The rules you need.**

- If you give it no transaction, a collection method begins its own.  It commits it after the last item, and rolls it back if anything throws, so no item is left.  See [Transactions](../reference/collection-methods.md#collection-transactions).
- To set the isolation level of that transaction, write it on the same line: `-- generator: collection-method transaction=serializable`.  `transaction=off` begins no transaction, so a failure leaves the items before it.  See [`transaction`](../reference/generator-parameters.md#gp-transaction).
- At the output `methods`, the method uses the transaction that you give it, whatever `transaction` says.  After an exception, what ran is still in your transaction, and you decide whether to roll it back or commit it.  See [Transactions](../reference/collection-methods.md#collection-transactions).
- If a `TransactionScope` is open, the method begins no transaction, and runs in the scope's.  This is how to put a repository's collection method, which takes no transaction, in a larger one.  A connection that is not in the scope, such as one opened before it, runs the items in no transaction at all.  See [Transactions and timeouts](../reference/repositories.md#repository-transactions).

<a id="failures"></a>

## When one item fails

Read this to find the item that the database refused, such as a user whose row breaks a constraint.

```csharp
try
{
    await users.AddUser(newUsers, cancellationToken);
}
catch (CollectionItemException exception)
{
    // The database refused one user, so no user was added.
    Console.WriteLine($"User {exception.ItemIndex} was refused: {exception.InnerException?.Message}");
}
```

**What you get.**  The method's own transaction is rolled back, so no user is added.  `ItemIndex` tells you which user the database refused.

**The rules you need.**

- A database error at an item is thrown as `CollectionItemException`.  Its `ItemIndex` is the position of the item, counted from 0, and its `InnerException` is the driver's exception, a `PostgresException` or a `SqlException`.  See [What it throws](../reference/collection-methods.md#collection-failures).
- It derives from `DbException`, and its `SqlState` and `IsTransient` are those of its inner exception.  So code that catches `DbException`, or a retry policy that tests `IsTransient`, works unchanged.  See [`CollectionItemException`](../reference/exceptions.md#collection-item-exception).
- Only a database error at an item is wrapped.  An exception of the method's own, such as `UnexpectedRowCountException`, is thrown as it is.  So is an error that no item raised, such as a deferred constraint that the commit checks.  See [What it throws](../reference/collection-methods.md#collection-failures).
- The items run 1,000 at a time, so a bad item is found only after the round trips before its own.  In your transaction, or under `transaction=off`, the items of those round trips stay.  See [Collection methods](../reference/collection-methods.md#collection-methods).

## If it does not work

- **`NotSupportedException`, "Specified method is not supported.", or a database error that names no item.**  The connection wraps the driver's connection.  Give the method the driver's own connection.  See [the limit](../reference/collection-methods.md#limit-wrapped-connection).
- **`TransactionRolledBackException`, on SQL Server.**  You gave the method a transaction, and a later error of the query rolled it back.  The first error is its `InnerException`, such as the `CollectionItemException` of the item that failed first.  Begin another transaction to go on.  See [`TransactionRolledBackException`](../reference/exceptions.md#transaction-rolled-back).
- **SQLSRC297.**  `transaction` names an isolation level that the database does not have.  Write a level it has.  See [SQLSRC297](../diagnostics.md#sqlsrc297).
