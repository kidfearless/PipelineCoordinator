namespace PipelineCoordinator.Models;

public record NugetReferenceResponse(int Version, string Parameters, List<Project> Projects);
