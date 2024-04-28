using System.Xml;

using CliFx.Infrastructure;

using Newtonsoft.Json;

using PipelineCoordinator.Models;
using Microsoft.Build.Construction;

namespace PipelineCoordinator.Services;

internal class DotNetService(IConsole _console, DirectoryConfiguration _directory)
{
  private Command DotNet => Cli.Wrap("dotnet")
      .WithValidation(CommandResultValidation.None)
      .WithStandardOutputPipe(PipeTarget.ToStream(_console.Output.BaseStream))
      .WithStandardErrorPipe(PipeTarget.ToStream(_console.Error.BaseStream));

  public async Task InitializeRepos(string storyId)
  {
    var featureDirectory = Path.Combine(_directory.RootDirectory, storyId);
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
    if (!_directory.DisableUnitTests)
    {
      return;
    }

    var originalCsProjFiles = Directory
      .GetFiles(featureDirectory, "*.csproj", SearchOption.AllDirectories)
      .Where(f => !f.Contains(".override") && f.Contains("Test"));

    foreach (var testProject in originalCsProjFiles)
    {
      // open the csproj file
      var xml = await File.ReadAllTextAsync(testProject);
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
      await File.WriteAllTextAsync(testProject, doc.OuterXml);
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

        foreach (var nugetProject in _directory.NugetPackages)
        {
          var hasPackage = await HasNugetPackageAsync(projectPath, nugetProject.ProjectName);
          if (hasPackage)
          {
            var nugetPath = Path.Combine(_directory.RootDirectory, storyId, nugetProject.Path);
            var overrideProjectPath = CreateOverrideCsproj(projectPath);
            await AddProjectToSolutionAsync(solution, overrideProjectPath);
            // TODO: Scan for sub projects
          }
        }
      }
    }
  }

  private string CreateOverrideCsproj(string projectPath)
  {
    var newPath = projectPath.Replace(".csproj", ".override.csproj")!;
    if (File.Exists(newPath))
    {
      return newPath;
    }

    var project = ProjectRootElement.Create();
    project.Sdk = "Microsoft.NET.Sdk";

    var _ = project.AddImport(projectPath);

    var itemGroup = project.AddItemGroup();
    var nugetPackages = _directory.NugetPackages;
    foreach (var nuget in nugetPackages)
    {
      var packageReference = itemGroup.AddItem("PackageReference", "");
      packageReference.Remove = nuget.ProjectName;
    }

    project.Save(newPath);

    return newPath;
  }

  private async Task ReplaceNugetWithLocalReferencesAsync(string featureDirectory, string storyId)
  {
    // scan for all csproj files
    var csprojFiles = Directory.GetFiles(featureDirectory, "*.csproj", SearchOption.AllDirectories);
    var projects = _directory.NugetPackages
      .SelectMany(nuget => csprojFiles
        .Select(csprojFile => (nuget, csprojFile)));

    foreach (var (nuget, csprojFile) in projects)
    {
      if (!csprojFile.Contains(".override"))
      {
        continue;
      }

      var referenceCsproj = csprojFile.Replace(".override", "");
      var hasNugetPackage = await HasNugetPackageAsync(referenceCsproj, nuget.ProjectName);
      var nugetPath = Path.Combine(featureDirectory, nuget.Path);
      if (hasNugetPackage)
      {
        AddLocalReference(csprojFile, nugetPath, nuget.ProjectName);
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