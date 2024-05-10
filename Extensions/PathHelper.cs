using PipelineCoordinator.Models;

internal static class PathHelper
{
  public static bool IsDirectoryPath(string path)
  {
    var result = File.GetAttributes(path).HasFlag(FileAttributes.Directory);
    return result;
  }
  internal static string ReverseSearchPath(string directory, string searchPath)
  {
    if (!IsDirectoryPath(directory))
    {
      directory = Path.GetDirectoryName(directory);
    }
    if (string.IsNullOrEmpty(directory))
    {
      throw new InvalidOperationException($"Could not find {searchPath} in the current directory or any of its parent directories.");
    }

    var folders = Directory.GetDirectories(directory).Select(t => Path.GetFileName(t));
    if (!folders.Contains(searchPath))
    {
      return ReverseSearchPath(directory, searchPath);
    }

    return directory;
  }

  internal static string GetFeatureNumber(string currentPath)
  {
    // E:\Features\12345\Folder\SubFolder
    var parts = currentPath.Split(Path.DirectorySeparatorChar);
    var featureNumber = parts.FirstOrDefault(p => int.TryParse(p, out var number));
    if (string.IsNullOrEmpty(featureNumber))
    {
      throw new InvalidOperationException($"Could not find a feature number in the current path: {currentPath}");
    }
    return featureNumber;
  }
}
