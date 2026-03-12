#!/usr/bin/env dotnet
// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

// Creates a release by tagging the current commit and pushing the tag.
// The publish workflow triggers automatically when a version tag is pushed.
//
// Usage: dotnet run scripts/Release.cs

#:package LoganBussell.EasyScripting@0.3.0

using System.Text.RegularExpressions;
using EasyScripting;
using Spectre.Console;
using static EasyScripting.CommandLine;

var status = await Shell("git status --porcelain").Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(status))
{
    Prompt.Error("Working tree is not clean. Commit or stash your changes first.");
    return 1;
}

Prompt.Info("Checking GitHub CLI authentication...");
await Shell("gh auth status").Quiet().RunAsync();

// List existing tags for context
var existingTags = await Shell("git tag --list --sort=-v:refname").Trim().Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(existingTags))
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Existing tags:[/]");
    foreach (var existingTag in existingTags.Split('\n'))
        AnsiConsole.MarkupLine($"- [dim]{Markup.Escape(existingTag)}[/]");
    AnsiConsole.WriteLine();
}

var version = Prompt.Ask("Enter the version to release (e.g. [green]0.6.0[/]):");
if (!IsValidSemVer(version))
{
    Prompt.Error($"'{Markup.Escape(version)}' is not a valid SemVer version.");
    return 1;
}

var tag = $"v{version}";

Prompt.Info($"Preparing release [green]{Markup.Escape(tag)}[/]");

var tagExists = await Shell($"git tag --list {tag}").Trim().Quiet().RunAsync();
if (!string.IsNullOrWhiteSpace(tagExists))
{
    Prompt.Error($"Tag [green]{Markup.Escape(tag)}[/] already exists.");
    return 1;
}

await Shell($"git tag {tag}").Confirm().RunAsync();
Prompt.Success($"Created tag [green]{Markup.Escape(tag)}[/]");

await Shell($"git push origin {tag}").Confirm().RunAsync();
Prompt.Success($"Pushed tag [green]{Markup.Escape(tag)}[/] — publish workflow will trigger automatically.");

var isPreRelease = version.Contains('-');
var prereleaseFlag = isPreRelease ? " --prerelease" : "";
await Shell($"gh release create {tag} --generate-notes{prereleaseFlag}").Confirm().RunAsync();
Prompt.Success($"Created GitHub Release [green]{Markup.Escape(tag)}[/]");

return 0;

static bool IsValidSemVer(string version) =>
    Regex.IsMatch(version, @"^\d+\.\d+\.\d+(-[0-9A-Za-z\-]+(\.[0-9A-Za-z\-]+)*)?(\+[0-9A-Za-z\-]+(\.[0-9A-Za-z\-]+)*)?$");
