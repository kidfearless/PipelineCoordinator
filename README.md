# Pipeline Coordinator

Pipeline Coordinator is a command-line tool that automates the process of initializing and managing feature development for a given story ID across multiple repositories.

## Features

- Clones repositories from GitHub based on the provided configuration
- Creates feature branches for the specified story ID
- Restores and adds projects to solutions
- Replaces NuGet package references with local project references for debugging
- Disables unit tests for the feature development
- Marks the start and end of feature development with Git commits
- Publishes feature branches to remote repositories

## Prerequisites

- .NET 8.0 SDK
- Git
- GitHub CLI (gh)

## Configuration

The tool relies on a `repos.json` configuration file located in the `Resources` directory. This file contains the directory configuration for the repositories and their respective GitHub URLs.

Example `repos.json`:

```json
{
  "DirectoryInfo": {
    "RootDirectory": "C:\\Projects",
    "Repositories": [
      {
        "Path": "Repo1",
        "GithubUrl": "https://github.com/user/repo1.git",
        "ProjectName": "Repo1",
        "IsNuget": false
      },
      {
        "Path": "Repo2",
        "GithubUrl": "https://github.com/user/repo2.git",
        "ProjectName": "Repo2",
        "IsNuget": true
      }
    ]
  }
}
```

## Usage

To start feature development for a story, run the following command:

```
dotnet run -- start <storyId>
```

Replace `<storyId>` with the actual story ID.

To finish feature development for a story, run the following command:

```
dotnet run -- finish <storyId>
```

Replace `<storyId>` with the actual story ID.

## Dependencies

The project uses the following dependencies:

- CliFx: A framework for building command-line applications in .NET
- Microsoft.Extensions.Configuration: Provides configuration support
- Microsoft.Extensions.DependencyInjection: Provides dependency injection support
- Newtonsoft.Json: A JSON framework for .NET

## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.