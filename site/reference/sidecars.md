# Sidecar files

<a id="sidecar-files"></a>

## Sidecar files

```text
Queries/
    Users.sql
    Users.sql.json      written by sqlsource describe
```

A sidecar is the file `Users.sql.json` beside `Users.sql`: a JSON file named after its `.sql` file.  It is the tool's record of what the database said about the parameters and the columns of each query of the file.  The [tool](cli.md#tool) writes it, you commit it, and you never edit it by hand: the tool writes the whole file.  The build reads it in place of a database.  The generator builds the [models](models.md#models) and the [query method](query-methods.md#query-methods) of a query from the entry of the query in the sidecar.  The build also checks the sidecar, and reports a query that must be described and has no current entry.

- **Commit the sidecars with the project.**  The build reads them in place of a database.  So every developer and every build server builds without a connection.  A copy of the project without the sidecars reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each query that must be described.
- **Only the person who changes a query, or the schema, needs a database**, to describe the query again.
- **By default, the build reads a sidecar and copies it nowhere.**  Like your `.sql` files, a sidecar is not in the build output, or in what `dotnet publish` writes.  This is true in a web project too.
- **A project that copies its sidecars itself still gets the copy.**  It does so with `CopyToOutputDirectory` or `CopyToPublishDirectory` on the items of the sidecars.  In a web project, that setting goes in `Directory.Build.targets`.

Rider shows each sidecar under its `.sql` file, with nothing to set.  Visual Studio Code nests files by a setting of its own.  To show each sidecar under its `.sql` file there, add this to your settings:

```json
{
    "explorer.fileNesting.enabled": true,
    "explorer.fileNesting.patterns": {
        "*.sql": "${capture}.sql.json"
    }
}
```

**Never edit a sidecar by hand.**  The tool writes the whole file.  To change what a sidecar says, change the query or the schema, and run `describe` again.  A sidecar with a merge conflict is no exception: see [sidecars in git](#sidecars-in-git).

To keep the sidecars current, run the command of each case:

| When | Run |
|----|----|
| You add, change, rename or remove a query, the database type or the nullability of a `-- param:` marker (not its C# type after `->`), the names of a `-- variable:` marker, or a token's default | `dotnet sqlsource describe`, or a build that [runs the tool](online-build.md#running-in-the-build) |
| The schema of the database changes | `dotnet sqlsource describe --force`: see [after a schema change](describing.md#after-a-schema-change) |
| While you work on your queries, to describe each one as you save it | `dotnet sqlsource describe --watch`: see [describe on save](describing.md#describe-on-save) |
| You update the tool | `dotnet sqlsource describe`, or a build that runs the tool, which describes every query again |
| On a build server, to catch a sidecar that someone forgot | `dotnet sqlsource describe --check`: see [check in CI](describing.md#check-in-ci) |

The build tells you when a sidecar is out of date, or cannot be read:

- A query that has no entry is [SQLSRC241](../diagnostics.md#sqlsrc241).
- A query that changed after its entry was written is [SQLSRC244](../diagnostics.md#sqlsrc244).
- An entry that names no query that must be described is the warning [SQLSRC245](../diagnostics.md#sqlsrc245).
- A sidecar, or an entry of one, that another version of the tool wrote is the warning [SQLSRC240](../diagnostics.md#sqlsrc240).
- A sidecar with a merge conflict in it is [SQLSRC237](../diagnostics.md#sqlsrc237).

The first three go away when you run `describe` and commit what it writes.  SQLSRC240 goes away once the tool and the `SqlSource` package have one version, and `describe` has run with that tool.

The build checks the sidecar of each `.sql` file that a type claims, even when no query of the file needs an entry.  If a `.sql` file has an error of its own, an id from 101 to 120 or [SQLSRC126](../diagnostics.md#sqlsrc126), the file has no queries to check.  Then the build checks only the sidecar itself, [SQLSRC237](../diagnostics.md#sqlsrc237) to [SQLSRC240](../diagnostics.md#sqlsrc240).  It reports no entry of that sidecar as one that names no query.

The JSON Schema [`sidecar-v1.schema.json`](https://github.com/mbcrawfo/SqlSource/blob/main/schemas/sidecar-v1.schema.json) describes the format of a sidecar, for the curious.  Nothing in this manual depends on it.

<a id="sidecars-in-git"></a>

## Sidecars in git

```text
# .gitattributes
*.sql.json linguist-generated=true
```

This line in the `.gitattributes` file at the root of your repository marks the sidecars as generated.  Then GitHub collapses them in the diff of a pull request.

- **Give the sidecars no other rule.**  Git merges two branches that changed one sidecar as it merges any text.  The build knows which version of each query an entry was written for.  So if a merge leaves an entry older than its query, the entry is [SQLSRC244](../diagnostics.md#sqlsrc244) until `describe` writes it again.
- **A rule that stops git merging sidecars, such as `-merge` or `binary`, does harm.**  It turns every sidecar that both branches changed into a conflict.  It also keeps the file of one side whole, and loses what `describe --force` wrote on the other side after a schema change.

**Before you describe after a merge, bring your database to the schema of the merged code.**  For example, apply the migrations that the other branch added.  `describe` writes what the database of your [connection](connections.md#connections) says.  So a run against the older schema writes the older description over the description of the other branch.  Each of the three procedures below starts with this step.

**After a merge that brings together a tool update and work described with the older tool:**

1. Bring your database to the schema of the merged code.
2. Run `dotnet sqlsource describe`, or a build that [runs the tool](online-build.md#running-in-the-build).
3. Commit what it writes.

Each entry of a sidecar keeps the version of the tool that wrote it.  So the build warns of an entry that the older tool wrote, [SQLSRC240](../diagnostics.md#sqlsrc240), and `describe` describes its query again.  If one branch updated the tool and the other changed a query or its logical database, git reports a conflict in the sidecar.

**After a merge that brings together a schema change and work described against the older schema:**

1. Bring your database to the schema of the merged code.
2. Run `dotnet sqlsource describe --force`, whichever branch you are on.  Do this even when git merged the sidecars without a conflict.
3. Commit what it writes.

Such a merge can keep an entry that one branch wrote against the older schema, for a query that has not changed since.  Neither the build nor `describe` reports that entry.  [`describe --check`](describing.md#check-in-ci) on a build server finds it.

**After a merge conflict in a sidecar:**

1. Bring your database to the schema of the merged code.
2. Leave the conflict in the sidecar.
3. Run `dotnet sqlsource describe`.
4. Commit what it writes.

A sidecar with a conflict in it cannot be read.  So the tool describes every query of its file again, and writes the whole file.  Until `describe` writes the file, the build reports [SQLSRC237](../diagnostics.md#sqlsrc237).

If you have already taken one side of the conflict, skip step 2.  In step 3, run `dotnet sqlsource describe --force` on that `.sql` file instead, such as `dotnet sqlsource describe --force Queries/Users.sql`.  The side you took can hold entries written before a schema change that the other side described.  Plain `describe` keeps an entry whose query has not changed.

<a id="describing-again"></a>

## Describing again

`describe` describes only what changed.  If the entry of a query is current, the tool skips the query, and does not send it to the database.  An entry stops being current when one of these changes:

- **The SQL of the query.**  A change to a comment alone does not count.
- **The database type or the nullability of a `-- param:` marker of the query**, `not null` included.  The C# type after `->` does not count, and neither does a `-- column:` marker.
- **The default of one of the tokens of the query.**
- **The names of a [`-- variable:` marker](parameters.md#variable-marker) of the query**, since they take names out of its query parameters.  Their order and spelling count too.
- **The dialect of the query, or its [logical database](connections.md#connections).**
- **The version of the tool.**  Each entry keeps the version that wrote it, and the tool describes again an entry that another version wrote.  So the tool describes again the whole of a sidecar that another version wrote.  Until it does, the build warns of it ([SQLSRC240](../diagnostics.md#sqlsrc240)).

The tool cannot see a change to the schema.  So it skips a query whose SQL is as it was, even when its table changed.  That is what `--force` is for.

**A sidecar is written whole or not at all.**  The tool leaves the sidecar of a file as it was in one of these cases:

- a query of the file fails;
- a filter leaves out a query of the file whose entry is missing or out of date.

If the run described any query of that file, [SQLSRC217](../diagnostics.md#sqlsrc217) says that the sidecar was left as it was.  Fix the query, or describe the whole file, and run `describe` again.

**When no query of a file needs an entry any more, the tool deletes the sidecar of the file.**  That is when each query of the file was removed, or now has the output `sql`.  The tool deletes a sidecar only when all of these are true:

- the run has no `--project`, `--database` or `.sql` path;
- the run reported no error;
- the run does not say that it is offline.

So a run over part of the project never removes what another part needs.  When no database with a query to describe has a connection, a run of the tool by the build says that it is offline.  See [running in the build](online-build.md#running-in-the-build).

**A run that is stopped while it writes a sidecar leaves the old file or the new one**, never part of either.  The run can leave a temporary file beside the sidecar, with a name like `Users.sql.json.<id>.tmp`.  The next `describe` of the whole project removes that file once it is a minute old.

<a id="limits"></a>

## Limits

<a id="limit-file-in-two-projects"></a>

### Two projects include one `.sql` file, and entries are missing from its sidecar

**What you see.**  Two projects of one solution include the same `.sql` file, each with a type that claims it.  You can see one of these, or both:

- **Entries are missing from the sidecar of the file, or vanish from it**, or the sidecar itself is gone, after a run of `describe` on one of the two projects alone, or after its build describes it.  The build of the other project then reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each query that lost its entry.
- **The build of one project warns of an entry that the other project needs** ([SQLSRC245](../diagnostics.md#sqlsrc245)).  It is the project in which the query has the output `sql`.

**Why.**  The file has one sidecar for both projects.  Both projects read the settings of the file, its [output](settings.md#setting-output) among them, from its markers and from the settings files of its own folder.  So the two projects need the same entries in the sidecar.  They differ when the build of one project misses a settings file that the other project finds for the `.sql` file.  A query can then need an entry for one project and not for the other.  One of these can cause it:

- A build target of your own that adds the `.sql` file, as [a limit](msbuild.md#limit-file-added-by-a-target) describes.
- A settings file named in another case, such as `SqlSource.json`, that the project file of one project lists itself.  That project does not read the settings file.  The other project finds it on macOS and Windows, as [its name](settings-file.md#settings-files) says.

A run on the solution sees what both projects need, with `--project` too.  If `--project` leaves the other project out, the run still reads that project for the files it shares.  The run keeps the entries that only that project needs, and does not describe them.  If such an entry is out of date, the run leaves the sidecar as it was.  It reports [SQLSRC217](../diagnostics.md#sqlsrc217) only if it described another query of the file, and otherwise says nothing.  A run on the solution without `--project` updates the entry.

If a query that only the other project uses has an error, such as a token with no default, the run reports it, as the build of that project does.  If the run cannot read the other project, as when it is not restored, it does not see what that project needs.

A run on one project alone does not see what the other project needs, and the build of one project does not either.  Such a run can write a sidecar that lacks entries that only the other project needs.  It can also remove those entries, or delete a sidecar that the other project needs.

**What to do.**  Make both projects find the same settings files, and describe the solution:

1. If a build target of your own adds the file, make it run before the package lists the project's files.  [That limit](msbuild.md#limit-file-added-by-a-target) shows how.
2. Name every settings file `sqlsource.json`.
3. Describe the solution, with `--project` if you want one project, never the project alone.
