using Refit;

namespace OneProviderMonitor.Apis;

public interface IOneProviderApi
{
    [Post("/search-page/{page}")]
    Task<List<Commmand>> SearchPageAsync(int page);
}

public class Commmand
{
    public string Command { get; set; }
    public Settings? Settings { get; set; }
    public string Data { get; set; }
}

public class Settings
{
    public bool HasNextPage { get; set; }
}