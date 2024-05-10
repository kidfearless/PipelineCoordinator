namespace PipelineCoordinator.Models;

public record DirectoryConfiguration(string RootDirectory, bool DisableUnitTests, IReadOnlyList<RepositoryInfo> Repositories)
{
  public IEnumerable<RepositoryInfo> Repos => Repositories.Distinct();
  public IEnumerable<RepositoryInfo> NugetPackages => Repos.Where(r => r.IsNuget).Distinct();
}
