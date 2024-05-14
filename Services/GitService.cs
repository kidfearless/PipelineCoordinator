using CliFx.Infrastructure;

using PipelineCoordinator.Commands;
using PipelineCoordinator.Models;

namespace PipelineCoordinator.Services;

internal class GitService
{
  private readonly GithubService _github;
  private readonly IConsole _console;
  private readonly DirectoryConfiguration _directory;
  private ICLICommand Git { get; }

  public GitService(GithubService github, IConsole console, DirectoryConfiguration directory, ICLICommand command)
  {
    this._github = github;
    this._console = console;
    this._directory = directory;
    Git = command.WithTargetFile("git")
         .WithValidation(CommandResultValidation.None)
      .WithStandardOutputPipe(PipeTarget.ToDelegate((a) => _console.WriteLine(a)))
      .WithStandardErrorPipe(PipeTarget.ToDelegate((a) => _console.WriteLine(a)));
  }


  public async Task InitializeReposAsync(string storyId)
  {
    var rootDirectory = Path.Combine(_directory.RootDirectory, storyId);
    await Parallel.ForEachAsync(_directory.Repos, async (repo, t) =>
    {
      var path = Path.Combine(rootDirectory, repo.Path);

      await CloneRepoAsync(path, repo.GithubUrl);
      await TrustRepoAsync(path);
      await CreateBranchAsync(path, storyId);
      await AddOverridesToGitIgnoreAsync(path);
      //await AddHooksAsync(path, repo, storyId);
    });
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
    await Parallel.ForEachAsync(_directory.Repos, async (repo, t) =>
    {
      var path = Path.Combine(rootDirectory, repo.Path);
      await MarkFeatureAsync(path, storyId);
    });
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
        .ExecuteAsync();
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
        .ExecuteAsync();

    _console.WriteLine($"Committing start commit: {message}");
    var _2 = await Git
        .WithWorkingDirectory(repoDir)
        .WithArguments($"commit --message \"{message}\"")
        .ExecuteAsync();
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
        .WithMockOutput("has remote")
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