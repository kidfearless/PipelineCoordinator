namespace PipelineCoordinator.Models;

public record DirectoryConfiguration(string RootDirectory, List<RepositoryInfo> Repositories);
