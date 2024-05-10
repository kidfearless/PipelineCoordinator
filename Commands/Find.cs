namespace PipelineCoordinator.Commands;

using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

using PipelineCoordinator.Services;

[Command("find")]
internal class FindCommand(GitService _git) : ICommand
{
  [CommandParameter(0, Description = "The story ID.")]
  public required string StoryId { get; init; }


  public async ValueTask ExecuteAsync(IConsole console)
  {
    console.WriteLine($"Finding remote branches for {StoryId}.");
    var _ = await _git.FindFeaturesAsync(StoryId).ToListAsync();

  }
}