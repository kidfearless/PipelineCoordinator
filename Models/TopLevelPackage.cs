using Newtonsoft.Json;

namespace PipelineCoordinator.Models;

internal record TopLevelPackage([JsonProperty("id")] string Name, string RequestedVersion, string ResolvedVersion);
