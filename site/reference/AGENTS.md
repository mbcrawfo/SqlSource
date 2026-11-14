# AGENTS.md - site/reference

Reference: look-up, one page for each thing.  A reader arrives by search or by a link and does not read from the top.

- **One section defines each written name**: a settings-file key, a marker, a generator parameter, an attribute property, an MSBuild property, a command, an option, an environment variable.  The section is on the page of the thing the name changes, and every other page links it.
- **A setting's section is anchored `setting-<name>`** and holds, in this order: an example, the values, the default, the places it can be written, what is special about its precedence, and its errors.
- **A section opens with an example**, then the rules.
- **Reference states rules, not how-to.**  A procedure is a guide; link it.
- **A section stands alone.**  It names its subject, and links what it relies on, since the reader has not read the sections above it.
- **A new page gets an entry in `toc.yml`, under its group.**  A page not yet moved is listed there as `../<page>.md`, in the group it will join, until its phase of the documentation overhaul moves it.
- **`../diagnostics.md` is listed last, under Errors**, though it is at the root of `site/`.  `errors.md` holds how errors are reported and the areas with their ids, and no section of one error.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
