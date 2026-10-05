# Publishing

How SqlSource is versioned and how it is released to nuget.org.  Building and testing locally are covered in [CONTRIBUTING.md](../CONTRIBUTING.md).

## Versioning

`VersionPrefix` in `Directory.Build.props` is the `major.minor.patch` of the next release, and the only version number edited by hand.  The workflows add the rest.  With `VersionPrefix` 0.1.0, pull request 42 and run 123:

| Build | Package version | Assembly and file version |
|----|----|----|
| Pull request | `0.1.0-pr-42.123` | `0.1.0.123` |
| `main` | `0.1.0-beta.123` | `0.1.0.123` |
| Tag `v0.1.0` | `0.1.0` | `0.1.0.<run>` |
| Local | `0.1.0-dev` | `0.1.0.0` |

The informational version is the package version followed by `+<commit sha>`.  Run numbers are counted per workflow, so a beta published by `publish.yml` does not share a number with the `ci.yml` artifact for the same commit.

## Releasing

Packages go to nuget.org through [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).  The repository holds no API key.

### Release

1. Set `VersionPrefix` in `Directory.Build.props` to the version being released, and merge that to `main`.
2. Tag the merged commit and push the tag:

   ```bash
   git tag v0.2.0
   ```

   ```bash
   git push origin v0.2.0
   ```

3. `publish.yml` builds the tag, fails if the tag is not `v<VersionPrefix>`, pushes `SqlSource.0.2.0.nupkg` to nuget.org and creates the GitHub release.
4. Raise `VersionPrefix` in the next pull request.  Until then, betas built from `main` sort below the release just published.

### Beta

Run `publish.yml` by hand on `main`, from the Actions tab or with:

```bash
gh workflow run publish.yml --ref main
```

It publishes `<VersionPrefix>-beta.<run>`.  A run started from any other branch builds and publishes nothing.

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
