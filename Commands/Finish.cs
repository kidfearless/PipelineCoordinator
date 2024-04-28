namespace PipelineCoordinator.Commands;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

using PipelineCoordinator.Services;

[Command("finish")]
internal class FinishCommand(GitService _git, DotNetService _dotnet) : ICommand
{
  [CommandParameter(0, Description = "The story ID.")]
  public required string StoryId { get; init; }


  public async ValueTask ExecuteAsync(IConsole console)
  {
    console.Output.WriteLine($"Finishing development for story {StoryId}.");

    await _git.InitializeReposAsync(StoryId);
    await _dotnet.InitializeRepos(StoryId);
  }
}