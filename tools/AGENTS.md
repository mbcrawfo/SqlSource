# AGENTS.md - tools

The check scripts that `pre-commit-validation.sh` and `.github/workflows/build.yml` run, and the consumer project that one of them installs the package into.  `CONTRIBUTING.md` says what each check does; this file is the traps.

- **A check added here is added to both callers in the same commit**: `pre-commit-validation.sh` at the root and the `build` or `lint` job of `.github/workflows/build.yml`.  A check that needs the .NET SDK goes in `build`; one that needs only Docker goes in `lint`.
- **The lint scripts run their tool in Docker, at a pinned image.**  A new version is a change to the tag and its digest in the script, and nothing else.
- **`package-install/` is a consumer, not a project of the solution.**  `check-package-install.sh` copies it out of the repository, installs the packed package into the copy, builds and runs it, and compares what it prints with `package-install/expected-output.txt`.  A change to what a consumer sees from the generator or its MSBuild files adds a line to `package-install/Program.cs` and to that file together.
- **Its multi-line values are deliberate.**  `package-install/Consumer.csproj` and `package-install/Directory.Build.targets` write `SqlSourceGeneratorParameters` and `SqlSourceDialect` with one word on each line, in a property, on an item and in a file the SDK imports after the package's targets, to prove that the targets trim them wherever they are set.  Do not put them on one line.
- **`package-install/Queries/AddedByATarget.sql` is meant to be missing from the manifest.**  A target of `Directory.Build.targets` adds it from a hook of the build; the script checks that the generator sees it and the manifest target does not, which is `docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md`.
- **`check-tool-install.sh` runs the packed tool on the newest runtime of the machine**, with `DOTNET_ROLL_FORWARD=LatestMajor`, and is the one check that the tool rolls forward from .NET 8.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
