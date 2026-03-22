#!/usr/bin/env dotnet
// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

// Creates a release by tagging the current commit, pushing the tag,
// and triggering the publish workflow.
// The workflow builds, publishes to NuGet, and creates the GitHub Release.

#:package LoganBussell.EasyScripting@0.3.0

using System.Text.RegularExpressions;
using EasyScripting;
using Spectre.Console;
using static EasyScripting.CommandLine;

var status = await Shell("git status --porcelain").Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(status))
    Prompt.Warning("Working tree is not clean. You may want to commit or stash your changes first.");

// Use minver-cli to determine the current version (without height)
var currentVersion = await Shell("dotnet minver --tag-prefix v --verbosity error --ignore-height")
    .Trim()
    .Quiet()
    .RunAsync();

Prompt.Info($"Detected current version: {currentVersion}");

// Parse the version components
var match = Regex.Match(currentVersion, @"^(\d+)\.(\d+)\.(\d+)(-alpha\.(\d+))?");
if (!match.Success)
{
    Prompt.Error($"Could not parse version: {currentVersion}");
    return 1;
}

var major = int.Parse(match.Groups[1].Value);
var minor = int.Parse(match.Groups[2].Value);
var patch = int.Parse(match.Groups[3].Value);
var hasPreRelease = match.Groups[4].Success;
var alphaNumber = hasPreRelease ? int.Parse(match.Groups[5].Value) : 0;

var stableVersion = $"{major}.{minor}.{patch}";
var stableTag = $"v{stableVersion}";

var preReleaseVersion = $"{major}.{minor}.{patch}-alpha.{alphaNumber + 1}";
var preReleaseTag = $"v{preReleaseVersion}";

var prompt = "[blue][[choice]][/] What type of release?";
var releaseType = await AnsiConsole.PromptAsync(
    new SelectionPrompt<string>()
    .Title(prompt)
    .AddChoices([ReleaseType.PreRelease, ReleaseType.Stable])
    .UseConverter(
        choice => choice switch
        {
            ReleaseType.PreRelease => $"Pre-release: publish [green]{preReleaseVersion}[/]",
            ReleaseType.Stable => $"Stable: publish [green]{stableVersion}[/]",
            _ => throw new InvalidOperationException("Invalid choice")
        }
    )
);

AnsiConsole.MarkupLine($"{prompt} [blue]{Markup.Escape(releaseType)}[/]");

var (version, tag) = releaseType switch
{
    ReleaseType.PreRelease => (preReleaseVersion, preReleaseTag),
    ReleaseType.Stable => (stableVersion, stableTag),
    _ => throw new InvalidOperationException("Invalid choice")
};

var tagExists = await Shell($"git tag --list {tag}").Trim().Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(tagExists))
{
    Prompt.Error($"Tag {tag} already exists.");
    return 1;
}

Prompt.Info($"Will create tag {tag} to release version {version}.");
Prompt.Info("The publish workflow will build, publish to NuGet, and create the GitHub Release.");

await Shell($"git tag {tag}").Confirm().RunAsync();
Prompt.Success($"Created tag {tag}");

await Shell($"git push origin {tag}").Confirm().RunAsync();
Prompt.Success($"Pushed tag {tag}");

await Shell($"gh workflow run publish-nuget.yml --ref {tag}").Confirm().RunAsync();
Prompt.Success($"Triggered publish workflow for {tag}");

return 0;

static class ReleaseType
{
    public const string Stable = "Stable";
    public const string PreRelease = "Pre-release";
}
