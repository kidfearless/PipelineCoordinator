namespace PipelineCoordinator.Commands;

using CliWrap.Builders;

internal class CLICommand : Command, ICLICommand
{
  public CLICommand(string targetFilePath) : base(targetFilePath)
  {
  }

  public CLICommand() : base("")
  {

  }

  private CLICommand(
      string targetFilePath,
      string arguments,
      string workingDirPath,
      Credentials credentials,
      IReadOnlyDictionary<string, string?> environmentVariables,
      CommandResultValidation validation,
      PipeSource standardInputPipe,
      PipeTarget standardOutputPipe,
      PipeTarget standardErrorPipe
  ) : base(
      targetFilePath,
      arguments,
      workingDirPath,
      credentials,
      environmentVariables,
      validation,
      standardInputPipe,
      standardOutputPipe,
      standardErrorPipe
  )
  {
  }

  public new ICLICommand WithTargetFile(string targetFilePath) =>
      new CLICommand(
          targetFilePath,
          Arguments,
          WorkingDirPath,
          Credentials,
          EnvironmentVariables,
          Validation,
          StandardInputPipe,
          StandardOutputPipe,
          StandardErrorPipe
      );

  public new ICLICommand WithArguments(string arguments) =>
      new CLICommand(
          TargetFilePath,
          arguments,
          WorkingDirPath,
          Credentials,
          EnvironmentVariables,
          Validation,
          StandardInputPipe,
          StandardOutputPipe,
          StandardErrorPipe
      );

  public new ICLICommand WithArguments(IEnumerable<string> arguments, bool escape)
  {
    var builder = new ArgumentsBuilder();
    builder.Add(arguments);
    return new CLICommand(
          TargetFilePath,
          builder.Build(),
          WorkingDirPath,
          Credentials,
          EnvironmentVariables,
          Validation,
          StandardInputPipe,
          StandardOutputPipe,
          StandardErrorPipe
      );
  }

  public new ICLICommand WithArguments(IEnumerable<string> arguments) =>
      WithArguments(arguments, true);

  public new ICLICommand WithArguments(Action<ArgumentsBuilder> configure)
  {
    var builder = new ArgumentsBuilder();
    configure(builder);
    return WithArguments(builder.Build());
  }

  public new ICLICommand WithWorkingDirectory(string workingDirPath) =>
      new CLICommand(
          TargetFilePath,
          Arguments,
          workingDirPath,
          Credentials,
          EnvironmentVariables,
          Validation,
          StandardInputPipe,
          StandardOutputPipe,
          StandardErrorPipe
      );

  public new ICLICommand WithCredentials(Credentials credentials) =>
      new CLICommand(
          TargetFilePath,
          Arguments,
          WorkingDirPath,
          credentials,
          EnvironmentVariables,
          Validation,
          StandardInputPipe,
          StandardOutputPipe,
          StandardErrorPipe
      );

  public new ICLICommand WithCredentials(Action<CredentialsBuilder> configure)
  {
    var builder = new CredentialsBuilder();
    configure(builder);
    return WithCredentials(builder.Build());
  }

  public new ICLICommand WithEnvironmentVariables(IReadOnlyDictionary<string, string?> environmentVariables) =>
      new CLICommand(
          TargetFilePath,
          Arguments,
          WorkingDirPath,
          Credentials,
          environmentVariables,
          Validation,
          StandardInputPipe,
          StandardOutputPipe,
          StandardErrorPipe
      );

  public new ICLICommand WithEnvironmentVariables(Action<EnvironmentVariablesBuilder> configure)
  {
    var builder = new EnvironmentVariablesBuilder();
    configure(builder);
    return WithEnvironmentVariables(builder.Build());
  }

  public new ICLICommand WithValidation(CommandResultValidation validation) =>
      new CLICommand(
          TargetFilePath,
          Arguments,
          WorkingDirPath,
          Credentials,
          EnvironmentVariables,
          validation,
          StandardInputPipe,
          StandardOutputPipe,
          StandardErrorPipe
      );

  public new ICLICommand WithStandardInputPipe(PipeSource source) =>
      new CLICommand(
          TargetFilePath,
          Arguments,
          WorkingDirPath,
          Credentials,
          EnvironmentVariables,
          Validation,
          source,
          StandardOutputPipe,
          StandardErrorPipe
      );

  public new ICLICommand WithStandardOutputPipe(PipeTarget target) =>
      new CLICommand(
          TargetFilePath,
          Arguments,
          WorkingDirPath,
          Credentials,
          EnvironmentVariables,
          Validation,
          StandardInputPipe,
          target,
          StandardErrorPipe
      );

  public new ICLICommand WithStandardErrorPipe(PipeTarget target) =>
      new CLICommand(
          TargetFilePath,
          Arguments,
          WorkingDirPath,
          Credentials,
          EnvironmentVariables,
          Validation,
          StandardInputPipe,
          StandardOutputPipe,
          target
      );
}