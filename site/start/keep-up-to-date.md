# Keep your queries up to date

This page says what to commit to git, and what to do after a schema change or migration, a merge conflict, and in CI.

<a id="what-to-commit"></a>

## What to commit

- **Commit** your `.sql` files, their sidecars (`*.sql.json`), `sqlsource.json` and `dotnet-tools.json`.  Every build reads the sidecars, and a copy of the project without them reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each query.
- **Do not commit** your `.env` file, since it holds your password.
- **Never edit a sidecar by hand.**  The tool writes it.

Add this line to the `.gitattributes` file at the root of your repository:

```text
# .gitattributes
*.sql.json linguist-generated=true
```

GitHub then collapses the sidecars in the diff of a pull request.

<a id="the-build-describes"></a>

## The build describes for you

If the folder of your project has a `.env` file, `dotnet build` describes your queries before it compiles:

- If you change a query, the next build describes it again.
- If your database rejects a query, the build shows an error at that query.

Commit the sidecars that the build writes.  A build with no connection does not describe.  It reports [SQLSRC244](../diagnostics.md#sqlsrc244) for a query that changed since it was described.  A `.env` file at the root of your repository does not count: see [Give the tool your connection](first-query.md#connection).

<a id="after-a-migration"></a>

## After a schema change

No build sees a change to your schema, such as a migration.  After a migration, describe every query again.  Then commit the sidecars that changed:

# [PostgreSQL](#tab/postgresql)

```console
$ dotnet sqlsource describe --force
postgres (postgres): 1 described, 0 skipped, 0 failed
$ git add '*.sql.json'
$ git commit -m "Describe the queries again after the migration"
```

# [SQL Server](#tab/sqlserver)

```console
$ dotnet sqlsource describe --force
mssql (mssql): 1 described, 0 skipped, 0 failed
$ git add '*.sql.json'
$ git commit -m "Describe the queries again after the migration"
```

---

`--force` describes each query again, whether or not it changed.  The number in the line is the count of your project's queries, so yours can differ.  If the change broke a query, such as one that reads a column you dropped, the tool reports what your database said.

<a id="merge-conflict"></a>

## A merge conflict in a sidecar

If git reports a conflict in a sidecar:

1. Bring your database to the schema of the merged code, such as by running the migrations of the other branch.
2. Leave the conflict in the sidecar.
3. Run `dotnet sqlsource describe`.  It describes each query of the file again and writes the whole sidecar.
4. Commit what it writes.

Until then, the build reports [SQLSRC237](../diagnostics.md#sqlsrc237) for the sidecar.

<a id="in-ci"></a>

## In CI

A build in CI never describes.  It reads the committed sidecars.  So a query that someone changed and did not describe fails the build in CI with [SQLSRC244](../diagnostics.md#sqlsrc244).

The build cannot see a sidecar that a schema change made wrong.  To find that too, add a step to your CI job that describes against a database with your schema:

```console
$ dotnet sqlsource describe --check
```

`--check` changes no file.  It fails with [SQLSRC219](../diagnostics.md#sqlsrc219) when a sidecar is missing or out of date.

## If it does not work

- [SQLSRC244](../diagnostics.md#sqlsrc244): a query changed after it was described.  Build with your `.env` file in place, or run `dotnet sqlsource describe`.  Then commit the sidecar.
- [SQLSRC237](../diagnostics.md#sqlsrc237): a sidecar holds a merge conflict.  Run `dotnet sqlsource describe`.
- A build does not describe: the `.env` file is not in the folder of your project.

## What you have now

The sidecars in git, a build that describes what you change, and a check in CI.

To describe each query as you save it, see [Describe queries as you save (watch)](../guides/describe-on-save.md).  For a whole CI job, see [Check the sidecars in CI](../guides/ci.md).

Rules: [sidecars in git](../reference/sidecars.md#sidecars-in-git), [sidecar files](../reference/sidecars.md#sidecar-files), [running in the build](../reference/msbuild.md#running-in-the-build), [after a schema change](../reference/describing.md#after-a-schema-change), [describing again](../reference/sidecars.md#describing-again), [check in CI](../reference/describing.md#check-in-ci).

Next: [When something goes wrong](errors.md)
