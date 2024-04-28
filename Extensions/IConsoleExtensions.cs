using CliFx.Infrastructure;

namespace PipelineCoordinator;

internal static class IConsoleExtensions
{
  internal static void WriteLine<T>(this IConsole console, T value) => console!.Output.WriteLine(value);
}