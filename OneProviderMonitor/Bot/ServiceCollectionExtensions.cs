using Microsoft.Extensions.Options;
using OneProviderMonitor.Options;
using Telegram.Bot;

namespace OneProviderMonitor.Bot;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramBot(this IServiceCollection services)
    {
        services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var option = provider.GetRequiredService<IOptions<MonitorOption>>().Value;
            return new TelegramBotClient(option.TelegramBotToken);
        });
        return services;
    }
}