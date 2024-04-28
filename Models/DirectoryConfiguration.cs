namespace PipelineCoordinator.Models;

internal record DirectoryConfiguration(string RootDirectory, bool DisableUnitTests, IReadOnlyList<RepositoryInfo> Repositories)
{
  public IEnumerable<RepositoryInfo> NugetPackages => Repositories.Where(r => r.IsNuget);
}
