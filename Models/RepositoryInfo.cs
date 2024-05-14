namespace PipelineCoordinator.Models;

internal record RepositoryInfo(string Path, string GithubUrl, string ProjectName, bool IsNuget = false);
