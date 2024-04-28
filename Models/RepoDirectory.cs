using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace PipelineCoordinator.Models;
internal record RepoDirectory([JsonProperty("dir")] string Directory, [JsonProperty("subDirs")] List<RepoDirectory>? SubDirectories);
