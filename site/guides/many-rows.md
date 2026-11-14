# Insert or update many rows in one call (bulk)

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

`-- generator: collection-method` gives `AddUser` a second method, which takes the input model of every user, adds them all in one transaction of its own, 1,000 to a round trip, and returns their ids in the order of the users.  When the database refuses one, such as a user whose row breaks a constraint, it throws `CollectionItemException`, whose `ItemIndex` is that user's position, and no user is added.  The same query runs on SQL Server with `OUTPUT inserted.id` before `VALUES` in place of `RETURNING id`.  At the output `methods`, the method also takes a transaction that your code began, and runs in it.

Rules: [collection methods](../reference/collection-methods.md#collection-methods), [transactions](../reference/collection-methods.md#collection-transactions), [`CollectionItemException`](../reference/exceptions.md#collection-item-exception).
