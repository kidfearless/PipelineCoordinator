using System.Diagnostics;

using CliFx.Infrastructure;

using PipelineCoordinator.Console;

internal static class IConsoleExtensions
{
  private static Lazy<DateTime> _getStartTime = new(() =>
  {
    Process currentProcess = Process.GetCurrentProcess();
    DateTime startTime = currentProcess.StartTime;
    return startTime;
  });
  internal static void WriteLine<T>(this IConsole console, T value)
  {
    if(console is FileConsole)
    {
      var threadId = Environment.CurrentManagedThreadId;
      var start = _getStartTime.Value;
      var now = DateTime.Now;
      var elapsed = now - start;

      console!.Output.WriteLine($"[{threadId}][{elapsed}] {value}");
    }
    else
    {
      console!.Output.WriteLine(value);
    }
  }

  internal static IEnumerable<string> GetArguments(this IConsole console)
  {
    return Environment.GetCommandLineArgs().Skip(1);
  }
  internal static string GetWorkingDirectory(this IConsole console)
  {
    return Environment.CurrentDirectory;
  }
}
