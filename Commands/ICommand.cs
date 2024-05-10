namespace PipelineCoordinator.Commands;

using System.Runtime.InteropServices;

using CliWrap.Builders;
public interface ICLICommand
{
  string TargetFilePath { get; }
  string Arguments { get; }
  string WorkingDirPath { get; }
  Credentials Credentials { get; }
  IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }
  CommandResultValidation Validation { get; }
  PipeSource StandardInputPipe { get; }
  PipeTarget StandardOutputPipe { get; }
  PipeTarget StandardErrorPipe { get; }

  ICLICommand WithTargetFile(string targetFilePath);
  ICLICommand WithArguments(string arguments);
  ICLICommand WithArguments(IEnumerable<string> arguments, bool escape);
  ICLICommand WithArguments(IEnumerable<string> arguments);
  ICLICommand WithArguments(Action<ArgumentsBuilder> configure);
  ICLICommand WithWorkingDirectory(string workingDirPath);
  ICLICommand WithCredentials(Credentials credentials);
  ICLICommand WithCredentials(Action<CredentialsBuilder> configure);
  ICLICommand WithEnvironmentVariables(IReadOnlyDictionary<string, string?> environmentVariables);
  ICLICommand WithEnvironmentVariables(Action<EnvironmentVariablesBuilder> configure);
  ICLICommand WithValidation(CommandResultValidation validation);
  ICLICommand WithStandardInputPipe(PipeSource source);
  ICLICommand WithStandardOutputPipe(PipeTarget target);
  ICLICommand WithStandardErrorPipe(PipeTarget target);
}

public class MockCLICommand : ICLICommand
{
  private readonly List<string> _configurations = new List<string>();
  private readonly TimeSpan _delay;

  public MockCLICommand(string targetFilePath, TimeSpan delay)
  {
    TargetFilePath = targetFilePath;
    _delay = delay;
  }

  public string TargetFilePath { get; private set; }
  public string Arguments { get; private set; } = string.Empty;
  public string WorkingDirPath { get; private set; } = Directory.GetCurrentDirectory();
  public Credentials Credentials { get; private set; } = Credentials.Default;
  public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; private set; } = new Dictionary<string, string?>();
  public CommandResultValidation Validation { get; private set; } = CommandResultValidation.ZeroExitCode;
  public PipeSource StandardInputPipe { get; private set; } = PipeSource.Null;
  public PipeTarget StandardOutputPipe { get; private set; } = PipeTarget.Null;
  public PipeTarget StandardErrorPipe { get; private set; } = PipeTarget.Null;

  public ICLICommand WithTargetFile(string targetFilePath)
  {
    TargetFilePath = targetFilePath;
    _configurations.Add($"WithTargetFile: {targetFilePath}");
    return this;
  }

  public ICLICommand WithArguments(string arguments)
  {
    Arguments = arguments;
    _configurations.Add($"WithArguments: {arguments}");
    return this;
  }

  public ICLICommand WithArguments(IEnumerable<string> arguments, bool escape)
  {
    var builder = new ArgumentsBuilder();
    builder.Add(arguments);
    Arguments = builder.Build();
    _configurations.Add($"WithArguments: {string.Join(" ", arguments)}, Escape: {escape}");
    return this;
  }

  public ICLICommand WithArguments(IEnumerable<string> arguments)
  {
    return WithArguments(arguments, true);
  }

  public ICLICommand WithArguments(Action<ArgumentsBuilder> configure)
  {
    var builder = new ArgumentsBuilder();
    configure(builder);
    return WithArguments(builder.Build());
  }

  public ICLICommand WithWorkingDirectory(string workingDirPath)
  {
    WorkingDirPath = workingDirPath;
    _configurations.Add($"WithWorkingDirectory: {workingDirPath}");
    return this;
  }

  public ICLICommand WithCredentials(Credentials credentials)
  {
    Credentials = credentials;
    _configurations.Add($"WithCredentials: {credentials}");
    return this;
  }

  public ICLICommand WithCredentials(Action<CredentialsBuilder> configure)
  {
    var builder = new CredentialsBuilder();
    configure(builder);
    return WithCredentials(builder.Build());
  }

  public ICLICommand WithEnvironmentVariables(IReadOnlyDictionary<string, string?> environmentVariables)
  {
    EnvironmentVariables = environmentVariables;
    _configurations.Add($"WithEnvironmentVariables: {string.Join(", ", environmentVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
    return this;
  }

  public ICLICommand WithEnvironmentVariables(Action<EnvironmentVariablesBuilder> configure)
  {
    var builder = new EnvironmentVariablesBuilder();
    configure(builder);
    return WithEnvironmentVariables(builder.Build());
  }

  public ICLICommand WithValidation(CommandResultValidation validation)
  {
    Validation = validation;
    _configurations.Add($"WithValidation: {validation}");
    return this;
  }

  public ICLICommand WithStandardInputPipe(PipeSource source)
  {
    StandardInputPipe = source;
    _configurations.Add($"WithStandardInputPipe: {source}");
    return this;
  }

  public ICLICommand WithStandardOutputPipe(PipeTarget target)
  {
    StandardOutputPipe = target;
    _configurations.Add($"WithStandardOutputPipe: {target}");
    return this;
  }

  public ICLICommand WithStandardErrorPipe(PipeTarget target)
  {
    StandardErrorPipe = target;
    _configurations.Add($"WithStandardErrorPipe: {target}");
    return this;
  }

  public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken = default)
  {
    var start = DateTimeOffset.Now;
    await Task.Delay(_delay, cancellationToken);
    return new CommandResult(0, start, DateTimeOffset.Now);
  }

  public IReadOnlyList<string> GetConfigurations() => _configurations.AsReadOnly();
}