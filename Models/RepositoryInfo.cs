namespace PipelineCoordinator.Models;

public record RepositoryInfo(string Path, string GithubUrl, string ProjectName, bool IsNuget = false);
