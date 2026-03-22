#!/usr/bin/env dotnet
// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

// Bumps the major or minor version by creating a MinVer pre-release tag.
// This tag tells MinVer to start versioning in the new MAJOR.MINOR range.

#:package LoganBussell.EasyScripting@0.3.0

using EasyScripting;
using Spectre.Console;
using static EasyScripting.CommandLine;

var status = await Shell("git status --porcelain").Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(status))
    Prompt.Warning("Working tree is not clean. You may want to commit or stash your changes first.");

// Use minver-cli to determine the current version
var currentVersion = await Shell("dotnet minver --tag-prefix v --verbosity error")
    .Trim()
    .Quiet()
    .RunAsync();

Prompt.Info($"Detected current version: {currentVersion}");

// Parse major.minor from the current version
var parts = currentVersion.Split('.');
var currentMajor = int.Parse(parts[0]);
var currentMinor = int.Parse(parts[1]);

var newMinorVersion = $"{currentMajor}.{currentMinor + 1}.0";
var newMinorTag = $"v{newMinorVersion}-alpha.0";

var newMajorVersion = $"{currentMajor + 1}.0.0";
var newMajorTag = $"v{newMajorVersion}-alpha.0";

var prompt = "[blue][[choice]][/] Which version do you want to bump?";
var bumpType = await AnsiConsole.PromptAsync(
    new SelectionPrompt<string>()
    .Title(prompt)
    .AddChoices([VersionType.Minor, VersionType.Major])
    .UseConverter(
        choice => choice switch
        {
            VersionType.Minor => $"Minor version: next version will be [green]{newMinorVersion}[/]",
            VersionType.Major => $"Major version: next version will be [green]{newMajorVersion}[/]",
            _ => throw new InvalidOperationException("Invalid choice")
        }
    )
);

AnsiConsole.MarkupLine($"{prompt} [blue]{Markup.Escape(bumpType)}[/]");

var (newVersion, newTag) = bumpType switch
{
    VersionType.Minor => (newMinorVersion, newMinorTag),
    VersionType.Major => (newMajorVersion, newMajorTag),
    _ => throw new InvalidOperationException("Invalid choice")
};

var tagExists = await Shell($"git tag --list {newTag}").Trim().Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(tagExists))
{
    Prompt.Error($"Tag {newTag} already exists.");
    return 1;
}

Prompt.Info($"Will create tag {newTag} to bump to version {newVersion}.");
Prompt.Info("Future builds will use this tag to determine the version until a new release tag is created.");
Prompt.Info("Run ./scripts/Release.cs to create a release tag for the new version when you're ready to publish it.");

await Shell($"git tag {newTag}").Confirm().RunAsync();
Prompt.Success($"Created tag {newTag}");

await Shell($"git push origin {newTag}").Confirm().RunAsync();
Prompt.Success($"Pushed tag {newTag}");

return 0;

static class VersionType
{
    public const string Minor = "Minor";
    public const string Major = "Major";
}
