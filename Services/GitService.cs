using CliFx.Infrastructure;

using PipelineCoordinator.Models;

namespace PipelineCoordinator.Services;

public class GitService(GithubService _github, IConsole _console, DirectoryConfiguration _directory)
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
      //await AddHooksAsync(path, repo, storyId);
    }
  }

  private async Task AddHooksAsync(string path, RepositoryInfo repo, string storyId)
  {
    var gitHooksPath = Path.Combine(path, ".git", "hooks", "post-push");
    var script =
    $"""
    #!/bin/sh

    feature listen {storyId} {repo.ProjectName}
    """.Replace("    ", "");
    await File.WriteAllTextAsync(gitHooksPath, script);


  }

  private async Task AddOverridesToGitIgnoreAsync(string path)
  {
    var gitIgnorePath = Path.Combine(path, ".gitignore");
    if (File.Exists(gitIgnorePath))
    {
      await File.AppendAllTextAsync(gitIgnorePath, $"{Environment.NewLine}*override.*");
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
        .ExecAsync();
  }

  private async Task CreateBranchAsync(string repoDir, string storyName)
  {
    _console.WriteLine($"Creating feature branch feature/story-{storyName}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"checkout -b feature/story-{storyName} develop")
        .ExecAsync();
  }

  private async Task PublishBranchAsync(string repoDir, string storyName)
  {
    _console.WriteLine($"Publishing feature branch feature/story-{storyName}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"push --set-upstream origin feature/story-{storyName}")
        .ExecAsync();
  }

  public async Task TriggerBuildAsync(string repoDir, string storyName)
  {
    // git commit -m "trigger build" --allow-empty
    _console.WriteLine($"Triggering build for feature/story-{storyName}");
    await CommitAsync(repoDir, "trigger build");

    // git push
    await PublishBranchAsync(repoDir, storyName);
  }

  private async Task CommitAsync(string repoDir, string message)
  {
    _console.WriteLine($"Committing: {message}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"commit -m \"{message}\" --allow-empty")
        .ExecAsync();
  }

  private async Task<string> FindCommitAsync(string repoDir, string commitMessage)
  {
    _console.WriteLine($"Searching for commit: {commitMessage}");
    var result = await Git.WithWorkingDirectory(repoDir).WithArguments($"rev-list --all --grep=\"^{commitMessage}$\" -n 1").ExecuteBufferedAsync();
    var commitHash = result.StandardOutput?.Trim() ?? result.StandardError?.Trim() ?? "";
    return commitHash;
  }

  private async Task RevertCommitAsync(string repoDir, string commitMessage)
  {
    var commitHash = await FindCommitAsync(repoDir, commitMessage);
    _console.WriteLine($"Reverting commit: {commitHash}");
    var _ = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"revert {commitHash}")
        .ExecuteBufferedAsync();
  }

  private async Task MarkFeatureAsync(string repoDir, string storyId)
  {
    var message = $"feature/story-{storyId}_start";
    _console.WriteLine($"Adding all files to start commit: {message}");
    var _1 = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"add .")
        .ExecAsync();

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

  public async IAsyncEnumerable<RepositoryInfo> FindFeaturesAsync(string storyId)
  {
    var rootDirectory = Path.Combine(_directory.RootDirectory, storyId);
    foreach (var repo in _directory.Repos)
    {
      var path = Path.Combine(rootDirectory, repo.Path);
      var hasBranch = await HasRemoteBranchAsync(path, storyId);
      if (hasBranch)
      {
        yield return repo;
        _console.WriteLine($"Remote branch found for {repo.ProjectName}");
      }
      else
      {
        _console.WriteLine($"Remote branch not found for {repo.ProjectName}");
      }
    }
  }
}