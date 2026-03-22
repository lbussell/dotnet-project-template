#!/usr/bin/env dotnet
// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

// Interactive release management for MinVer-tagged releases and version bumps.
// The script explains the planned steps first, and each mutating command still
// asks for confirmation before it runs.

#:package LoganBussell.EasyScripting@0.3.0

using System.Text.RegularExpressions;
using System.Text.Json;
using EasyScripting;
using Spectre.Console;
using static EasyScripting.CommandLine;

const string tagPrefix = "v";
const string publishWorkflow = "publish-nuget.yml";
const string publishWorkflowPath = $".github/workflows/{publishWorkflow}";

await WarnIfWorkingTreeIsDirtyAsync();

var currentVersion = await DetectCurrentVersionAsync();
Prompt.Info($"Detected current version: {currentVersion.DisplayVersion}");
Prompt.Info(
    "Choose a release-management task. The script will explain the steps up-front, and every command will still ask for confirmation before it runs."
);

var actionPrompt = "[blue][[choice]][/] What do you want to do?";
var action = await AnsiConsole.PromptAsync(
    new SelectionPrompt<string>().Title(actionPrompt).AddChoices(
        [ReleaseAction.PreRelease, ReleaseAction.Stable, ReleaseAction.Bump]
    )
);
AnsiConsole.MarkupLine($"{actionPrompt} [blue]{Markup.Escape(action)}[/]");

ExecutionPlan plan;

switch (action)
{
    case ReleaseAction.PreRelease:
        if (!await EnsureWorkflowIsEnabledAsync())
            return 1;

        if (!TryBuildPreReleasePlan(currentVersion, out plan, out var preReleaseError))
        {
            Prompt.Error(preReleaseError);
            return 1;
        }
        break;

    case ReleaseAction.Stable:
        if (!await EnsureWorkflowIsEnabledAsync())
            return 1;

        plan = BuildStableReleasePlan(currentVersion);
        break;

    case ReleaseAction.Bump:
        plan = await BuildVersionBumpPlanAsync(currentVersion);
        break;

    default:
        throw new InvalidOperationException("Invalid choice");
}

if (!await EnsureTagCanBeCreatedAsync(plan.Tag))
    return 1;

ShowPlan(plan);
return await ExecutePlanAsync(plan) ? 0 : 1;

async Task WarnIfWorkingTreeIsDirtyAsync()
{
    var status = await Shell("git status --porcelain").Trim().Quiet().RunAsync();
    if (!string.IsNullOrWhiteSpace(status))
        Prompt.Warning("Working tree is not clean. You may want to commit or stash your changes first.");
}

async Task<VersionInfo> DetectCurrentVersionAsync()
{
    var version = await Shell($"dotnet minver --tag-prefix {tagPrefix} --verbosity error --ignore-height")
        .Trim()
        .Quiet()
        .OnNonZeroExitCode(_ =>
        {
            Prompt.Error("Could not determine the current version with MinVer.");
            Environment.Exit(1);
        })
        .RunAsync();

    if (!TryParseVersion(version, out var parsedVersion))
    {
        Prompt.Error($"Could not parse version: {version}");
        Environment.Exit(1);
    }

    return parsedVersion;
}

bool TryParseVersion(string version, out VersionInfo parsedVersion)
{
    var match = VersionRegex().Match(version);
    if (!match.Success)
    {
        parsedVersion = default!;
        return false;
    }

    parsedVersion = new VersionInfo(
        int.Parse(match.Groups["major"].Value),
        int.Parse(match.Groups["minor"].Value),
        int.Parse(match.Groups["patch"].Value),
        match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null
    );

    return true;
}

bool TryBuildPreReleasePlan(
    VersionInfo currentVersion,
    out ExecutionPlan plan,
    out string error
)
{
    if (!TryGetNextPreReleaseVersion(currentVersion, out var version, out error))
    {
        plan = default!;
        return false;
    }

    var tag = $"{tagPrefix}{version}";
    plan = new ExecutionPlan(
        Title: ReleaseAction.PreRelease,
        Version: version,
        Tag: tag,
        Summary: "This will create and push the prerelease tag, then trigger the publish workflow. The workflow will build, publish to NuGet, and create the GitHub Release.",
        Commands: [
            new PlannedCommand($"Create tag {tag}", $"git tag {tag}", $"Created tag {tag}"),
            new PlannedCommand($"Push tag {tag}", $"git push origin {tag}", $"Pushed tag {tag}"),
            new PlannedCommand(
                $"Trigger {publishWorkflow} for {tag}",
                $"gh workflow run {publishWorkflow} --ref {tag}",
                $"Triggered publish workflow for {tag}"
            )
        ]
    );
    return true;
}

ExecutionPlan BuildStableReleasePlan(VersionInfo currentVersion)
{
    var version = currentVersion.StableVersion;
    var tag = $"{tagPrefix}{version}";

    return new ExecutionPlan(
        Title: ReleaseAction.Stable,
        Version: version,
        Tag: tag,
        Summary: "This will create and push the release tag, then trigger the publish"
            + " workflow. The workflow will build, publish to NuGet, and create"
            + " the GitHub Release.",
        Commands: [
            new PlannedCommand($"Create tag {tag}", $"git tag {tag}", $"Created tag {tag}"),
            new PlannedCommand($"Push tag {tag}", $"git push origin {tag}", $"Pushed tag {tag}"),
            new PlannedCommand(
                $"Trigger {publishWorkflow} for {tag}",
                $"gh workflow run {publishWorkflow} --ref {tag}",
                $"Triggered publish workflow for {tag}"
            )
        ]
    );
}

async Task<ExecutionPlan> BuildVersionBumpPlanAsync(VersionInfo currentVersion)
{
    var newMinorVersion = $"{currentVersion.Major}.{currentVersion.Minor + 1}.0";
    var newMinorTag = $"{tagPrefix}{newMinorVersion}-alpha.0";

    var newMajorVersion = $"{currentVersion.Major + 1}.0.0";
    var newMajorTag = $"{tagPrefix}{newMajorVersion}-alpha.0";

    var bumpPrompt = "[blue][[choice]][/] Which version do you want to bump?";
    var bumpType = await AnsiConsole.PromptAsync(
        new SelectionPrompt<string>()
            .Title(bumpPrompt)
            .AddChoices([BumpType.Minor, BumpType.Major])
            .UseConverter(
                choice => choice switch
                {
                    BumpType.Minor => $"Minor version: next version line will be [green]{newMinorVersion}[/]",
                    BumpType.Major => $"Major version: next version line will be [green]{newMajorVersion}[/]",
                    _ => throw new InvalidOperationException("Invalid choice")
                }
            )
    );

    AnsiConsole.MarkupLine($"{bumpPrompt} [blue]{Markup.Escape(bumpType)}[/]");

    var (version, tag) = bumpType switch
    {
        BumpType.Minor => (newMinorVersion, newMinorTag),
        BumpType.Major => (newMajorVersion, newMajorTag),
        _ => throw new InvalidOperationException("Invalid choice")
    };

    return new ExecutionPlan(
        Title: ReleaseAction.Bump,
        Version: version,
        Tag: tag,
        Summary: "This will create and push a MinVer alpha.0 seed tag for the next"
            + " version line. Run it on a new commit after a release so the"
            + " same commit does not end up with multiple version tags.",
        Commands: [
            new PlannedCommand($"Create tag {tag}", $"git tag {tag}", $"Created tag {tag}"),
            new PlannedCommand($"Push tag {tag}", $"git push origin {tag}", $"Pushed tag {tag}")
        ]
    );
}

bool TryGetNextPreReleaseVersion(
    VersionInfo currentVersion,
    out string nextVersion,
    out string error
)
{
    if (currentVersion.PreRelease is null)
    {
        nextVersion = $"{currentVersion.StableVersion}-alpha.1";
        error = string.Empty;
        return true;
    }

    var match = AlphaPreReleaseRegex().Match(currentVersion.PreRelease);
    if (!match.Success)
    {
        nextVersion = string.Empty;
        error =
            $"Current version {currentVersion.DisplayVersion} uses prerelease suffix '{currentVersion.PreRelease}', but this script only knows how to increment alpha prereleases.";
        return false;
    }

    var nextAlphaNumber = int.Parse(match.Groups["number"].Value) + 1;
    nextVersion = $"{currentVersion.StableVersion}-alpha.{nextAlphaNumber}";
    error = string.Empty;
    return true;
}

async Task<bool> EnsureWorkflowIsEnabledAsync()
{
    var workflowsJson = await Shell("gh workflow list --all --json path,state")
        .Trim()
        .Quiet()
        .OnNonZeroExitCode(_ =>
        {
            Prompt.Error($"Could not inspect workflow state for {publishWorkflowPath}.");
            Environment.Exit(1);
        })
        .RunAsync();

    string? state;
    try
    {
        using var workflows = JsonDocument.Parse(workflowsJson);
        state = null;

        foreach (var workflow in workflows.RootElement.EnumerateArray())
        {
            if (
                workflow.TryGetProperty("path", out var path)
                && path.GetString() == publishWorkflowPath
                && workflow.TryGetProperty("state", out var workflowState)
            )
            {
                state = workflowState.GetString();
                break;
            }
        }
    }
    catch (JsonException)
    {
        Prompt.Error($"Could not parse workflow state for {publishWorkflowPath}.");
        return false;
    }

    if (string.IsNullOrWhiteSpace(state))
    {
        Prompt.Error($"Could not find workflow {publishWorkflowPath}.");
        return false;
    }

    if (string.Equals(state, "active", StringComparison.OrdinalIgnoreCase))
        return true;

    Prompt.Error(
        $"Workflow {publishWorkflowPath} is {state}. Enable it with [blue]gh workflow enable {publishWorkflow}[/] before creating a release."
    );
    return false;
}

async Task<bool> EnsureTagCanBeCreatedAsync(string tag)
{
    var tagExists = await Shell($"git tag --list \"{tag}\"").Trim().Quiet().RunAsync();
    if (!string.IsNullOrWhiteSpace(tagExists))
    {
        Prompt.Error($"Tag {tag} already exists.");
        return false;
    }

    var tagsOnHead = await Shell($"git tag --points-at HEAD --list \"{tagPrefix}*\"")
        .Trim()
        .Quiet()
        .RunAsync();

    var headVersionTags = SplitLines(tagsOnHead);
    if (headVersionTags.Length > 0)
    {
        Prompt.Error(
            $"HEAD already has version tag(s): {string.Join(", ", headVersionTags)}. Create a new commit before tagging {tag} so the same commit does not end up with multiple version tags."
        );
        return false;
    }

    return true;
}

void ShowPlan(ExecutionPlan plan)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[bold]Planned steps for:[/] {Markup.Escape(plan.Title)}");
    AnsiConsole.MarkupLine($"[bold]Target version:[/] [green]{Markup.Escape(plan.Version)}[/]");
    AnsiConsole.MarkupLine($"[bold]Tag to create:[/] [green]{Markup.Escape(plan.Tag)}[/]");
    Prompt.Info(plan.Summary);
    Prompt.Info("All commands below will still ask for confirmation before they run.");
    AnsiConsole.WriteLine();

    for (var index = 0; index < plan.Commands.Count; index++)
    {
        var command = plan.Commands[index];
        AnsiConsole.MarkupLine($"[blue]{index + 1}.[/] {Markup.Escape(command.Description)}");
        AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(command.Command)}[/]");
    }
}

async Task<bool> ExecutePlanAsync(ExecutionPlan plan)
{
    AnsiConsole.WriteLine();

    foreach (var command in plan.Commands)
    {
        try
        {
            await Shell(command.Command).Confirm().RunAsync();
        }
        catch (OperationCanceledException)
        {
            Prompt.Warning("Aborted before running any remaining commands.");
            return false;
        }

        Prompt.Success(command.SuccessMessage);
    }

    return true;
}

string[] SplitLines(string value) =>
    value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

record VersionInfo(int Major, int Minor, int Patch, string? PreRelease)
{
    public string StableVersion => $"{Major}.{Minor}.{Patch}";

    public string DisplayVersion =>
        PreRelease is null ? StableVersion : $"{StableVersion}-{PreRelease}";
}

record PlannedCommand(string Description, string Command, string SuccessMessage);

record ExecutionPlan(
    string Title,
    string Version,
    string Tag,
    string Summary,
    IReadOnlyList<PlannedCommand> Commands
);

static class ReleaseAction
{
    public const string PreRelease = "Release a pre-release version";
    public const string Stable = "Release a stable version";
    public const string Bump = "Bump the version number";
}

static class BumpType
{
    public const string Minor = "Minor";
    public const string Major = "Major";
}

partial class Program
{
    [GeneratedRegex(@"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<prerelease>[0-9A-Za-z.-]+))?$")]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"^alpha\.(?<number>\d+)$")]
    private static partial Regex AlphaPreReleaseRegex();
}
