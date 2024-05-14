namespace PipelineCoordinator.Commands;

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

using CliWrap.Buffered;
using CliWrap.Builders;

[DebuggerStepThrough]
internal class CLICommand : ICommandConfiguration, ICLICommand
{
  public string TargetFilePath { get; set; } = null!;
  public string? Arguments { get; set; }
  public string? WorkingDirPath { get; set; }
  public Credentials? Credentials { get; set; }
  public IReadOnlyDictionary<string, string?>? EnvironmentVariables { get; set; }
  public CommandResultValidation Validation { get; set; }
  public PipeSource? StandardInputPipe { get; set; }
  public PipeTarget? StandardOutputPipe { get; set; }
  public PipeTarget? StandardErrorPipe { get; set; }

  public ICLICommand WithTargetFile(string targetFilePath)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.TargetFilePath = targetFilePath;
    return copy;
  }


  public ICLICommand WithArguments(string arguments)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.Arguments = arguments;
    return copy;
  }


  public ICLICommand WithArguments(IEnumerable<string> arguments, bool escape)
  {
    return WithArguments(args => args.Add(arguments, escape));
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
    var copy = MemberwiseClone() as ICLICommand;
    copy.WorkingDirPath = workingDirPath;
    return copy;
  }


  public ICLICommand WithCredentials(Credentials credentials)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.Credentials = credentials;
    return copy;
  }


  public ICLICommand WithCredentials(Action<CredentialsBuilder> configure)
  {
    var builder = new CredentialsBuilder();
    configure(builder);
    return WithCredentials(builder.Build());
  }


  public ICLICommand WithEnvironmentVariables(IReadOnlyDictionary<string, string?> environmentVariables)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.EnvironmentVariables = environmentVariables;
    return copy;
  }


  public ICLICommand WithEnvironmentVariables(Action<EnvironmentVariablesBuilder> configure)
  {
    var builder = new EnvironmentVariablesBuilder();
    configure(builder);
    return WithEnvironmentVariables(builder.Build());
  }


  public ICLICommand WithValidation(CommandResultValidation validation)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.Validation = validation;
    return copy;
  }


  public ICLICommand WithStandardInputPipe(PipeSource source)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.StandardInputPipe = source;
    return copy;
  }


  public ICLICommand WithStandardOutputPipe(PipeTarget target)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.StandardOutputPipe = target;
    return copy;
  }


  public ICLICommand WithStandardErrorPipe(PipeTarget target)
  {
    var copy = MemberwiseClone() as ICLICommand;
    copy.StandardErrorPipe = target;
    return copy;
  }

  public Command ToCommand()
  {
    return new Command(
        TargetFilePath,
        Arguments!,
        WorkingDirPath!,
        Credentials!,
        EnvironmentVariables!,
        Validation,
        StandardInputPipe!,
        StandardOutputPipe!,
        StandardErrorPipe!
    );
  }

  public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      var result = await ToCommand().ExecuteAsync(cancellationToken);
      return result;
    }
    catch
    {
      return new CommandResult(-1, default, default);
    }
  }

  public async Task<BufferedCommandResult> ExecuteBufferedAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      var result = await ToCommand().ExecuteBufferedAsync(cancellationToken);
      return result;
    }
    catch (Exception ex)
    {
      return new BufferedCommandResult(-1, default, default, "", ex.ToString());
    }
  }
}
