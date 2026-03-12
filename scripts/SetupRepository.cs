#!/usr/bin/env dotnet
// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

// Configures GitHub repository settings:
// 1. Enables release immutability
// 2. Disables wikis
// 3. Disables discussions
// 4. Disables merge commit for pull requests
//
// Usage: dotnet run scripts/SetupRepository.cs

#:package LoganBussell.EasyScripting@0.3.0

using System.Text.RegularExpressions;
using EasyScripting;
using Spectre.Console;
using static EasyScripting.CommandLine;

AnsiConsole.WriteLine();
(string? owner, string? repo) = await DetectGitHubRepoAsync();
await EnsureGhAuthenticatedAsync();
await RunEditRepoCommandAsync(owner, repo);
await EnableReleaseImmutabilityAsync(owner, repo);
Prompt.Success("Repository settings configured.");

async Task<(string Owner, string Repo)> DetectGitHubRepoAsync()
{
    string url = await Shell("git remote get-url origin")
        .Trim()
        .Quiet()
        .OnNonZeroExitCode(_ =>
        {
            Prompt.Error("Could not detect git remote. Are you in a git repository?");
            Environment.Exit(1);
        })
        .RunAsync();
    (string Owner, string Repo)? repo = ParseGitHubRepo(url);

    if (repo is null)
    {
        Prompt.Error($"Origin URL is not a GitHub repository: [dim]{url}[/]");
        Environment.Exit(1);
    }

    AnsiConsole.MarkupLine($"[bold]Detected repository:[/] [link]https://github.com/{repo.Value.Owner}/{repo.Value.Repo}[/]");

    if (!Prompt.Confirm("Is this correct?"))
    {
        AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
        Environment.Exit(1);
    }

    return (repo.Value.Owner, repo.Value.Repo);
}

async Task EnsureGhAuthenticatedAsync()
{
    AnsiConsole.MarkupLine("Checking GitHub CLI authentication...");
    await Shell("gh auth status")
        .Quiet()
        .OnNonZeroExitCode(_ =>
        {
            Prompt.Error(
                "The GitHub CLI is not authenticated. Run [blue]gh auth login[/] first."
            );
            Environment.Exit(1);
        })
        .RunAsync();
}

static async Task EnableReleaseImmutabilityAsync(string owner, string repo)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Enabling [green]release immutability[/][/]");
    await Shell($"gh api --method PATCH repos/{owner}/{repo} -f security_and_analysis[release_immutability][status]=enabled")
        .Confirm().RunAsync();
    Prompt.Success("Release immutability enabled.");
}

static async Task RunEditRepoCommandAsync(string owner, string repo)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Disabling [green]wikis[/], [green]discussions[/], and [green]merge commit[/][/]");
    await Shell($"gh repo edit {owner}/{repo} --enable-wiki=false --enable-discussions=false --enable-merge-commit=false")
        .Confirm().RunAsync();
    Prompt.Success("Wikis, discussions, and merge commit disabled.");
}

partial class Program
{
    // Match HTTPS: https://github.com/{owner}/{repo}.git
    // Match SSH:   git@github.com:{owner}/{repo}.git
    [GeneratedRegex(@"github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+)")]
    private static partial Regex GitHubUrlRegex { get; }

    private static (string Owner, string Repo)? ParseGitHubRepo(string url)
    {
        Match match = GitHubUrlRegex.Match(url);
        return !match.Success ? null : (match.Groups["owner"].Value, match.Groups["repo"].Value);
    }
}
