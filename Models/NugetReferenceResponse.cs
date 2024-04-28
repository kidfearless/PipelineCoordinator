namespace PipelineCoordinator.Models;

internal record NugetReferenceResponse(int Version, string Parameters, List<Project> Projects);
