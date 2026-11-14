# Keep your queries up to date

<a id="after-a-migration"></a>

## Describe again after a schema change

In a PostgreSQL project (`"dialect": "postgres"` in its `sqlsource.json`), after a migration:

```console
$ dotnet sqlsource describe --force
billing (postgres): 16 described, 0 skipped, 0 failed
$ git add '*.sql.json'
$ git commit -m "Describe the queries again after the migration"
```

Rules: [after a schema change](../reference/describing.md#after-a-schema-change), [describing again](../reference/sidecars.md#describing-again).
