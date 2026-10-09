# Publishing

How SqlSource is versioned and how it is released to nuget.org.  Building and testing locally are covered in [CONTRIBUTING.md](../CONTRIBUTING.md).

A release is two packages: `SqlSource`, the generator, and `SqlSource.Tool`, the `sqlsource` command.  They are built by one run, carry one version and are published together.

## Versioning

`VersionPrefix` in `Directory.Build.props` is the `major.minor.patch` of the next release, and the only version number edited by hand.  The workflows add the rest.  With `VersionPrefix` 0.1.0, pull request 42 and run 123:

| Build | Package version | Assembly and file version |
|----|----|----|
| Pull request | `0.1.0-pr-42.123` | `0.1.0.123` |
| `main` | `0.1.0-beta.123` | `0.1.0.123` |
| Tag `v0.1.0` | `0.1.0` | `0.1.0.<run>` |
| Local | `0.1.0-dev` | `0.1.0.0` |

The table holds for both packages.  The informational version is the package version followed by `+<commit sha>`, and it is what `sqlsource --version` prints.  Run numbers are counted per workflow, so a beta published by `publish.yml` does not share a number with the `ci.yml` artifact for the same commit.

## Releasing

Packages go to nuget.org through [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).  The repository holds no API key.

### Release

1. Set `VersionPrefix` in `Directory.Build.props` to the version being released.
2. In the same pull request, record the diagnostics the release ships.  Move every entry of `src/SqlSource/AnalyzerReleases.Unshipped.md` to `src/SqlSource/AnalyzerReleases.Shipped.md`, under a new `## Release <version>` heading, keeping the `### New Rules`, `### Removed Rules` and `### Changed Rules` tables as they are.  Leave the unshipped file with its two header lines only.  Skip this when the unshipped file has no entries.  Nothing checks that it was done, and a release that skips it makes the next removal or change of a diagnostic impossible to record correctly.
3. Merge that to `main`.
4. Tag the merged commit and push the tag:

   ```bash
   git tag v0.2.0
   ```

   ```bash
   git push origin v0.2.0
   ```

5. `publish.yml` builds the tag, fails unless the build gave exactly `SqlSource.0.2.0.nupkg` and `SqlSource.Tool.0.2.0.nupkg`, which it does when the tag is `v<VersionPrefix>`, pushes both to nuget.org and creates the GitHub release with both attached.  If the push of the second fails, run the workflow again: `--skip-duplicate` passes over the first.
6. Raise `VersionPrefix` in the next pull request.  Until then, betas built from `main` sort below the release just published.

### Beta

Run `publish.yml` by hand on `main`, from the Actions tab or with:

```bash
gh workflow run publish.yml --ref main
```

It publishes `<VersionPrefix>-beta.<run>` of both packages.  A run started from any other branch builds and publishes nothing.

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
| GitHub ruleset `main` | Required status check | `ci` |

The policy was made when `SqlSource` was the only package.  Before the first release that holds `SqlSource.Tool`, confirm on nuget.org that the policy covers a package id that does not exist yet.  If it does not, the push of the tool fails after the push of the generator has succeeded.
