namespace PipelineCoordinator.Commands;

using System.Runtime.CompilerServices;

using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

using PipelineCoordinator.Services;

[Command("start")]
internal class StartCommand(GitService _git, DotNetService _dotnet, IConsole _console) : ICommand
{
  [CommandParameter(0, Description = "The story ID.")]
  public required string StoryId { get; init; }


  public async ValueTask ExecuteAsync(IConsole console)
  {
    _console.WriteLine($"Started feature development for story {StoryId}.");

    await _git.InitializeReposAsync(StoryId);
    await _dotnet.InitializeRepos(StoryId);
    await _git.MarkFeaturesAsync(StoryId);
  }
}