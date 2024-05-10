using PipelineCoordinator.Commands;

internal static class CliWrapExtensions
{
  /// <summary>
  /// Executes the command asynchronously with buffering.
  /// Data written to the standard output and standard error streams is decoded as text
  /// and returned as part of the result object.
  /// Uses <see cref="Console.OutputEncoding" /> for decoding.
  /// </summary>
  /// <remarks>
  /// This method can be awaited.
  /// </remarks>
  public static async Task<BufferedCommandResult> ExecuteBufferedAsync(this ICLICommand command)
  {
    try
    {
      return await command.ExecuteBufferedAsync(Console.OutputEncoding, default);
    }
    catch (Exception e)
    {
      return new BufferedCommandResult(-1, default, default, "", e.ToString());
    }
  }
  /// <summary>
  /// Executes the command asynchronously.
  /// </summary>
  /// <remarks>
  /// This method can be awaited.
  /// </remarks>
  public static async Task<CommandResult> ExecAsync(this Command command)
  {
    try
    {
      return await command.ExecuteAsync(default);
    }
    catch (Exception ex)
    {
      return new CommandResult(-1, default, default);
    }
  }
}
