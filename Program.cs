using System.ComponentModel.Design;

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
serviceCollection.AddSingleton<IConsole, SystemConsole>();
var builder = new CliApplicationBuilder();

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Resources/repos.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = configurationBuilder.Build();

serviceCollection.AddSingleton<IConfiguration>(configuration);
serviceCollection.AddTransient<DirectoryConfiguration>(t => configuration.Get<AppSettings>()!.DirectoryInfo);
builder.AddCommand<StartCommand>();


var provider = serviceCollection.BuildServiceProvider();
builder.UseTypeActivator(commandTypes => provider);



var con = builder.Build();
string[] argss = ["start", "138880"];
await con.RunAsync(argss);