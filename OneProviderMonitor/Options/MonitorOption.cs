namespace OneProviderMonitor.Options;

public class MonitorOption
{
    public const string Position = "Monitor";
    
    public string OneProviderUrl { get; set; } = "https://oneprovider.com";
    public string MonitorCron { get; set; } = "0 0/5 * * * ? *";
    public string SqliteDb { get; set; } = "Filename=oneprovider.db";
    public string TelegramBotToken { get; set; } = "";
    public string TelegramChannel { get; set; } = "";
}