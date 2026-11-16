---
name: upgrade-nuget-packages
description: Upgrade all NuGet packages in LogManager to their latest stable versions, validate the full build and test suite, and open a pull request.
---

# Upgrade NuGet packages

Use this workflow when asked to upgrade or update all NuGet packages in this repository. Do not publish a NuGet package.

## Prepare

1. Check `git status` and stop if there are unrelated uncommitted changes; do not overwrite or include them.
2. Identify the repository's default branch, fetch it, and create a new uniquely named branch from its up-to-date remote ref. Do not work on the default branch or reuse an existing upgrade branch.
3. Inspect the current solution, project files, NuGet configuration, CI workflow, and package-management files. Currently, `Directory.Packages.props` centrally pins package versions for `LogManager.sln`; both projects target .NET 10 and CI uses the .NET 10 SDK. Use the actual files as the source of truth if these change.

## Upgrade

1. Inventory all directly declared NuGet dependencies and central `PackageVersion` entries, including entries not currently referenced by a project. Check enabled NuGet sources and query the latest **stable** version of each package, including major releases. Use configured sources and their source-mapping rules; do not add or replace feeds or credentials to bypass failures.
2. Update every outdated direct dependency and central version to the latest stable version. Preserve central package management rather than adding project-level versions. The SDK's `dotnet package list --project LogManager.sln --outdated` helps identify referenced updates, but does not prove that unreferenced central entries are current; check those separately. Refresh any tracked lock files if present. Do not add direct references solely to force transitive dependencies to update.
3. Resolve upgrade-related build or API incompatibilities with the smallest necessary code and test changes, preserving the public API and behavior. Do not change the target framework, drop packages, suppress failures, or settle for an older version to claim success. If the latest stable versions cannot work together with the existing target framework, report the blocker and do not open a PR.
4. Restore and check again for outdated direct packages, and independently verify all central entries against the latest stable versions. Investigate feed errors, version conflicts, downgrades, and restore warnings rather than treating incomplete results as success. If all packages were already current and no changes are needed, report that and stop without creating a PR.

## Validate

The Elasticsearch and Loki integration tests use Testcontainers. Confirm `docker info` succeeds and that the required images can run; if Docker is unavailable, obtain an available daemon with appropriate permission or report the blocker. Do not exclude container tests.

Run the full CI-equivalent sequence from the repository root:

```sh
dotnet restore LogManager.sln
dotnet build LogManager.sln --configuration Release --no-restore
dotnet test LogManager.sln --configuration Release --no-build --verbosity normal --logger trx
```

Inspect the test output and TRX results: tests must actually run, with no failures or skipped tests, including the Elasticsearch and Loki fixtures. Address failures caused by the upgrade and rerun the full sequence. If any step cannot be completed, fails, or skips tests, report the evidence and stop without opening a PR.

## Publish the pull request

1. Review the diff for only dependency updates and necessary compatibility changes. Check for secrets, unintended changes, and new warnings. Update directly affected documentation if behavior or public APIs must change.
2. Check for a repository PR template and follow it. Commit the changes on the new branch, push it, and open a PR targeting the default branch only after the upgrade and validation gates pass. Respect any user confirmation or tool-specific requirements for creating the PR.
3. In the PR description summarize package version changes, necessary compatibility changes, and the actual build and test results. Include the repository checklist when applicable; do not claim checks passed that did not run. If pushing or PR creation fails, report the failure and leave the branch available for follow-up.
