using CliFx.Infrastructure;

internal static class IConsoleExtensions
{
  internal static void WriteLine<T>(this IConsole console, T value) => console!.Output.WriteLine(value);
  internal static IEnumerable<string> GetArguments(this IConsole console)
  {
    return Environment.GetCommandLineArgs().Skip(1);
  }
}
