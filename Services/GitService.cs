using CliFx.Infrastructure;

using Microsoft.TeamFoundation.Test.WebApi;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PipelineCoordinator.Models;

namespace PipelineCoordinator.Services;

internal class GitService(GithubService _github, IConsole _console, DirectoryConfiguration _directory)
{
  private Command Git => Cli.Wrap("git")
          .WithValidation(CommandResultValidation.None)
          .WithStandardOutputPipe(PipeTarget.ToStream(_console.Output.BaseStream))
          .WithStandardErrorPipe(PipeTarget.ToStream(_console.Error.BaseStream));

  public async Task InitializeReposAsync(string storyId)
  {
    var rootDirectory = Path.Combine(_directory.RootDirectory, $"feature/story-{storyId}");
    foreach(var repo in _directory.Repositories)
    {
      var path = Path.Combine(rootDirectory, repo.Path);

      await CloneRepoAsync(path, repo.GithubUrl);
      await CreateBranchAsync(path, storyId);
    }
  }

  public async Task FinishReposAsync(string repoDir, string storyId)
  {
    var gitFolders = Directory.GetDirectories(repoDir, ".git", SearchOption.AllDirectories);

  }

  private async Task CloneRepoAsync(string repoDir, string repoName)
  {
    _console.Output.WriteLine($"Cloning {repoName}...");
    await _github.CloneRepo(repoDir, repoName);
  }

  private async Task TrustRepoAsync(string repoDir)
  {
    _console.Output.WriteLine($"Trusting {repoDir}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"config --global --add safe.directory {repoDir}")
        .ExecuteAsync();
  }

  private async Task CreateBranchAsync(string repoDir, string storyName)
  {
    await TrustRepoAsync(repoDir);

    _console.Output.WriteLine($"Creating feature branch feature/story-{storyName}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"checkout -b feature/story-{storyName} develop")
        .ExecuteAsync();
  }

  private async Task PublishBranchAsync(string repoDir, string storyName)
  {
    _console.Output.WriteLine($"Publishing feature branch feature/story-{storyName}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"push --set-upstream origin feature/story-{storyName}")
        .ExecuteAsync();
  }

  private async Task<string> FindCommitAsync(string repoDir, string commitMessage)
  {
    _console.Output.WriteLine($"Searching for commit: {commitMessage}");
    var result = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"log --grep='^{commitMessage}$' -n 1 --pretty=format:%H")
        .ExecuteBufferedAsync();
    var commitHash = result.StandardOutput ?? result.StandardOutput;
    return commitHash ?? "";
  }

  private async Task RevertCommitAsync(string repoDir, string commitMessage)
  {
    var commitHash = await FindCommitAsync(repoDir, commitMessage);
    _console.Output.WriteLine($"Reverting commit: {commitHash}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"revert {commitHash}")
        .ExecuteAsync();
  }

  private async Task MarkFeatureAsync(string repoDir, string storyId)
  {
    var message = $"feature/story-{storyId} start";
    _console.Output.WriteLine($"Adding all files to start commit: {message}");
    var _1 = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"add .")
        .ExecuteAsync();

    _console.Output.WriteLine($"Committing start commit: {message}");
    var _2 = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"commit -m '{message}'")
        .ExecuteAsync();
  }

  public async Task FinishFeatureAsync(string repoDir, string storyId)
  {
    var commitMessage = $"feature/story-{storyId} start";
    await RevertCommitAsync(repoDir, commitMessage);
  }

  public async Task<bool> HasRemoteBranchAsync(string repoDir, string storyId)
  {
    var result = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"ls-remote --heads origin feature/story-{storyId}")
        .ExecuteBufferedAsync();

    _console.Output.WriteLine($"Checking for remote branch for: {repoDir}");
    _console.Output.WriteLine(result.StandardOutput ?? result.StandardError);

    var hasBranch = result.StandardOutput?.Length > 2 || result.StandardError?.Length > 2;
    return hasBranch;
  }

  public async Task PushToRemoteAsync(string repoDir, string storyId)
  {
    await PublishBranchAsync(repoDir, storyId);
  }
}