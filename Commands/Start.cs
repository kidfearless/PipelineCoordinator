
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineCoordinator.Commands;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

using PipelineCoordinator.Services;

[Command("start")]
internal class StartCommand(GitService _git, DotNetService _dotnet) : ICommand
{
  [CommandParameter(0, Description = "The story ID.")]
  public required string StoryId { get; init; }


  public async ValueTask ExecuteAsync(IConsole console)
  {
    console.Output.WriteLine($"Started feature development for story {StoryId}.");

    await _git.InitializeReposAsync(StoryId);
    await _dotnet.InitializeRepos(StoryId);
  }
}
