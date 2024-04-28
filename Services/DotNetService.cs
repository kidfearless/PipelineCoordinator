using System.Drawing;
using System.IO;
using System.Xml;

using CliFx.Infrastructure;

using Newtonsoft.Json;

using PipelineCoordinator.Models;

namespace PipelineCoordinator.Services;

internal class DotNetService(IConsole _console, DirectoryConfiguration _directory)
{
  private Command DotNet => Cli.Wrap("dotnet")
      .WithValidation(CommandResultValidation.None)
      .WithStandardOutputPipe(PipeTarget.ToStream(_console.Output.BaseStream))
      .WithStandardErrorPipe(PipeTarget.ToStream(_console.Error.BaseStream));

  public async Task InitializeRepos(string storyId)
  {
    var featureDirectory = Path.Combine(_directory.RootDirectory, $"feature/story-{storyId}");
    await RestoreSolutionsAsync(featureDirectory, storyId);
    await AddProjectsToSolutionsAsync(featureDirectory, storyId);
    await ReplaceNugetWithLocalReferencesAsync(featureDirectory, storyId);
    await DisableUnitTestsAsync(featureDirectory, storyId);
  }

  private async Task RestoreSolutionsAsync(string featureDirectory, string storyId)
  {
    var solutionFiles = Directory.GetFiles(featureDirectory, "*.sln", SearchOption.AllDirectories);
    var solutionDirectories = solutionFiles.Select(s => Path.GetDirectoryName(s)!);
    foreach (var solution in solutionFiles)
    {
      await RestoreAsync(solution);
    }
  }


  public async Task AddProjectToSolutionAsync(string solutionPath, string projectPath)
  {
    var result = await DotNet.WithWorkingDirectory(solutionPath)
        .WithArguments($"sln add {projectPath}")
        .ExecuteAsync();
  }

  private void AddLocalReference(string csprojPath, string importPath, string projectName)
  {

    var xml = File.ReadAllText(csprojPath);
    var doc = new XmlDocument();
    doc.LoadXml(xml);

    var namespaceManager = new XmlNamespaceManager(doc.NameTable);
    namespaceManager.AddNamespace("ns", doc.DocumentElement.NamespaceURI);

    var packageReference = doc.SelectSingleNode($"//ns:PackageReference[@Include='{projectName}']", namespaceManager);
    if (packageReference != null)
    {
      var condition = doc.CreateAttribute("Condition");
      condition.Value = "'$(Configuration)' != 'Debug'";
      packageReference.Attributes.Append(condition);
    }

    var itemGroup = doc.CreateElement("ItemGroup", doc.DocumentElement.NamespaceURI);
    var itemGroupCondition = doc.CreateAttribute("Condition");
    itemGroupCondition.Value = "'$(Configuration)' == 'Debug'";
    itemGroup.Attributes.Append(itemGroupCondition);

    var projectReference = doc.CreateElement("ProjectReference", doc.DocumentElement.NamespaceURI);
    projectReference.SetAttribute("Include", importPath);
    itemGroup.AppendChild(projectReference);

    doc.DocumentElement.AppendChild(itemGroup);

    var settings = new XmlWriterSettings
    {
      Indent = true,
      IndentChars = "  ",
      NewLineChars = "\n",
      NewLineHandling = NewLineHandling.Replace
    };

    using var writer = XmlWriter.Create(csprojPath, settings);
    doc.Save(writer);
  }

  private async Task DisableUnitTestsAsync(string featureDirectory, string storyId)
  {
    var solutionFiles = Directory.GetFiles(featureDirectory, "*.sln", SearchOption.AllDirectories);
    var solutionDirectories = solutionFiles.Select(s => Path.GetDirectoryName(s)!);
    foreach (var solution in solutionFiles)
    {
      await RestoreAsync(solution);
    }
  }



  private async Task AddProjectsToSolutionsAsync(string featureDirectory, string storyId)
  {
    var solutionFiles = Directory.GetFiles(featureDirectory, "*.sln", SearchOption.AllDirectories);
    var solutionDirectories = solutionFiles.Select(s => Path.GetDirectoryName(s)!);

    foreach (var solution in solutionDirectories)
    {
      // TODO: Create override projects
      var projects = await GetProjectsInSolution(solution);
      foreach (var project in projects)
      {
        var projectPath = Path.Combine(solution, project);
        foreach (var nugetProject in _directory.Repositories.Where(r => r.IsNuget))
        {
          var hasPackage = await HasNugetPackageAsync(projectPath, nugetProject.ProjectName);
          if (hasPackage)
          {
            var nugetPath = Path.Combine(_directory.RootDirectory, $"feature/story-{storyId}", nugetProject.Path);
            await AddProjectToSolutionAsync(solution, nugetPath);
            // TODO: Scan for sub projects
          }
        }
      }
    }
  }

  private async Task ReplaceNugetWithLocalReferencesAsync(string featureDirectory, string storyId)
  {
    // scan for all csproj files
    var csprojFiles = Directory.GetFiles(featureDirectory, "*.csproj", SearchOption.AllDirectories);

    foreach (var csprojFile in csprojFiles)
    {
      foreach(var nuget in _directory.Repositories.Where(r => r.IsNuget))
      {
        var hasNugetPackage = await HasNugetPackageAsync(csprojFile, nuget.ProjectName);
        var nugetPath = Path.Combine(featureDirectory, nuget.Path);

        if (hasNugetPackage)
        {
          AddLocalReference(csprojFile, nugetPath, nuget.ProjectName);
        }
      }
    }
  }

  private async Task<List<string>> GetProjectsInSolution(string solutionFolder)
  {
    var commandResult = await DotNet
        .WithWorkingDirectory(solutionFolder)
        .WithArguments("sln list")
        .ExecuteBufferedAsync();

    var projects = commandResult.StandardOutput
        .Split(Environment.NewLine)
        .Where(l => l.Contains(".csproj"))
        .ToList();

    return projects;
  }

  private async Task RestoreAsync(string directoryPath)
  {
    var _ = await DotNet
        .WithWorkingDirectory(directoryPath)
        .WithArguments("restore")
        .ExecuteAsync();
  }

  private async Task<bool> HasNugetPackageAsync(string csprojPath, string nugetName)
  {
    var folderPath = Path.GetDirectoryName(csprojPath)!;


    var commandResult = await DotNet
        .WithWorkingDirectory(folderPath)
        .WithArguments($"list package --format json")
        .ExecuteBufferedAsync();

    var json = commandResult.StandardOutput;
    var results = JsonConvert.DeserializeObject<NugetReferenceResponse>(json)!;
    var nugetPackages = results.Projects
        .First()
        .Frameworks
        .SelectMany(f => f.TopLevelPackages)
        .Select(p => p.Name);

    var hasPackage = nugetPackages.Contains(nugetName);
    return hasPackage;
  }
}