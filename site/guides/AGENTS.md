# AGENTS.md - site/guides

Guides: one task on each page, for a reader who knows what they want to do.

- **A guide is one task, in one shape**: "Use this when", in two sentences; the whole sample, every file complete enough to copy; "What you get"; "The rules you need"; a section for each variation; and last "If it does not work", what the reader sees with a link to the limit or the error.
- **Restate each rule the task needs in a sentence or two, and link the reference section that owns it.**  The page is complete for its task without the click.
- **Show a setting in use and link it.**  Never list its values, its default or its places: a guide owns a sample and a procedure, and no value, default or precedence.
- **A guide has a `## Limits` heading only when it owns a limit**, one that no reference page can own.  Otherwise it links the limit's anchor on the page that owns it.
- **The title is in the reader's words** and carries the word a junior types in a search box, such as "bulk", "mock", "nullable", "timeout", "IN" or "enum".  The first sentence carries the other search words.
- **A long difference between databases is two anchored sections**, never tabs.
- **A new guide goes under a group of `toc.yml`**: Queries, Running queries, Types, Projects, Workflow, or Other ways to use your SQL, in that order.  Add no group for one page, and let none pass nine entries.
- **`troubleshooting.md` is for a reader with a symptom and no `SQLSRC` id.**  It has an entry for each limit of the site, under what the reader sees: one or two sentences and a link to the limit's own anchor.  `SiteStructureTests` fails for a limit that has none.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
