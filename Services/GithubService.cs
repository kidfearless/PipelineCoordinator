global using CliWrap;
global using CliWrap.Buffered;

using CliFx.Infrastructure;

using PipelineCoordinator.Commands;



namespace PipelineCoordinator.Services;
internal class GithubService
{
  public ICLICommand GH { get; }
    private readonly IConsole _console;

  public GithubService(IConsole _console, ICLICommand command)
  {
    this._console = _console;
    GH = command.WithTargetFile("gh")
      .WithValidation(CommandResultValidation.None);
  }

  public async Task<BufferedCommandResult> CloneRepo(string path, string repoName)
  {
    _console.WriteLine("Deleting old repos...");
    if (!Directory.Exists(path))
    {
      Directory.CreateDirectory(path);
    }

    _console.WriteLine($"Cloning {repoName}...");
    var result = await (GH as Command)
        .WithArguments(@$"repo clone {repoName} {path}")
        .WithWorkingDirectory(path)
        .ExecuteBufferedAsync();

    return result;
  }

  /// <summary>
  /// Helper method to delete a directory and all of its contents. .git folders contain readonly files so we need to set them to normal before deleting
  /// </summary>
  /// <param name="targetDir"></param>
  public static void DeleteDirectory(string targetDir)
  {
    File.SetAttributes(targetDir, FileAttributes.Normal);

    var files = Directory.GetFiles(targetDir);
    var dirs = Directory.GetDirectories(targetDir);

    foreach (string file in files)
    {
      File.SetAttributes(file, FileAttributes.Normal);
      File.Delete(file);
    }

    foreach (string dir in dirs)
    {
      DeleteDirectory(dir);
    }

    Directory.Delete(targetDir, false);
  }
}
