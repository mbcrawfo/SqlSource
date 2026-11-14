# Give a folder its own settings, or leave files out

<a id="configure-a-folder"></a>

## Configure one folder differently

A project reads its `.sql` files as PostgreSQL and keeps the comments of its queries.  One folder, `Legacy/`, holds MySQL files, and their queries take token fragments that are sometimes empty, so the folder turns the token check off.  A [settings file](../reference/settings-file.md#settings-files) in a folder gives it its own settings:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "generatorParameters": ["keep-comments"],
}
```

```jsonc
// Legacy/sqlsource.json
{
    "dialect": "mysql",
    "generatorParameters": ["token-validation=off"],
}
```

Every file under `Legacy/` is read as `mysql`, keeps its comments, and skips the token check.  Every other file is untouched: it is read as `postgres`, keeps its comments, and checks its tokens.  Three things to notice:

- The folder's list names only `token-validation=off`.  It adds to the project's list, so the files of `Legacy/` keep their comments without saying so.  A folder that wants none of the project's list starts its own with `default`: `["default", "token-validation=off"]` would give `Legacy/` every default but the token check.
- The dialect takes the nearest value whole.  Had the project's file set `"mysql,ansi-quotes"` as its dialect, this folder would still be read as plain `mysql`.
- A file's own markers still win.  A `-- generator:` line in one of the files adds its words to the list of both settings files, and `-- generator: default` drops that list, and a `-- dialect: mariadb` overrides both settings files.  See [which place wins](../reference/settings.md#precedence).

The file in `Legacy/` applies to the `.sql` files of its subfolders too, `Legacy/Reports/` among them, unless a settings file nearer to them sets the same setting.  Every setting of a query comes from the settings files of its `.sql` file, whichever type claims it, and never from those of a type's C# file.

Only the settings whose row in [the table](../reference/settings.md#all-settings) lists a settings file can be given this way.  To change a setting for one file, write its marker in the file header, which wins over every settings file.
