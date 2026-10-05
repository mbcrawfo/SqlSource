# SqlSource - C# SQL Query Source Generator

## Core rules

These apply to all development in this project.

1. **Document deferred work.**  Any work that is not implemented as planned must be documented in `docs/deferred`, in the same PR that defers it.
2. **Document tech debt.**  Any problem identified and not resolved, or any intentional choice that is known to be sub-optimal, must be documented in `docs/tech-debt`, in the same PR that discovers or introduces it.
3. **Keep the README current.**  `README.md` lists the tools a developer must install and how to build and test the project locally.  A change that adds, removes or re-pins a required tool, or that changes how the project is built, tested, checked or packed, updates `README.md` in the same PR.
4. **Rebase only.  Never merge.**  `main` has a linear history and the repository allows only "Rebase and merge": each commit of a pull request lands on `main` as itself.  Never merge `main` into a branch, by hand (`git merge`, or `git pull` without `--rebase`) or through a tool that does it for you.  A merge commit makes the pull request unmergeable, and a conflict resolved inside one is lost when the commits are replayed.  Bring a branch up to date with `git fetch origin` and `git rebase origin/main`, resolve each conflict in the commit that causes it, and push with `--force-with-lease`, never `--force`.  Ask the user before any force-push.  If a merge is already in progress, abort it (`git merge --abort`) and rebase.

## Pull requests

1. **Reply to every CodeRabbit finding.**  When you act on a comment, reply in its thread with what you changed.  When you do not act on one, reply in its thread with the reason: the rule or decision it conflicts with, or why the finding does not apply.  Never leave a finding unanswered or skip one silently.

## Adding guidance for AI agents

1. Always use AGENTS.md files for AI guidance.
2. Add folder-level AGENTS.md files to give AI agents non-obvious context to work safely with the code in that folder.  Add them where there is complex code with patterns or structure that may not be clear, or where additional context is beneficial when working with the code.
3. Be brief and direct.  Do not describe what the folder "does" - focus on actionable guidance that helps navigate the code and avoid pitfalls.  The ideal size is < 250 lines.  An AGENTS.md in the root of a project may be larger if necessary.
4. Do not repeat source code in an AGENTS.md file - link to the actual source files instead.  Brief code snippets that demonstrate a common pattern or pitfall are acceptable.
5. Keep lists of files and folders in sync.  Once an AGENTS.md names a specific file, folder, type, function, etc. it is coupled to that name.  When you rename, move, split, or delete those things, update the AGENTS.md **in the same commit**.  Treat it as part of the change, not a follow-up.  Any AGENTS.md that lists files or folders must end with a one-line maintenance footer reminding future editors of this coupling.
6. DO NOT list child AGENTS.md files from a parent AGENTS.md or instruct agents to read them - AGENTS.md files are loaded automatically by the tooling.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
