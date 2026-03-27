# Overview

The Aspire Community Toolkit follows the [Semantic Versioning 2.0.0](https://semver.org/) specification and aims to keep in sync with the Aspire version numbers to a minor version level. This means that the major version of the toolkit will be the same as the major version of the Aspire framework.

Individual Community Toolkit NuGet packages may version at a patch level different to Aspire, but the major and minor versions will be the same. For example, a package with the version 8.2.x will be compatible with Aspire 8.2, and the patch version may be different.

This means that the introduction of a new integration will not result in a major (or minor) version bump of the toolkit, but it may result in a patch version bump to other packages.

## Release Process

The Aspire Community Toolkit aims to be released in sync with Aspire, but there may be a short lag, depending on bandwidth of the maintainers and complexity of any changes.

Releases are automated using GitHub Actions, each release will be tagged in the repository with the version number, and a release will be created on GitHub with the release notes (where possible).

## Preview and Pull Request Builds

Preview builds are created for each pull request and are available as assets of the workflow run, as well as via Azure Artifacts (where applicable). These builds are not intended for production use and may contain bugs or incomplete features, so will be marked with pre-release tags.
