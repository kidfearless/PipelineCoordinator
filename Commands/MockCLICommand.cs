using CliFx.Infrastructure;

using CliWrap.Builders;

using PipelineCoordinator.Commands;

internal class MockCLICommand : ICLICommand
{
  public MockCLICommand(IConsole console)
  {
    _console = console;
  }
  private readonly List<string> _configurations = new List<string>();
  private string? _output;
  private string? _error;
  private IConsole _console;

  public string TargetFilePath { get; set; } = null!;
  public string? Arguments { get; set; } = string.Empty;
  public string? WorkingDirPath { get; set; } = Directory.GetCurrentDirectory();
  public Credentials? Credentials { get; set; } = Credentials.Default;
  public IReadOnlyDictionary<string, string?>? EnvironmentVariables { get; set; } = new Dictionary<string, string?>();
  public CommandResultValidation Validation { get; set; } = CommandResultValidation.ZeroExitCode;
  public PipeSource? StandardInputPipe { get; set; } = PipeSource.Null;
  public PipeTarget? StandardOutputPipe { get; set; } = PipeTarget.Null;
  public PipeTarget? StandardErrorPipe { get; set; } = PipeTarget.Null;
  public TimeSpan Delay { get; set; }

  public ICLICommand WithTargetFile(string targetFilePath)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.TargetFilePath = targetFilePath;
    copy._configurations.Add($"WithTargetFile: {targetFilePath}");
    return copy;
  }

  public ICLICommand WithArguments(string arguments)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.Arguments = arguments;
    copy._configurations.Add($"WithArguments: {arguments}");
    return copy;
  }

  public ICLICommand WithArguments(IEnumerable<string> arguments, bool escape)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    var builder = new ArgumentsBuilder();
    builder.Add(arguments);
    copy.Arguments = builder.Build();
    copy._configurations.Add($"WithArguments: {string.Join(" ", arguments)}, Escape: {escape}");
    return copy;
  }

  public ICLICommand WithArguments(IEnumerable<string> arguments)
  {
    return WithArguments(arguments, true);
  }

  public ICLICommand WithArguments(Action<ArgumentsBuilder> configure)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    var builder = new ArgumentsBuilder();
    configure(builder);
    copy.Arguments = builder.Build();
    copy._configurations.Add($"WithArguments: {copy.Arguments}");
    return copy;
  }

  public ICLICommand WithWorkingDirectory(string workingDirPath)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.WorkingDirPath = workingDirPath;
    copy._configurations.Add($"WithWorkingDirectory: {workingDirPath}");
    return copy;
  }

  public ICLICommand WithCredentials(Credentials credentials)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.Credentials = credentials;
    copy._configurations.Add($"WithCredentials: {credentials}");
    return copy;
  }

  public ICLICommand WithCredentials(Action<CredentialsBuilder> configure)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    var builder = new CredentialsBuilder();
    configure(builder);
    copy.Credentials = builder.Build();
    copy._configurations.Add($"WithCredentials: {copy.Credentials}");
    return copy;
  }

  public ICLICommand WithEnvironmentVariables(IReadOnlyDictionary<string, string?> environmentVariables)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.EnvironmentVariables = environmentVariables;
    copy._configurations.Add($"WithEnvironmentVariables: {string.Join(", ", environmentVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
    return copy;
  }

  public ICLICommand WithEnvironmentVariables(Action<EnvironmentVariablesBuilder> configure)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    var builder = new EnvironmentVariablesBuilder();
    configure(builder);
    copy.EnvironmentVariables = builder.Build();
    copy._configurations.Add($"WithEnvironmentVariables: {string.Join(", ", copy.EnvironmentVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
    return copy;
  }

  public ICLICommand WithValidation(CommandResultValidation validation)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.Validation = validation;
    copy._configurations.Add($"WithValidation: {validation}");
    return copy;
  }

  public ICLICommand WithStandardInputPipe(PipeSource source)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.StandardInputPipe = source;
    copy._configurations.Add($"WithStandardInputPipe: {source}");
    return copy;
  }

  public ICLICommand WithStandardOutputPipe(PipeTarget target)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.StandardOutputPipe = target;
    copy._configurations.Add($"WithStandardOutputPipe: {target}");
    return copy;
  }

  public ICLICommand WithStandardErrorPipe(PipeTarget target)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.StandardErrorPipe = target;
    copy._configurations.Add($"WithStandardErrorPipe: {target}");
    return copy;
  }

  public ICLICommand WithMockDelay(TimeSpan delay)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy.Delay = delay;
    copy._configurations.Add($"WithMockDelay: {delay}");
    return copy;
  }

  public ICLICommand WithMockOutput(string output)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy._output = output;
    copy._configurations.Add($"WithMockOutput: {output}");
    return copy;
  }

  public ICLICommand WithMockError(string error)
  {
    var copy = (MockCLICommand)MemberwiseClone();
    copy._error = error;
    copy._configurations.Add($"WithMockError: {error}");
    return copy;
  }

  public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken = default)
  {
    var startTime = DateTimeOffset.Now;
    await Task.Delay(Delay, cancellationToken);

    var exitCode = 0;
    if (Validation == CommandResultValidation.ZeroExitCode && _configurations.Any(c => c.Contains("Error")))
    {
      exitCode = 1;
    }
    if (this.StandardOutputPipe != null)
    {
      using var memoryStream = new MemoryStream();
      using var writer = new StreamWriter(memoryStream);
      await writer.WriteAsync(_output);
      await writer.FlushAsync(cancellationToken);
      memoryStream.Seek(0, SeekOrigin.Begin);
      await this.StandardOutputPipe.CopyFromAsync(memoryStream, cancellationToken);
    }

    if (this.StandardErrorPipe != null)
    {
      using var memoryStream = new MemoryStream();
      using var writer = new StreamWriter(memoryStream);
      await writer.WriteAsync(_error);
      await writer.FlushAsync(cancellationToken);
      memoryStream.Seek(0, SeekOrigin.Begin);
      await this.StandardErrorPipe.CopyFromAsync(memoryStream, cancellationToken);
    }

    var endTime = DateTimeOffset.Now;
    var result = new CommandResult(exitCode, startTime, endTime);
    _console.WriteLine("");
    _console.WriteLine("");
    _console.WriteLine($"{TargetFilePath} {Arguments}:");
    foreach(var item in _configurations.ToArray())
    {
       _console.WriteLine($"\t{item}");
    }
    return result;
  }

  public async Task<BufferedCommandResult> ExecuteBufferedAsync(CancellationToken cancellationToken = default)
  {
    var result = await ExecuteAsync(cancellationToken);
    return new BufferedCommandResult(result.ExitCode, result.StartTime, result.ExitTime, _output, _error);
  }

  public IReadOnlyList<string> GetConfigurations() => _configurations.AsReadOnly();
}