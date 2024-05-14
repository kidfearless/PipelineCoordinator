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

    var tasks = new List<Task>();
    foreach (var solutionFile in solutionFiles)
    {
      tasks.Add(ProcessSolutionAsync(solutionFile, storyId));
    }

    await Task.WhenAll(tasks);
  }

  private async Task ProcessSolutionAsync(string solutionFile, string storyId)
  {
    var solutionDirectory = Path.GetDirectoryName(solutionFile)!;
    var solutionName = Path.GetFileName(solutionFile);
    var overrideFileName = solutionName.Replace(".sln", ".override.sln");
    var overrideSolutionFile = Path.Combine(solutionDirectory, overrideFileName);

    await CreateSolutionAsync(solutionDirectory, Path.GetFileNameWithoutExtension(overrideFileName));

    var projects = await GetProjectsInSolutionAsync(solutionDirectory, solutionName);
    var overrideProjects = new List<string>();

    foreach (var project in projects)
    {
      var projectPath = Path.Combine(solutionDirectory, project);
      var overrideProjectPath = await CreateOverrideProjectAsync(projectPath);
      overrideProjects.Add(overrideProjectPath);
      await AddProjectToSolutionAsync(overrideSolutionFile, overrideProjectPath);
    }

    await CleanSolutionFileAsync(overrideSolutionFile);
    await ReplaceNugetWithLocalReferencesAsync(overrideProjects);
    await DisableUnitTestsAsync(solutionDirectory, storyId);
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

  private async Task ReplaceNugetWithLocalReferencesAsync(List<string> overrideProjects)
  {
    foreach (var overrideProject in overrideProjects)
    {
      var nugetPackages = await GetNugetPackagesAsync(overrideProject);
      foreach (var nuget in _directory.NugetPackages)
      {
        var hasNugetPackage = nugetPackages.Contains(nuget.ProjectName);
        if (hasNugetPackage)
        {
          var nugetPath = Directory.GetFiles(Path.GetDirectoryName(overrideProject)!, $"{nuget.ProjectName}.csproj", SearchOption.AllDirectories)
              .FirstOrDefault();

          if (nugetPath != null)
          {
            AddLocalReference(overrideProject, nugetPath, nuget.ProjectName);
          }
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

  private async Task DisableUnitTestsAsync(string solutionDirectory, string storyId)
  {
    if (!_directory.DisableUnitTests)
    {
      return;
    }

    var testProjects = Directory.GetFiles(solutionDirectory, "*Test.csproj", SearchOption.AllDirectories);
    foreach (var testProject in testProjects)
    {
      var xml = await File.ReadAllTextAsync(testProject);
      var doc = new XmlDocument();
      doc.LoadXml(xml);

      var itemGroupElement = doc.CreateElement("ItemGroup");
      var compileRemoveElement = doc.CreateElement("Compile");
      compileRemoveElement.SetAttribute("Remove", "**");
      itemGroupElement.AppendChild(compileRemoveElement);

      doc.DocumentElement!.AppendChild(itemGroupElement);

      var projectReferences = doc.SelectNodes("//ProjectReference");
      if (projectReferences != null)
      {
        foreach (XmlNode projectReference in projectReferences)
        {
          projectReference.ParentNode!.RemoveChild(projectReference);
        }
      }

      await File.WriteAllTextAsync(testProject, doc.OuterXml);
    }
  }

  private async Task<List<string>> GetNugetPackagesAsync(string csprojPath)
  {
    try
    {
      var folderPath = Path.GetDirectoryName(csprojPath)!;
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
                  "+ mockNugetString 
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

      var json = commandResult.StandardOutput;
      var results = JsonConvert.DeserializeObject<NugetReferenceResponse>(json)!;
      var nugetPackages = results.Projects
      .First()
      ?.Frameworks
      .SelectMany(f => f.TopLevelPackages)
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