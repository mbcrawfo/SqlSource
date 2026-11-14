# Sidecar files

<a id="sidecar-files"></a>

## Sidecar files

A sidecar is the tool's record of what the database said about each query's parameters and columns.  It is a JSON file named after the `.sql` file, beside it: `Users.sql.json` for `Users.sql`.

```text
Queries/
    Users.sql
    Users.sql.json      written by sqlsource describe
```

**Commit the sidecars with the project.**  The build reads them in place of a database, so that every developer and every build server builds without a connection.  A copy of the project without them reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each query that must be described.  Only the person who changes a query, or the schema, needs a database to describe it again.  By default the build reads a sidecar and copies it nowhere: like your `.sql` files, it is not in the build output or in what `dotnet publish` writes, in a web project too.  A project that copies its sidecars itself, with `CopyToOutputDirectory` or `CopyToPublishDirectory` on their items, still gets the copy; in a web project that setting goes in `Directory.Build.targets`.

Rider shows each sidecar under its `.sql` file, with nothing to set.  To show each sidecar under its `.sql` file in Visual Studio Code, which nests files by a setting of its own, add this to your settings:

```json
{
    "explorer.fileNesting.enabled": true,
    "explorer.fileNesting.patterns": {
        "*.sql": "${capture}.sql.json"
    }
}
```

**Never edit a sidecar by hand.**  The tool writes it whole.  To change what a sidecar says, change the query or the schema and run `describe` again.  A sidecar with a merge conflict is no exception: see [sidecars in git](#sidecars-in-git).

To keep the sidecars current:

| When | Run |
|----|----|
| You add, change, rename or remove a query, the database type or the nullability of a `-- param:` marker (not its C# type after `->`), or a token's default | `dotnet sqlsource describe`, or a build that [runs the tool](msbuild.md#running-in-the-build) |
| The schema of the database changes | `dotnet sqlsource describe --force`: see [after a schema change](describing.md#after-a-schema-change) |
| While you work on your queries, to describe each one as you save it | `dotnet sqlsource describe --watch`: see [describe on save](describing.md#describe-on-save) |
| You update the tool | `dotnet sqlsource describe`, or a build that runs the tool, which describes every query again |
| On a build server, to catch a sidecar that someone forgot | `dotnet sqlsource describe --check`: see [check in CI](describing.md#check-in-ci) |

The build tells you when a sidecar is not current.  A query without an entry is [SQLSRC241](../diagnostics.md#sqlsrc241), and one that changed since its entry was written is [SQLSRC244](../diagnostics.md#sqlsrc244).  An entry that names no query that must be described is the warning [SQLSRC245](../diagnostics.md#sqlsrc245), and a sidecar, or an entry of one, that another version of the tool wrote the warning [SQLSRC240](../diagnostics.md#sqlsrc240).  Each of the first three goes away when you run `describe` and commit what it writes.  SQLSRC240 goes away once the tool and the `SqlSource` package have one version and `describe` has run with that tool.  A sidecar with a merge conflict in it is [SQLSRC237](../diagnostics.md#sqlsrc237).

A sidecar that exists is checked even when no query of its file needs an entry.  While a `.sql` file has an error of its own, an id from 101 to 120 or [SQLSRC126](../diagnostics.md#sqlsrc126), it has no queries to check, so the build checks only the sidecar itself, [SQLSRC237](../diagnostics.md#sqlsrc237) to [SQLSRC240](../diagnostics.md#sqlsrc240), and reports no entry of it as one that names no query.

The format of a sidecar is described by its JSON Schema, [`sidecar-v1.schema.json`](https://github.com/mbcrawfo/SqlSource/blob/main/schemas/sidecar-v1.schema.json), for the curious.  Nothing in this manual depends on it.

<a id="sidecars-in-git"></a>

## Sidecars in git

```text
# .gitattributes
*.sql.json linguist-generated=true
```

This line in the `.gitattributes` file at the root of your repository marks the sidecars as generated, so that GitHub collapses them in the diff of a pull request.  Give them no other rule.  Git merges two branches that changed one sidecar as it merges any text, and the build knows which version of each query an entry was written for, so an entry that a merge leaves older than its query is [SQLSRC244](../diagnostics.md#sqlsrc244) until `describe` writes it again.  A rule that stops git merging sidecars, such as `-merge` or `binary`, would turn every sidecar that both branches changed into a conflict, and would keep one side's file whole, losing what the other side's `describe --force` wrote after a schema change.

**After a merge that brings together a tool update and work described with the older tool**, run `dotnet sqlsource describe`, or a build that [runs the tool](msbuild.md#running-in-the-build), and commit what it writes.  Each entry of a sidecar keeps the version of the tool that wrote it, so the build warns of an entry that the older tool wrote, [SQLSRC240](../diagnostics.md#sqlsrc240), and `describe` describes its query again.  Where one branch updated the tool and the other changed a query or its logical database, git reports a conflict in the sidecar.

**After a merge that brings together a schema change and work described against the older schema**, whichever branch you are on, run `dotnet sqlsource describe --force` and commit what it writes, even when git merged the sidecars without a conflict.  Such a merge can keep an entry that one branch wrote against the older schema, for a query that has not changed since, and neither the build nor `describe` reports it.  [`describe --check`](describing.md#check-in-ci) on a build server finds it.

**After a merge conflict in a sidecar**, leave the conflict in the file and run `dotnet sqlsource describe`.  A sidecar with a conflict in it cannot be read, so the tool describes every query of its file again and writes the file whole: commit what it writes.  Until then the build reports [SQLSRC237](../diagnostics.md#sqlsrc237).  If you have already taken one side of the conflict, run `dotnet sqlsource describe --force` on that `.sql` file instead, such as `dotnet sqlsource describe --force Queries/Users.sql`: the side you took may hold entries written before a schema change that the other side described, and plain `describe` keeps an entry whose query has not changed.

**Before any of these runs**, bring your database to the schema of the merged code, such as by applying the migrations that the other branch added.  `describe` writes what your [connection](connections.md#connections)'s database says, so a run against the older schema writes the older description over the other branch's.

<a id="describing-again"></a>

## Describing again

`describe` describes only what changed.  A query whose sidecar entry is current is skipped, and is not sent to the database.  An entry stops being current when any of these change:

- The query's SQL.  A change to a comment alone does not count.
- The database type or the nullability of a `-- param:` marker of the query, `not null` included, or the default of one of its tokens.  The C# type after `->`, and a `-- column:` marker, do not count.
- The query's dialect, or its [logical database](connections.md#connections).
- The version of the tool: each entry keeps the version that wrote it, and an entry that another version wrote is described again, so a sidecar that another version wrote is described again whole.  Until it is, the build warns of it ([SQLSRC240](../diagnostics.md#sqlsrc240)).

The tool cannot see a change to the schema, so a query whose SQL is as it was is skipped even when its table changed.  That is what `--force` is for.

A sidecar is written whole or not at all.  When a query of a file fails, or a filter leaves one out whose entry is not current, the file's sidecar is left as it was, and [SQLSRC217](../diagnostics.md#sqlsrc217) says so if the run described any of its queries.  Fix the query, or describe the whole file, and run `describe` again.

When no query of a file needs an entry any more, because each was removed or now has the output `sql`, the tool deletes the file's sidecar.  It does so only in a run with no `--project`, `--database` or `.sql` path, and only when the run reported no error, so that a run over part of the project never removes what another part needs.

A run that is stopped while it writes a sidecar leaves the old file or the new one, never part of either.  It can leave a temporary file beside it, named like `Users.sql.json.<id>.tmp`; the next `describe` of the whole project removes it once it is a minute old.

<a id="limits"></a>

## Limits

<a id="limit-file-in-two-projects"></a>

### Two projects include one `.sql` file, and entries are missing from its sidecar

- **A `.sql` file that two projects list.**  When two projects of one solution include the same `.sql` file, each with a type that claims it, the file has one sidecar.  Both projects read its settings, its [output](../settings.md#setting-output) among them, from its markers and the settings files of its own folder, so they need the same entries in it.  They differ when one project's build misses a settings file that the other finds.  A build target of your own that adds the file can cause it, as [the known issues of settings](../settings.md#known-issues) describe, and so can a settings file named in another case, such as `SqlSource.json`, that one project's file lists itself: that project does not read it, while the other finds it on macOS and Windows, as [its name](../settings.md#settings-files) says.  A query can then need an entry for one project and not for the other.  A run on only one of the two projects does not see what the other needs: it can write a sidecar that lacks entries the other project needs, or delete a sidecar that the other project needs, and the other project's build then reports [SQLSRC241](../diagnostics.md#sqlsrc241) for them.  The project in which a query has the output `sql` warns of the entry that the other project needs ([SQLSRC245](../diagnostics.md#sqlsrc245)).  Make your target run before the package lists the project's files, as that known issue shows, and name every settings file `sqlsource.json`, so that both projects find the same settings files, and describe the whole solution in one run.

- **A `.sql` file that two projects list, when only one of them finds a settings file of it,** has one sidecar for both, and a run on one of the two projects can leave out, or remove, the entries that only the other needs.  This takes, for example, a build target of your own that adds the file, or a settings file named `SqlSource.json` that one of the projects lists itself.  Make both projects find the same settings files, and describe the whole solution in one run.
