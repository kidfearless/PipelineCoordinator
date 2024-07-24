using CliFx.Infrastructure;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
using Newtonsoft.Json;
using PipelineCoordinator.Models;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using System.Reflection.Metadata;
using System.Xml.Linq;
using System.Xml;
using System;
using PipelineCoordinator.Commands;
using Microsoft.Build.Construction;
using Microsoft.TeamFoundation.Core.WebApi;

internal record OverrideResponse(string OverrideSolutionPath, string SolutionPath, string OverrideProjectPath, string ProjectPath);

internal class DotNetService
{
  private readonly IConsole _console;
  private readonly DirectoryConfiguration _directory;

  private ICLICommand DotNet { get; }
  public string StoryId { get; private set; }

  public DotNetService(IConsole console, DirectoryConfiguration directory, ICLICommand command)
  {
    _console = console;
    _directory = directory;
    DotNet = command.WithTargetFile("dotnet")
      .WithValidation(CommandResultValidation.None)
      .WithStandardOutputPipe(PipeTarget.ToDelegate((a) => _console.WriteLine(a)))
      .WithStandardErrorPipe(PipeTarget.ToDelegate((a) => _console.WriteLine(a)));
  }

  public async Task InitializeRepos(string storyId)
  {
    this.StoryId = storyId;
    var featureDirectory = Path.Combine(_directory.RootDirectory, storyId);
    var solutionFiles = Directory.GetFiles(featureDirectory, "*.sln", SearchOption.AllDirectories);
    var allOverrideProjects = new List<OverrideResponse>();
    foreach (var solutionFile in solutionFiles)
    {
      await RestoreSolutionAsync(solutionFile);
    }

    foreach (var repo in _directory.Repositories.OrderBy(t => t.IsNuget))
    {
      var repoPath = Path.Combine(featureDirectory, repo.Path);
      var results = await CreateOverrideFiles(repoPath, storyId);
      allOverrideProjects.AddRange(results);
    }

    foreach (var pair in allOverrideProjects.DistinctBy(t => t.OverrideSolutionPath))
    {
      await CleanSolutionFileAsync(pair.OverrideSolutionPath);
    }

    await ReplaceNugetWithLocalReferencesAsync(allOverrideProjects);

    await ResolveMissingProjectsAsync(allOverrideProjects);

    await DisableUnitTestsAsync(allOverrideProjects);
  }

  private async Task ResolveMissingProjectsAsync(List<OverrideResponse> allOverrideProjects)
  {
    foreach (var pair in allOverrideProjects)
    {
      var solutionPath = pair.OverrideSolutionPath;
      var projectsInSolution = await GetProjectsInSolutionAsync(Path.GetDirectoryName(solutionPath), Path.GetFileName(solutionPath));

      var projectsToAdd = new HashSet<string>();
      await CheckMissingDependenciesRecursively(pair.OverrideProjectPath, projectsInSolution, projectsToAdd);

      foreach (var projectToAdd in projectsToAdd)
      {
        await AddProjectToSolutionAsync(solutionPath, projectToAdd);
      }
    }
  }

  private async Task CheckMissingDependenciesRecursively(string projectPath, List<string> projectsInSolution, HashSet<string> projectsToAdd)
  {
    var projectReferences = await GetProjectReferencesAsync(projectPath);

    foreach (var reference in projectReferences)
    {
      var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath), reference));
      if (!projectsInSolution.Contains(fullPath) && !projectsToAdd.Contains(fullPath))
      {
        projectsToAdd.Add(fullPath);
        await CheckMissingDependenciesRecursively(fullPath, projectsInSolution, projectsToAdd);
      }
    }
  }

  private async Task<List<string>> GetProjectReferencesAsync(string projectPath)
  {
    var commandResult = await DotNet
        .WithWorkingDirectory(Path.GetDirectoryName(projectPath))
        .WithArguments($"list \"{Path.GetFileName(projectPath)}\" reference")
        .ExecuteBufferedAsync();

    var references = commandResult.StandardOutput
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.EndsWith(".csproj"))
        .ToList();

    return references;
  }

  private async Task RestoreSolutionAsync(string solutionPath)
  {
    var directory = Path.GetDirectoryName(solutionPath);
    var fileName = Path.GetFileName(solutionPath);

    var r = await DotNet.WithWorkingDirectory(directory)
      .WithArguments($"restore {fileName}")
      .ExecuteBufferedAsync();
  }

  private async Task<List<OverrideResponse>> CreateOverrideFiles(string repoPath, string storyId)
  {
    var solutionFiles = Directory.GetFiles(repoPath, "*.sln", SearchOption.AllDirectories);
    var overrideProjects = new List<OverrideResponse>();
    foreach (var solutionFile in solutionFiles)
    {
      var solutionDirectory = Path.GetDirectoryName(solutionFile)!;
      var solutionName = Path.GetFileName(solutionFile);
      var overrideFileName = solutionName.Replace(".sln", ".override.sln");
      var overrideSolutionFile = Path.Combine(repoPath, overrideFileName);

      await CreateSolutionAsync(repoPath, Path.GetFileNameWithoutExtension(overrideFileName));

      var projects = await GetProjectsInSolutionAsync(solutionDirectory, solutionName);

      foreach (var project in projects)
      {
        var projectPath = Path.GetFullPath(Path.Combine(solutionDirectory, project));
        if (!File.Exists(projectPath))
        {
          continue;
        }
        var overrideProjectPath = await CreateOverrideProjectAsync(projectPath);
        overrideProjects.Add(new(overrideSolutionFile, solutionFile, overrideProjectPath, projectPath));
        await AddProjectToSolutionAsync(overrideSolutionFile, overrideProjectPath);
      }
    }
    return overrideProjects;
  }

  private async Task CreateSolutionAsync(string solutionDirectory, string overrideFileName)
  {
    await DotNet.WithWorkingDirectory(solutionDirectory)
        .WithArguments($"new sln -n {overrideFileName}")
        .ExecuteAsync();
  }

  private async Task<List<string>> GetProjectsInSolutionAsync(string solutionFolder, string solutionFileName)
  {
    var mockRepo = _directory.Repositories.GetRandom();
    var mockPath = Path.Join(solutionFolder, StoryId, mockRepo.Path);
    var commandResult = await DotNet
        .WithWorkingDirectory(solutionFolder)
        .WithArguments($"sln {solutionFileName} list")
        .WithMockOutput(mockPath)
        .ExecuteBufferedAsync();

    var projects = commandResult.StandardOutput
        .Split(Environment.NewLine)
        .Where(l => l.Contains(".csproj"))
        .ToList();

    return projects;
  }

  private async Task<string> CreateOverrideProjectAsync(string projectPath)
  {
    var overrideProjectPath = projectPath.Replace(".csproj", ".override.csproj");
    if (File.Exists(overrideProjectPath))
    {
      return overrideProjectPath;
    }
    try
    {


      var nugetPackages = await GetNugetPackagesAsync(projectPath);
      var matchingNugetPackages = _directory.NugetPackages.IntersectBy(nugetPackages, k => k.ProjectName);

      var doc = new XmlDocument();
      doc.LoadXml("<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

      var importElement = doc.CreateElement("Import");
      importElement.SetAttribute("Project", projectPath);
      doc.DocumentElement!.AppendChild(importElement);

      var itemGroupElement = doc.CreateElement("ItemGroup");
      doc.DocumentElement.AppendChild(itemGroupElement);

      foreach (var nuget in matchingNugetPackages)
      {
        var packageReferenceElement = doc.CreateElement("PackageReference");
        packageReferenceElement.SetAttribute("Remove", nuget.ProjectName);
        itemGroupElement.AppendChild(packageReferenceElement);
      }

      doc.Save(overrideProjectPath);

    }
    catch (Exception ex)
    {

    }
    return overrideProjectPath;
  }

  private async Task AddProjectToSolutionAsync(string solutionPath, string projectPath)
  {
    var solutionFolder = Path.GetDirectoryName(solutionPath)!;
    var solutionName = Path.GetFileName(solutionPath);

    await DotNet
        .WithWorkingDirectory(solutionFolder)
        .WithArguments($"sln {solutionName} add {projectPath}")
        .ExecuteAsync();
  }

  private async Task CleanSolutionFileAsync(string overrideSolutionFile)
  {
    var fileText = await File.ReadAllLinesAsync(overrideSolutionFile);
    var newFileText = new List<string>(fileText.Length);

    for (int i = 0; i < fileText.Length; i++)
    {
      var line = fileText[i];
      if (line.StartsWith("Project") && !line.Contains(".csproj"))
      {
        i++; // skip the next two lines
        continue;
      }
      newFileText.Add(line);
    }

    await File.WriteAllLinesAsync(overrideSolutionFile, newFileText);
  }

  private async Task ReplaceNugetWithLocalReferencesAsync(List<OverrideResponse> overrideProjects)
  {
    foreach (var pair in overrideProjects)
    {
      var nugetPackages = await GetNugetPackagesAsync(pair.ProjectPath);
      var references = _directory.NugetPackages.IntersectBy(nugetPackages, t => t.ProjectName);
      foreach (var reference in references)
      {

        var resolvedFolder = Path.Join(_directory.RootDirectory, StoryId, reference.Path);
        var resolvedPath = Directory.GetFiles(resolvedFolder, $"*{reference.ProjectName}.override.csproj", SearchOption.AllDirectories).FirstOrDefault();
        var overrideReference = overrideProjects.FirstOrDefault(t => t.OverrideProjectPath == resolvedPath);

        if (overrideReference != null)
        {
          AddLocalReference(pair.OverrideProjectPath, overrideReference.OverrideProjectPath, reference.ProjectName);
        }
        else
        {
          var overrideSolutionPath = pair.OverrideSolutionPath;
          var overrideProjectPath = await CreateOverrideProjectAsync(resolvedPath);
          await AddProjectToSolutionAsync(overrideSolutionPath, overrideProjectPath);
          AddLocalReference(pair.OverrideProjectPath, overrideProjectPath, reference.ProjectName);
        }
      }
    }
  }

  private void AddLocalReference(string csprojPath, string importPath, string projectName)
  {
    var xml = File.ReadAllText(csprojPath);
    var doc = new XmlDocument();
    doc.LoadXml(xml);

    var itemGroup = doc.CreateElement("ItemGroup", doc.DocumentElement!.NamespaceURI);

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
  private async Task DisableUnitTestsAsync(List<OverrideResponse> overrideProjects)
  {
    if (!_directory.DisableUnitTests)
    {
      return;
    }

    foreach (var pair in overrideProjects.Where(t => t.ProjectPath.Contains(".Test")))
    {
      var xml = await File.ReadAllTextAsync(pair.ProjectPath);
      var doc = new XmlDocument();
      doc.LoadXml(xml);

      // remove all files from the project
      var itemGroupElement = doc.CreateElement("ItemGroup");
      var compileRemoveElement = doc.CreateElement("Compile");
      compileRemoveElement.SetAttribute("Remove", "**");
      var contentRemoveElement = doc.CreateElement("Content");
      contentRemoveElement.SetAttribute("Remove", "**");
      var embeddedResourceRemoveElement = doc.CreateElement("EmbeddedResource");
      embeddedResourceRemoveElement.SetAttribute("Remove", "**");
      var noneRemoveElement = doc.CreateElement("None");
      noneRemoveElement.SetAttribute("Remove", "**");

      itemGroupElement.AppendChild(compileRemoveElement);
      itemGroupElement.AppendChild(contentRemoveElement);
      itemGroupElement.AppendChild(embeddedResourceRemoveElement);
      itemGroupElement.AppendChild(noneRemoveElement);
      doc.DocumentElement.AppendChild(itemGroupElement);

      // remove any project references
      var projectReferences = doc.SelectNodes("//ProjectReference");
      if (projectReferences != null)
      {
        foreach (XmlNode projectReference in projectReferences)
        {
          projectReference.ParentNode.RemoveChild(projectReference);
        }
      }

      // save the updated csproj file
      await File.WriteAllTextAsync(pair.ProjectPath, doc.OuterXml);
    }
  }

  private async Task<List<string>> GetNugetPackagesAsync(string csprojPath)
  {
    try
    {
      var folderPath = Path.GetFullPath(Path.GetDirectoryName(csprojPath)!)!;
      var csprojFileName = Path.GetFileName(csprojPath)!;
      var mockNuget = _directory.NugetPackages.GetRandom();
      var mockNugetString = $"{{\"{mockNuget.ProjectName}\"}},";
      var mockjson = @"{
        ""projects"": [
          {
            ""frameworks"": [
              {
                ""framework"": "".NETCoreApp,Version=v6.0"",
                ""topLevelPackages"": [
                  {
                    ""id"": ""Microsoft.AspNetCore.Authentication.JwtBearer"",
                    ""version"": ""6.0.0""
                  },
                  " + mockNugetString
                  + @"
                  {
                    ""id"": ""Swashbuckle.AspNetCore"",
                    ""version"": ""6.2.3""
                  }
                ]
              }
            ]
          }
        ]
      }";

      var commandResult = await DotNet
          .WithWorkingDirectory(folderPath)
          .WithArguments($"list \"{csprojFileName}\" package --format json")
          .WithMockOutput(mockjson)
          .ExecuteBufferedAsync();

      if (commandResult.StandardOutput!.StartsWith("error"))
      {
        return new List<string>();
      }
      var json = commandResult.StandardOutput;
      var results = JsonConvert.DeserializeObject<NugetReferenceResponse>(json)!;
      var nugetPackages = results?.Projects
      .FirstOrDefault()
      ?.Frameworks
      ?.SelectMany(f => f.TopLevelPackages)
      .Where(f => f != null)
          .Select(p => p.Name) ?? new List<string>();

      return nugetPackages.ToList();
    }
    catch (Exception)
    {
      return new List<string>();
    }
  }
}