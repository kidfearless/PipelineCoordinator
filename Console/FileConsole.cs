using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CliFx.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

namespace PipelineCoordinator.Console;
internal class FileConsole : IConsole
{
  private FileStream _underlyingStream;

  public ConsoleReader Input { get; }
  public bool IsInputRedirected { get; }
  public ConsoleWriter Output { get; }
  public bool IsOutputRedirected { get; }


  public ConsoleWriter Error { get; }
  public bool IsErrorRedirected { get; }
  public ConsoleColor ForegroundColor { get; set; }
  public ConsoleColor BackgroundColor { get; set; }
  public int WindowWidth { get; set; }
  public int WindowHeight { get; set; }
  public int CursorLeft { get; set; }
  public int CursorTop { get; set; }

  public FileConsole(string filePath)
  {
    this._underlyingStream = File.OpenWrite(filePath);
    Error = new ConsoleWriter(this, _underlyingStream);
    Output = new ConsoleWriter(this, _underlyingStream);
  }

  public void Clear()
  {
    throw new NotImplementedException();
  }

  public ConsoleKeyInfo ReadKey(bool intercept = false)
  {
    throw new NotImplementedException();
  }

  public CancellationToken RegisterCancellationHandler()
  {
    throw new NotImplementedException();
  }

  public void ResetColor()
  {
    throw new NotImplementedException();
  }
}

internal static class FileConsoleExtensions
{
  internal static IServiceCollection AddFileConsole(this IServiceCollection services, string fileName)
  {
    // get the documents folder
    var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    // create the feature-logs folder
    var featureLogs = Path.Combine(documents, "feature-logs");
    if (!Directory.Exists(featureLogs))
    {
      Directory.CreateDirectory(featureLogs);
    }
    // create the file path
    var filePath = Path.Combine(featureLogs, fileName);
    // add the file console to the services
    services.AddSingleton<IConsole>(new FileConsole(filePath));
    return services;
  }
}
