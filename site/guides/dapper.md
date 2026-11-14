# Use Dapper or your own ADO.NET code

<a id="limits"></a>

## Limits

<a id="limit-parameter-names"></a>

### Dapper sends no value for a parameter such as `@account_id`

- **An input model binds through Dapper only when the query writes its parameters as the model's properties are named.**  Dapper sends a property of a parameter object only to a parameter that the SQL writes as `@` and the property's name, ignoring case but not `_`.  So the property `AccountId` binds to `@accountId`, and a query that writes `@account_id` gets no value from it.  Write the query's parameters in camelCase, or give Dapper the parameters yourself, as in `new { account_id = args.AccountId }`: see [the recipe](../recipes.md#models-through-dapper).
