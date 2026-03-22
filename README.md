# .NET Project Template

Fully configured starter repository for .NET projects hosted on GitHub.

## Scripts

The `scripts/` folder contains interactive scripts to help set up the repo.

- `SetupRepository.cs` - Automatically sets up the repo with my preferred settings (with confirmation).
- `SetupPublishing.cs` - Helps you set up NuGet trusted publishing from GitHub Workflows.
- `Release.cs` - Creates and tags a new GitHub release.

These scripts assume the GitHub CLI is installed and authenticated with `gh auth login`.
