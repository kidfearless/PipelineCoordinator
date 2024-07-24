namespace PipelineCoordinator.Models;

internal record DirectoryConfiguration(string RootDirectory, bool DisableUnitTests, HashSet<RepositoryInfo> Repositories)
{
  public HashSet<RepositoryInfo> Repos => Repositories.ToHashSet();
  public HashSet<RepositoryInfo> NugetPackages => Repos.Where(r => r.IsNuget).ToHashSet();
}
