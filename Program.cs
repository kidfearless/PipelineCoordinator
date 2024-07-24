using System.Diagnostics;

using CliFx;
using CliFx.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using PipelineCoordinator.Commands;
using PipelineCoordinator.Console;
using PipelineCoordinator.Models;
using PipelineCoordinator.Services;

internal class Program
{
  private static async Task Main(string[] args)
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddTransient<ICLICommand, CLICommand>();
    serviceCollection.AddTransient<GithubService>();
    serviceCollection.AddTransient<GitService>();
    serviceCollection.AddTransient<AzureService>();
    serviceCollection.AddTransient<DotNetService>();
    serviceCollection.AddTransient<StartCommand>();
    serviceCollection.AddTransient<FinishCommand>();
    serviceCollection.AddTransient<ListenCommand>();
    serviceCollection.AddTransient<FindCommand>();
    serviceCollection.AddTransient<PushCommand>();
    serviceCollection.AddSystemConsole(/*$"console.{DateTime.Now.Ticks}.log"*/);

    var builder = new CliApplicationBuilder();

    var configurationBuilder = new ConfigurationBuilder()
        .AddEnvironmentVariables();

    var configuration = configurationBuilder.Build();

    serviceCollection.AddSingleton<IConfiguration>(configuration);

    var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
    var filePath = Path.Combine(appDirectory, "Resources", "repos.json");

    var text = File.ReadAllText(filePath);
    AppSettings appSettings = JsonConvert.DeserializeObject<AppSettings>(text)!;

    serviceCollection.AddSingleton(appSettings);
    serviceCollection.AddSingleton(appSettings.DirectoryInfo);

    builder.AddCommand<StartCommand>();
    builder.AddCommand<FinishCommand>();
    builder.AddCommand<ListenCommand>();
    builder.AddCommand<FindCommand>();
    builder.AddCommand<PushCommand>();

    var provider = serviceCollection.BuildServiceProvider();
    builder.UseTypeActivator(commandTypes => provider);

    var con = builder.Build();
    if (System.Diagnostics.Debugger.IsAttached)
    {
      Console.WriteLine("Please enter a command to run");
      var command = Console.ReadLine()!;
      await con.RunAsync(command.Split(" "));
    }
    else
    {
      await con.RunAsync(args);
    }
  }
}