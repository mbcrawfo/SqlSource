# Sidecar files

A sidecar is the file beside a `.sql` file in which the `sqlsource` tool records what your database said about each query's parameters and columns: see [sidecar files](../tool.md#sidecar-files).

<a id="limits"></a>

## Limits

<a id="limit-file-in-two-projects"></a>

### Two projects include one `.sql` file, and entries go from its sidecar

- **A `.sql` file that two projects list.**  When two projects of one solution include the same `.sql` file, each with a type that claims it, the file has one sidecar.  Both projects read its settings, its [output](../settings.md#setting-output) among them, from its markers and the settings files of its own folder, so they need the same entries in it.  They differ when one project's build misses a settings file that the other finds.  A build target of your own that adds the file can cause it, as [the known issues of settings](../settings.md#known-issues) describe, and so can a settings file named in another case, such as `SqlSource.json`, that one project's file lists itself: that project does not read it, while the other finds it on macOS and Windows, as [its name](../settings.md#settings-files) says.  A query can then need an entry for one project and not for the other.  A run on only one of the two projects does not see what the other needs: it can write a sidecar that lacks entries the other project needs, or delete a sidecar that the other project needs, and the other project's build then reports [SQLSRC241](../diagnostics.md#sqlsrc241) for them.  The project in which a query has the output `sql` warns of the entry that the other project needs ([SQLSRC245](../diagnostics.md#sqlsrc245)).  Make your target run before the package lists the project's files, as that known issue shows, and name every settings file `sqlsource.json`, so that both projects find the same settings files, and describe the whole solution in one run.
