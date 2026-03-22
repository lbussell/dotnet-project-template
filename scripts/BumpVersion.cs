#!/usr/bin/env dotnet
// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

// Bumps the major or minor version by creating a MinVer pre-release tag.
// This tag tells MinVer to start versioning in the new MAJOR.MINOR range.
// See: https://github.com/adamralph/minver#can-i-bump-the-major-or-minor-version
//
// Usage: dotnet run scripts/BumpVersion.cs

#:package LoganBussell.EasyScripting@0.3.0

using System.Text.RegularExpressions;
using EasyScripting;
using static EasyScripting.CommandLine;

var status = await Shell("git status --porcelain").Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(status))
{
    Prompt.Error("Working tree is not clean. Commit or stash your changes first.");
    return 1;
}

// Find the latest version tag to determine the current version
var latestTag = await Shell("git tag --list 'v*' --sort=-v:refname")
    .Trim()
    .Quiet()
    .RunAsync();

var currentMajor = 0;
var currentMinor = 0;

if (!string.IsNullOrWhiteSpace(latestTag))
{
    var firstTag = latestTag.Split('\n')[0];
    var match = Regex.Match(firstTag, @"^v(\d+)\.(\d+)\.(\d+)");
    if (match.Success)
    {
        currentMajor = int.Parse(match.Groups[1].Value);
        currentMinor = int.Parse(match.Groups[2].Value);
    }

    Prompt.Info($"Current version: {currentMajor}.{currentMinor} (from tag {firstTag})");
}
else
{
    Prompt.Info("Current version: 0.0 (no tags found)");
}

var bumpType = Prompt.Ask("Bump [green]major[/] or [green]minor[/]?");
int newMajor;
int newMinor;

if (string.Equals(bumpType, "major", StringComparison.OrdinalIgnoreCase))
{
    newMajor = currentMajor + 1;
    newMinor = 0;
}
else if (string.Equals(bumpType, "minor", StringComparison.OrdinalIgnoreCase))
{
    newMajor = currentMajor;
    newMinor = currentMinor + 1;
}
else
{
    Prompt.Error($"Expected 'major' or 'minor', got '{bumpType}'.");
    return 1;
}

var tag = $"v{newMajor}.{newMinor}.0-alpha.0";

Prompt.Info($"New version range: {newMajor}.{newMinor} — tag: {tag}");

var tagExists = await Shell($"git tag --list {tag}").Trim().Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(tagExists))
{
    Prompt.Error($"Tag {tag} already exists.");
    return 1;
}

await Shell($"git tag {tag}").Confirm().RunAsync();
Prompt.Success($"Created tag {tag}");

await Shell($"git push origin {tag}").Confirm().RunAsync();
Prompt.Success($"Pushed tag {tag}");

return 0;
