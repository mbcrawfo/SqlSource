# SqlSource - C# SQL Query Source Generator

## Core rules

These apply to all development in this project.

1. **Document deferred work.**  Any work that is not implemented as planned must be documented in `docs/deferred`, in the same PR that defers it.
2. **Document tech debt.**  Any problem identified and not resolved, or any intentional choice that is known to be sub-optimal, must be documented in `docs/tech-debt`, in the same PR that discovers or introduces it.
3. **Keep the docs current, each in its own place.**  A change that makes one of these documents wrong updates it in the same PR.
   - `README.md` is for people who use the library: what it does, what it supports, how to install it and how to use it.  A change to behaviour a user can see, or to the supported environments, updates it.  Nothing about developing or releasing the project belongs there; link to the other two documents.  It is also packed as the package readme shown on nuget.org, so its links must be absolute URLs.
   - `CONTRIBUTING.md` is for people who work on the project: the tools a developer must install, how to build, test, check and pack locally, and what CI runs.  A change that adds, removes or re-pins a required tool, or that changes how the project is built, tested, checked or packed, updates it.
   - `docs/publishing.md` covers the version scheme, the release procedure and the configuration outside the repository that releasing depends on.  A change to any of those updates it.
   - `docs/diagnostics.md` is the list of diagnostics for people who use the library: what each `SQLSRC` error means and how to fix it.  A change that adds, removes or changes a diagnostic updates it.  A test in `tests/SqlSource.Tests/Diagnostics/` fails when its sections and the descriptors disagree; the explanations are yours to keep true.
4. **Every MSBuild property of the package starts with `SqlSource`.**  A property that `src/SqlSource/build/SqlSource.props` or `src/SqlSource/build/SqlSource.targets` sets, reads or shows to the compiler is named `SqlSource<Something>`, as `SqlSourceIncludeFiles` and `SqlSourceGeneratorParameters` are.  The prefix keeps the package's names apart from those of the SDK and of other packages in a consumer's project.  A property of the SDK that the files read keeps its own name and is listed in `SdkProperties` in `tests/SqlSource.Tests/Package/BuildFileTests.cs`, where a test fails on any other name without the prefix.

## Commits

Every commit must pass `./pre-commit-validation.sh`.  In Claude Code this is enforced: a `PreToolUse` hook, `.claude/hooks/pre-commit-validation.sh`, registered in `.claude/settings.json`, runs that script before any Bash command that contains `git commit` and blocks the command when a check fails.  Other agents run the script themselves before committing.

1. **Validate before you commit.**  Run `./pre-commit-validation.sh` first and fix what it reports.  A blocked commit only tells you that validation failed; the details come from running the script.
2. **Fix first, commit in a separate command.**  The hook runs before the command starts and checks the working tree as it is at that moment, not the staged content.  A command such as `./format.sh && git commit` is validated before `format.sh` has run.
3. **Never work around the hook.**  Do not reach `git commit` through an alias, a wrapper, a line continuation or any other spelling the hook does not match, and do not edit the hook or `.claude/settings.json` to get a commit through.  If a check is wrong, tell the user.
4. **Expect it to fire on the phrase, not the intent.**  Matching is textual, so a command that only mentions `git commit` inside a quoted string, or runs `git commit-tree` or `git commit-graph`, is validated too and is blocked when the tree fails.  Keep the phrase out of commands that do not commit.
5. **It needs the full toolchain.**  The commit is blocked when Docker is not running, when `jq` is not installed, or when the local tools have not been restored.  Fix the environment; see `CONTRIBUTING.md`.

## Pull requests

1. **Reply to every CodeRabbit finding.**  When you act on a comment, reply in its thread with what you changed.  When you do not act on one, reply in its thread with the reason: the rule or decision it conflicts with, or why the finding does not apply.  Never leave a finding unanswered or skip one silently.
2. **Check the version before you open a pull request.**  Run `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with the highest release tag, `git tag --list 'v*' --sort=-v:refname | head -n 1`.  If `VersionPrefix` is not greater than that tag, ask the user what the new version should be and set it in the same pull request.  When the repository has no `v*` tags there is nothing to compare.

## Adding guidance for AI agents

1. Always use AGENTS.md files for AI guidance.
2. Add folder-level AGENTS.md files to give AI agents non-obvious context to work safely with the code in that folder.  Add them where there is complex code with patterns or structure that may not be clear, or where additional context is beneficial when working with the code.
3. Be brief and direct.  Do not describe what the folder "does" - focus on actionable guidance that helps navigate the code and avoid pitfalls.  The ideal size is < 250 lines.  An AGENTS.md in the root of a project may be larger if necessary.
4. Do not repeat source code in an AGENTS.md file - link to the actual source files instead.  Brief code snippets that demonstrate a common pattern or pitfall are acceptable.
5. Keep lists of files and folders in sync.  Once an AGENTS.md names a specific file, folder, type, function, etc. it is coupled to that name.  When you rename, move, split, or delete those things, update the AGENTS.md **in the same commit**.  Treat it as part of the change, not a follow-up.  Any AGENTS.md that lists files or folders must end with a one-line maintenance footer reminding future editors of this coupling.
6. DO NOT list child AGENTS.md files from a parent AGENTS.md or instruct agents to read them - AGENTS.md files are loaded automatically by the tooling.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
