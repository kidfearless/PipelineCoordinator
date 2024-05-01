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
    //await TranslateSolutionsToRootAsync(featureDirectory, storyId);
    await AddProjectsToSolutionsAsync(featureDirectory, storyId);
    await ReplaceNugetWithLocalReferencesAsync(featureDirectory, storyId);
    await AddMissingProjectsToSolutionAsync(featureDirectory, storyId);
    await DisableUnitTestsAsync(featureDirectory, storyId);
  }

  private async Task TranslateSolutionsToRootAsync(string featureDirectory, string storyId)
  {
    foreach (var repo in _directory.Repos)
    {
      var repoRoot = Path.Combine(featureDirectory, repo.Path);
      var solutionFiles = Directory.GetFiles(repoRoot, "*.sln", SearchOption.AllDirectories);
      foreach (var solutionFile in solutionFiles)
      {
        var solutionDirectory = Path.GetDirectoryName(solutionFile)!;
        var solutionName = Path.GetFileNameWithoutExtension(solutionFile);
        var overrideFileName = $"{solutionName}.override.sln";
        var overrideSolutionFile = Path.Combine(solutionDirectory, overrideFileName);

        // Create override solution in the same location
        File.Copy(solutionFile, overrideSolutionFile);

        // Restore the override solution to allow cooperation
        await RestoreAsync(solutionDirectory, overrideFileName);

        // List projects in the solution
        var projects = await GetProjectsInSolution(solutionDirectory);

        // Remove all projects from the override solution
        foreach (var project in projects)
        {
          await RemoveProjectFromSolutionAsync(overrideSolutionFile, project);
        }

        // Move the override solution to the root
        var rootSolutionFile = Path.Combine(repoRoot, $"{solutionName}.override.sln");
        File.Move(overrideSolutionFile, rootSolutionFile);

        // Restore the solution
        await RestoreAsync(repoRoot, overrideFileName);

        // Add the projects back to the solution
        foreach (var project in projects)
        {
          var projectPath = Path.Combine(solutionDirectory, project);
          await AddProjectToSolutionAsync(rootSolutionFile, projectPath);
        }
      }
    }
  }

  private async Task RestoreSolutionsAsync(string featureDirectory, string storyId)
  {
    var solutionFiles = Directory.GetFiles(featureDirectory, "*.sln", SearchOption.AllDirectories);
    foreach (var solution in solutionFiles)
    {
      var directory = Path.GetDirectoryName(solution);
      var fileName = Path.GetFileName(solution);
      await RestoreAsync(directory, fileName);
    }
  }

  private async Task AddMissingProjectsToSolutionAsync(string featureDirectory, string storyId)
  {
    var rootDirectory = featureDirectory;

    foreach (var repo in _directory.Repos)
    {
      var repoFolder = Path.Combine(rootDirectory, repo.Path);
      var solutions = Directory.GetFiles(repoFolder, "*.override.sln", SearchOption.AllDirectories);
      foreach (var solution in solutions)
      {
        var solutionFolder = Path.GetDirectoryName(solution)!;
        var projects = await GetProjectsInSolution(solutionFolder);
        var overrides = projects.Where(p => p.Contains("override"));
        foreach (var projectFile in overrides)
        {
          var projectPath = Path.Combine(solutionFolder, projectFile);
          var originalFile = projectPath.Replace(".override", "");
          var projectDirectory = Path.GetDirectoryName(projectPath)!;
          var projectName = Path.GetFileName(projectPath);
          var subProjects = await GetProjectsInProject(projectDirectory, projectName);
          foreach (var subFile in subProjects)
          {
            await AddProjectToSolutionAsync(solution, subFile);
          }
        }
      }
    }
  }


  public async Task AddProjectToSolutionAsync(string solutionPath, string projectPath)
  {
    var solutionFolder = Path.GetDirectoryName(solutionPath)!;
    var result = await DotNet
        .WithWorkingDirectory(solutionFolder)
        .WithArguments($"sln add {projectPath}")
        .ExecuteAsync();
  }

  public async Task RemoveProjectFromSolutionAsync(string solutionPath, string projectPath)
  {
    var result = await DotNet.WithWorkingDirectory(solutionPath)
        .WithArguments($"sln remove {projectPath}")
        .ExecuteAsync();
  }

  private void AddLocalReference(string csprojPath, string importPath, string projectName)
  {
    var xml = File.ReadAllText(csprojPath);
    var doc = new XmlDocument();
    doc.LoadXml(xml);

    var namespaceManager = new XmlNamespaceManager(doc.NameTable);
    namespaceManager.AddNamespace("ns", doc.DocumentElement.NamespaceURI);

    var itemGroup = doc.CreateElement("ItemGroup", doc.DocumentElement.NamespaceURI);

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
    var solutionFiles = Directory.GetFiles(featureDirectory, "*.override.sln", SearchOption.AllDirectories);
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
            var overrideProjectPath = CreateOverrideCsproj(projectPath);
            await AddProjectToSolutionAsync(solution, overrideProjectPath);// add override
            await RemoveProjectFromSolutionAsync(solution, project);//remove original
            // TODO: Scan for sub projects
          }
        }
      }
    }
  }

  private string CreateOverrideCsproj(string projectPath)
  {
    var newPath = projectPath.Replace(".csproj", ".override.csproj");
    if (File.Exists(newPath))
    {
      return newPath;
    }

    var doc = new XmlDocument();
    doc.LoadXml("<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

    var importElement = doc.CreateElement("Import");
    importElement.SetAttribute("Project", projectPath);
    doc.DocumentElement.AppendChild(importElement);

    var itemGroupElement = doc.CreateElement("ItemGroup");
    doc.DocumentElement.AppendChild(itemGroupElement);

    var nugetPackages = _directory.NugetPackages;
    foreach (var nuget in nugetPackages)
    {
      var packageReferenceElement = doc.CreateElement("PackageReference");
      packageReferenceElement.SetAttribute("Remove", nuget.ProjectName);
      itemGroupElement.AppendChild(packageReferenceElement);
    }

    //// add the nuget package
    //var packageReference = doc.CreateElement("PackageReference");
    //packageReference.SetAttribute("Include", nugetPath);
    //itemGroupElement.AppendChild(packageReference);

    doc.Save(newPath);

    return newPath;
  }

  private async Task ReplaceNugetWithLocalReferencesAsync(string featureDirectory, string storyId)
  {
    // scan for all csproj files
    var csprojFiles = Directory.GetFiles(featureDirectory, "*.csproj", SearchOption.AllDirectories)
      .Where(t => t.Contains(".override"));

    foreach (var csprojFile in csprojFiles)
    {
      foreach (var nuget in _directory.NugetPackages)
      {
        var referenceCsproj = csprojFile.Replace(".override", "");
        var hasNugetPackage = await HasNugetPackageAsync(referenceCsproj, nuget.ProjectName);
        var nugetDirectory = Path.Combine(featureDirectory, nuget.Path);
        var nugetPath = Directory.GetFiles(nugetDirectory, "*.csproj", SearchOption.AllDirectories)
          .Where(t => !t.Contains("Test"))
          .OrderByDescending(t => t.IndexOf("override"))
          .First();
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

  private async Task<List<string>> GetProjectsInProject(string projectFolder, string fileName)
  {
    var commandResult = await DotNet
        .WithWorkingDirectory(projectFolder)
        .WithArguments($"list {fileName} reference")
        .ExecuteBufferedAsync();

    var projects = commandResult.StandardOutput
        .Split(Environment.NewLine)
        .Where(l => l.Contains(".csproj"))
        .ToList();

    return projects;
  }
  private async Task RestoreAsync(string directoryPath, string fileName)
  {
    var _ = await DotNet
        .WithWorkingDirectory(directoryPath)
        .WithArguments($"restore {fileName}")
        .ExecuteAsync();
  }

  private async Task<bool> HasNugetPackageAsync(string csprojPath, string nugetName)
  {
    try
    {
      csprojPath = Path.GetFullPath(csprojPath);
      var folderPath = Path.GetDirectoryName(csprojPath)!;

      // resolve the .. and . in the path
      folderPath = Path.GetFullPath(folderPath);


      var commandResult = await DotNet
          .WithWorkingDirectory(folderPath)
          .WithArguments($"list \"{csprojPath}\" package --format json")
          .ExecuteBufferedAsync();

      var json = commandResult.StandardOutput;
      var results = JsonConvert.DeserializeObject<NugetReferenceResponse>(json)!;
      var nugetPackages = results.Projects
          .First()
          ?.Frameworks
          .SelectMany(f => f.TopLevelPackages)
          .Where(f => f != null)
          .Select(p => p.Name) ?? [];

      var hasPackage = nugetPackages.Contains(nugetName);
      return hasPackage;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine(ex);
      return false;
    }
  }
}