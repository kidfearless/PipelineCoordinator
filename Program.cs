using CliFx;
using CliFx.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PipelineCoordinator.Commands;
using PipelineCoordinator.Models;
using PipelineCoordinator.Services;

var serviceCollection = new ServiceCollection();
serviceCollection.AddTransient<GithubService>();
serviceCollection.AddTransient<GitService>();
serviceCollection.AddTransient<AzureService>();
serviceCollection.AddTransient<DotNetService>();
serviceCollection.AddTransient<StartCommand>();
serviceCollection.AddTransient<FinishCommand>();
serviceCollection.AddSingleton<IConsole, SystemConsole>();
var builder = new CliApplicationBuilder();

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Resources/repos.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = configurationBuilder.Build();

serviceCollection.AddSingleton<IConfiguration>(configuration);
serviceCollection.AddSingleton<DirectoryConfiguration>(t => configuration.Get<AppSettings>()!.DirectoryInfo);
builder.AddCommand<StartCommand>();
builder.AddCommand<FinishCommand>();


var provider = serviceCollection.BuildServiceProvider();
builder.UseTypeActivator(commandTypes => provider);


var con = builder.Build();
if (System.Diagnostics.Debugger.IsAttached)
{
  Console.WriteLine("Please enter a command to run");
  var command = Console.ReadLine()!;
  Console.WriteLine("Please enter a story number to run against");
  var story = Console.ReadLine()!;
  await con.RunAsync([command, story]);
}
else
{
  await con.RunAsync(args);
}