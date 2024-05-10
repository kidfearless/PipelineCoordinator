namespace PipelineCoordinator.Commands;

using System.Diagnostics;

using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

using PipelineCoordinator.Services;

[Command("push")]
internal class PushCommand(GitService _git) : ICommand
{

  public async ValueTask ExecuteAsync(IConsole console)
  {
    var folder = Environment.CurrentDirectory;
    var gitFolder = PathHelper.ReverseSearchPath(folder, ".git");
    var storyNumber = PathHelper.GetFeatureNumber(folder);

    console.WriteLine($"Pushing story {storyNumber} at path {gitFolder} to remote");


    await _git.TriggerBuildAsync(gitFolder, storyNumber);
  }
}

