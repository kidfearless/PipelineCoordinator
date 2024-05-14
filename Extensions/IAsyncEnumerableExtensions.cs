internal static class IAsyncEnumerableExtensions
{
  public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> asyncEnumerable)
  {
    var list = new List<T>();
    await foreach (var item in asyncEnumerable)
    {
      list.Add(item);
    }
    return list;
  }
}

internal static class IEnumerableExtensions
{
  public static T GetRandom<T>(this IEnumerable<T> values)
  {
    var random = new Random();
    var count = values.Count();
    var index = random.Next(count);
    return values.ElementAt(index);
  }
}
