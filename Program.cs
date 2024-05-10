using CliFx;
using CliFx.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PipelineCoordinator.Commands;
using PipelineCoordinator.Models;
using PipelineCoordinator.Services;

var serviceCollection = new ServiceCollection();
#if DEBUG
serviceCollection.AddTransient<ICLICommand, MockCLICommand>(
  t => new("", TimeSpan.FromSeconds(2)));
#else
serviceCollection.AddTransient<ICLICommand, CLICommand>(t => new(""));
#endif
serviceCollection.AddTransient<GithubService>();
serviceCollection.AddTransient<GitService>();
serviceCollection.AddTransient<AzureService>();
serviceCollection.AddTransient<DotNetService>();
serviceCollection.AddTransient<StartCommand>();
serviceCollection.AddTransient<FinishCommand>();
serviceCollection.AddTransient<ListenCommand>();
serviceCollection.AddTransient<FindCommand>();
serviceCollection.AddTransient<PushCommand>();
serviceCollection.AddSingleton<IConsole, SystemConsole>();
var builder = new CliApplicationBuilder();

var configurationBuilder = new ConfigurationBuilder()
    .AddJsonFile("Resources/repos.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = configurationBuilder.Build();

serviceCollection.AddSingleton<IConfiguration>(configuration);

var directoryInfo = configuration.GetSection("DirectoryInfo").Get<DirectoryConfiguration>();
var appSettings = new AppSettings(directoryInfo);
serviceCollection.AddSingleton(appSettings);
serviceCollection.AddSingleton(directoryInfo);

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