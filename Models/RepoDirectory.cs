using Newtonsoft.Json;

namespace PipelineCoordinator.Models;
internal record RepoDirectory([JsonProperty("dir")] string Directory, [JsonProperty("subDirs")] HashSet<RepoDirectory>? SubDirectories);
