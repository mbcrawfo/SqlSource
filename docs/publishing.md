# Publishing

How SqlSource is versioned and how it is released to nuget.org and PyPI.  Building and testing locally are covered in [CONTRIBUTING.md](../CONTRIBUTING.md).

A release is three packages: `SqlSource`, the generator, and `SqlSource.Tool`, the `sqlsource` command, on nuget.org, and `sqlfluff-templater-sqlsource`, the sqlfluff templater, on PyPI.  They are built by one run, carry one version and are published together.

## Versioning

`VersionPrefix` in `Directory.Build.props` is the `major.minor.patch` of the next release, and the only version number edited by hand.  The workflows add the rest.  With `VersionPrefix` 0.1.0, pull request 42 and run 123:

| Build | Package version | Assembly and file version | Templater version |
|----|----|----|----|
| Pull request | `0.1.0-pr-42.123` | `0.1.0.123` | `0.1.0.dev123` |
| `main` or an `integration/*` branch | `0.1.0-beta.123` | `0.1.0.123` | `0.1.0b123` |
| Tag `v0.1.0` | `0.1.0` | `0.1.0.<run>` | `0.1.0` |
| Local | `0.1.0-dev` | `0.1.0.0` | `0.0.0.dev0` |

The first two columns hold for both NuGet packages.  The templater's version is the package version in the form PyPI takes, [PEP 440](https://peps.python.org/pep-0440/), which [`tools/templater-version.sh`](../tools/templater-version.sh) gives and `build.yml` sets before it builds the wheel and sdist; a local build keeps the placeholder of `pyproject.toml`.  The informational version is the package version followed by `+<commit sha>`, and it is what `sqlsource --version` prints.  Run numbers are counted per workflow, so a beta published by `publish.yml` does not share a number with the `ci.yml` artifact for the same commit.

## Releasing

Packages go to nuget.org through [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), and the templater to PyPI through [trusted publishing](https://docs.pypi.org/trusted-publishers/).  The repository holds no API key or token.

### Release

1. Set `VersionPrefix` in `Directory.Build.props` to the version being released.  If that changes it, run `tools/describe-end-to-end.sh` in the same pull request, as step 7 says.
2. In the same pull request, record the diagnostics the release ships.  Move every entry of `src/SqlSource/AnalyzerReleases.Unshipped.md` to `src/SqlSource/AnalyzerReleases.Shipped.md`, under a new `## Release <version>` heading, keeping the `### New Rules`, `### Removed Rules` and `### Changed Rules` tables as they are.  Leave the unshipped file with its two header lines only.  Skip this when the unshipped file has no entries.  Nothing checks that it was done, and a release that skips it makes the next removal or change of a diagnostic impossible to record correctly.
3. For the first stable release only, in the same pull request, switch the NuGet badge of `README.md` from `nuget/vpre` to `nuget/v` if it should show releases alone, and take out the rule of `schemas/AGENTS.md` that lets `settings-v1.schema.json` and `sidecar-v1.schema.json` change in place before the first stable release, with the paragraph of `docs/architecture/sidecar-format.md`'s compatibility that says the same.
4. Merge that to `main`.
5. Tag the merged commit and push the tag:

   ```bash
   git tag v0.2.0
   ```

   ```bash
   git push origin v0.2.0
   ```

6. `publish.yml` builds the tag and runs two jobs side by side.  `publish` fails unless the build gave exactly `SqlSource.0.2.0.nupkg` and `SqlSource.Tool.0.2.0.nupkg`, and pushes both to nuget.org.  `publish-pypi` fails unless it gave exactly `sqlfluff_templater_sqlsource-0.2.0-py3-none-any.whl` and `sqlfluff_templater_sqlsource-0.2.0.tar.gz`, and pushes both to PyPI.  Each matches when the tag is `v<VersionPrefix>`.  When both jobs pass, the workflow creates the GitHub release with all four files attached, and then publishes the manual's site from the tag through `docs.yml`.
   - If the site fails to publish, the packages and the release stand: run `docs.yml` again from the tag, `gh workflow run docs.yml --ref v0.2.0`.
   - If a push fails, run the failed jobs again, from the run's page or with `gh run rerun <run id> --failed`.  `--skip-duplicate` passes over a NuGet package that is already pushed, and `skip-existing` over a file that is already on PyPI, so a rerun is safe after a partial push.  The release and the site wait for both pushes, and run in the rerun.
7. Raise `VersionPrefix` in the next pull request.  Until then, betas built from `main` sort below the release just published.  In that pull request, run `tools/describe-end-to-end.sh` and commit the sidecars it rewrites: every sidecar of the end-to-end queries, the realistic cases and the examples holds the tool's version, and the build warns with `SQLSRC240` until they are written again.  [`tools/README.md`](../tools/README.md#recording-scripts) has the script.

### Beta

Run `publish.yml` by hand on `main`, from the Actions tab or with:

```bash
gh workflow run publish.yml --ref main
```

It publishes `<VersionPrefix>-beta.<run>` of both NuGet packages, and `<VersionPrefix>b<run>` of the templater, a pre-release that pip installs only when asked.  A run started from any other branch builds and publishes nothing.

### Site

The manual, `site/`, is published to GitHub Pages at `https://mbcrawfo.github.io/SqlSource/`, where the help link of every diagnostic and the links of the three packages' readmes go.  It shows one version, the latest release's: a release publishes it, and a beta does not.  To publish it by hand, the first time and again after a fix to a release's pages, run `docs.yml` on `main`:

```bash
gh workflow run docs.yml --ref main
```

It builds the branch it runs on, so a run on `main` between releases publishes pages that may describe what is not released yet.  It is not the way to keep the site current between releases.

### External configuration

| Where | Setting | Value |
|----|----|----|
| nuget.org trusted publishing policy | Repository owner and repository | `mbcrawfo`, `SqlSource` |
| | Workflow file | `publish.yml` |
| | Environment | `nuget` |
| GitHub environment `nuget` | Deployment branches and tags | Branch `main`, tags `v*` |
| | Required reviewers | None |
| | Allow administrators to bypass | Off |
| | Variable `NUGET_USER` | The nuget.org profile name, not the email address |
| PyPI trusted publisher of `sqlfluff-templater-sqlsource` | Owner and repository | `mbcrawfo`, `SqlSource` |
| | Workflow | `publish.yml` |
| | Environment | `pypi` |
| GitHub environment `pypi` | Deployment branches and tags | Branch `main`, tags `v*` |
| | Required reviewers | None |
| | Allow administrators to bypass | Off |
| GitHub Pages | Source | GitHub Actions |
| GitHub environment `github-pages` | Deployment branches and tags | Branch `main`, tags `v*` |
| GitHub ruleset `main` | Required status check | `ci` |

The policy was made when `SqlSource` was the only package.  Before the first release that holds `SqlSource.Tool`, confirm on nuget.org that the policy covers a package id that does not exist yet.  If it does not, the push of the tool fails after the push of the generator has succeeded.

The PyPI project does not exist before the first release that holds the templater.  Before that release, once:

1. On PyPI, under the account's publishing settings, add a pending publisher for the project `sqlfluff-templater-sqlsource`, with the owner `mbcrawfo`, the repository `SqlSource`, the workflow `publish.yml` and the environment `pypi`.  The first push creates the project and turns the pending publisher into its trusted publisher.
2. In the repository's settings, add the environment `pypi` with the settings of the table.

Without them, `publish-pypi` fails, a failure that leaves the NuGet push of `publish` standing, and the release waits.  Make both, then run the failed jobs again.
