# SqlSource

C# SQL query source generator.

## Status

SqlSource is in early development.  The generator does not produce any code yet, and no version has been published to nuget.org.  It will be published as the `SqlSource` package.

## Supported environments

The generator is compiled against Roslyn 4.8.0, so it loads in the .NET 8 SDK and later and in Visual Studio 2022 17.8 and later.  .NET 8 is the explicit floor for support; older SDKs and IDEs are not supported.

This is what a project that *uses* the generator needs.  Working on the generator itself needs more; see [Contributing](#contributing).

## Contributing

- [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md): development setup, building, testing and the checks to run before a commit.
- [docs/publishing.md](https://github.com/mbcrawfo/SqlSource/blob/main/docs/publishing.md): versioning and releasing.
