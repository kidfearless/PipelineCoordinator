namespace PipelineCoordinator.Commands;

using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using CliWrap.Buffered;
using CliWrap.Builders;
internal interface ICLICommand
{
  string TargetFilePath { get; set; }
  string? Arguments { get; set; }
  string? WorkingDirPath { get; set; }
  Credentials? Credentials { get; set; }
  IReadOnlyDictionary<string, string?>? EnvironmentVariables { get; set; }
  CommandResultValidation Validation { get; set; }
  PipeSource? StandardInputPipe { get; set; }
  PipeTarget? StandardOutputPipe { get; set; }
  PipeTarget? StandardErrorPipe { get; set; }

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
  Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken = default);
  Task<BufferedCommandResult> ExecuteBufferedAsync(CancellationToken cancellationToken = default);
  ICLICommand WithMockDelay(TimeSpan delay) => this;
  ICLICommand WithMockError(string error) => this;
  ICLICommand WithMockOutput(string output) => this;
}
