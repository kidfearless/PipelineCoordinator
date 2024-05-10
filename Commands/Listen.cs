namespace PipelineCoordinator.Commands;

using System.Diagnostics;

using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

using PipelineCoordinator.Services;

[Command("listen")]
internal class ListenCommand(GitService _git, DotNetService _dotnet) : ICommand
{
  public async ValueTask ExecuteAsync(IConsole console)
  {
    Debugger.Launch();
    var arguments = Environment.GetCommandLineArgs();

    console.WriteLine($"Arguments:\n {string.Join("\n ", arguments)}");
    var directory = Directory.GetCurrentDirectory();
    console.WriteLine($"cwd: {directory}");
  }
}

