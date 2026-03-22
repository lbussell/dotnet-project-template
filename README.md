# .NET Project Template

Fully configured starter repository for .NET projects hosted on GitHub.

## Scripts

The `scripts/` folder contains interactive scripts to help set up the repo.

- `SetupRepository.cs` - Automatically sets up the repo with my preferred settings (with confirmation).
- `SetupPublishing.cs` - Helps you set up NuGet trusted publishing from GitHub Workflows.
- `Release.cs` - Interactive release management tool. Run this to when you want to release software
  (stable or pre-release) or bump the version number.

These scripts assume the GitHub CLI is installed and authenticated with `gh auth login`.
