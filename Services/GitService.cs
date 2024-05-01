using CliFx.Infrastructure;

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
    var rootDirectory = Path.Combine(_directory.RootDirectory, storyId);
    foreach (var repo in _directory.Repos)
    {
      var path = Path.Combine(rootDirectory, repo.Path);

      await CloneRepoAsync(path, repo.GithubUrl);
      await TrustRepoAsync(path);
      await CreateBranchAsync(path, storyId);
      await AddOverridesToGitIgnoreAsync(path);
    }
  }

  private async Task AddOverridesToGitIgnoreAsync(string path)
  {
    var gitIgnorePath = Path.Combine(path, ".gitignore");
    if(File.Exists(gitIgnorePath))
    {
      await File.AppendAllTextAsync(gitIgnorePath, "*override.*");
    }
  }

  public async Task MarkFeaturesAsync(string storyId)
  {
    var rootDirectory = Path.Combine(_directory.RootDirectory, storyId);
    foreach (var repo in _directory.Repos)
    {
      var path = Path.Combine(rootDirectory, repo.Path);
      await MarkFeatureAsync(path, storyId);
    }
  }

  public async Task FinishReposAsync(string storyId)
  {
    var rootDirectory = Path.Combine(_directory.RootDirectory, storyId);
    foreach (var repo in _directory.Repos)
    {
      var path = Path.Combine(rootDirectory, repo.Path);
      await FinishFeatureAsync(path, storyId);
    }

  }

  private async Task CloneRepoAsync(string repoDir, string repoName)
  {
    _console.WriteLine($"Cloning {repoName}...");
    await _github.CloneRepo(repoDir, repoName);
  }

  private async Task TrustRepoAsync(string repoDir)
  {
    _console.WriteLine($"Trusting {repoDir}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"config --global --add safe.directory {repoDir}")
        .ExecuteAsync();
  }

  private async Task CreateBranchAsync(string repoDir, string storyName)
  {
    _console.WriteLine($"Creating feature branch feature/story-{storyName}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"checkout -b feature/story-{storyName} develop")
        .ExecuteAsync();
  }

  private async Task PublishBranchAsync(string repoDir, string storyName)
  {
    _console.WriteLine($"Publishing feature branch feature/story-{storyName}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"push --set-upstream origin feature/story-{storyName}")
        .ExecuteAsync();
  }

  private async Task<string> FindCommitAsync(string repoDir, string commitMessage)
  {
    _console.WriteLine($"Searching for commit: {commitMessage}");
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
    _console.WriteLine($"Reverting commit: {commitHash}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"revert {commitHash}")
        .ExecuteAsync();
  }

  private async Task MarkFeatureAsync(string repoDir, string storyId)
  {
    var message = $"feature/story-{storyId}_start";
    _console.WriteLine($"Adding all files to start commit: {message}");
    var _1 = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"add .")
        .ExecuteAsync();

    _console.WriteLine($"Committing start commit: {message}");
    var _2 = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"commit --message \"{message}\"")
        .ExecuteBufferedAsync();
  }

  public async Task FinishFeatureAsync(string repoDir, string storyId)
  {
    var startMessage = $"feature/story-{storyId}_start";
    await RevertCommitAsync(repoDir, startMessage);
  }

  public async Task<bool> HasRemoteBranchAsync(string repoDir, string storyId)
  {
    var result = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"ls-remote --heads origin feature/story-{storyId}")
        .ExecuteBufferedAsync();

    _console.WriteLine($"Checking for remote branch for: {repoDir}");
    _console.WriteLine(result.StandardOutput ?? result.StandardError);

    var hasBranch = result.StandardOutput?.Length > 2 || result.StandardError?.Length > 2;
    return hasBranch;
  }

  public async Task PushToRemoteAsync(string repoDir, string storyId)
  {
    await PublishBranchAsync(repoDir, storyId);
  }
}