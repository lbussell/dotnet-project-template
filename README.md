# .NET Project Template

Starter repository for .NET projects with a root solution, shared build settings, GitHub Actions workflows, and a few file-based scripts for common repository setup tasks.

## Automation scripts

The `scripts/` folder contains .NET file-based apps. They use the `LoganBussell.EasyScripting` NuGet package, so they can run without a local project reference.

Run them from the repository root:

```bash
dotnet run scripts/SetupRepository.cs
dotnet run scripts/SetupPublishing.cs
dotnet run scripts/Release.cs
```

These scripts assume the GitHub CLI is installed and authenticated with `gh auth login`.

### `SetupRepository.cs`

Configures a GitHub repository with the defaults used by this template:

- enables release immutability
- disables wikis
- disables discussions
- disables merge commits for pull requests

### `SetupPublishing.cs`

Bootstraps the GitHub-side publishing setup used by `.github/workflows/publish-nuget.yml`:

- creates the `production` environment
- sets the `NUGET_USER` environment secret
- prints the remaining values needed to configure NuGet Trusted Publishing

After the script finishes, go to <https://www.nuget.org/account/trustedpublishing> and create a Trusted Publisher that matches the repository, workflow file, and environment printed by the script.

### `Release.cs`

Creates a release by:

- verifying the working tree is clean
- creating and pushing a `v*` tag
- creating a GitHub Release with generated notes

Pushing the version tag triggers `.github/workflows/publish-nuget.yml`.

## How to add new projects

Projects go under the `src/` folder.
Projects should be named descriptively, but should not be too verbose.

Example project layout:

```text
- MyAwesomeProject.slnx
- src/
    - Console/
        Console.csproj
    - Library/
        Library.csproj
    - Library.Tests/
        Library.Tests.csproj
```

Test projects go in the `src/` folder too, and should be named the same as the project they are testing but with the `.Tests` suffix.

Projects should have limited namespace nesting.
Only add nesting when differentiation is required.
If all of the projects in the repo will have the same prefix (e.g. `MyProject.Library`, `MyProject.Tests`, `MyProject.Web`), then the prefix does not provide value and should be removed.

### Example: Add a new class library

From the repo root, run the following:

```bash
# Always run with dry-run first to verify correctness
dotnet new classlib --name MyClasslib --output src/MyClasslib --dry-run
# Create the project
dotnet new classlib --name MyClasslib --output src/MyClasslib --dry-run
# Always add projects to the root of the solution - no solution folders
dotnet sln *.slnx add src/MyClasslib --in-root
```
