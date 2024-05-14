using CliFx.Infrastructure;

using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace PipelineCoordinator.Services;

internal class AzureService
{
  string? organizationUrl = "https://dev.azure.com/your-organization-name";
  string? personalAccessToken = "your-personal-access-token";
  string? projectName = "your-project-name";

  public AzureService(IConfiguration config)
  {
    personalAccessToken = config["AzureDevOps:PersonalAccessToken"];
    organizationUrl = config["AzureDevOps:OrganizationUrl"];
  }
  void Main(string[] args)
  {
    var credentials = new VssCredentials(new VssBasicCredential("", personalAccessToken));
    using var connection = new VssConnection(new Uri(organizationUrl!), credentials);
    var buildClient = connection.GetClient<BuildHttpClient>();

    var pipelineClient = connection.GetClient<PipelinesHttpClient>();

    var builds = GetLastBuilds(buildClient, projectName!, 10);

    foreach (Build build in builds)
    {
      System.Console.WriteLine($"Build ID: {build.Id}, Status: {build.Status}, Definition: {build.Definition.Name}");
    }
  }

  static List<Build> GetLastBuilds(BuildHttpClient buildClient, string projectName, int count)
  {
    var builds = buildClient.GetBuildsAsync(projectName, top: count).Result;
    return builds;
  }

}