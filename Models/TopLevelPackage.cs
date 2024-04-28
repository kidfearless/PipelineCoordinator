using Newtonsoft.Json;

namespace PipelineCoordinator.Models;

public record TopLevelPackage([JsonProperty("id")] string Name, string RequestedVersion, string ResolvedVersion);
