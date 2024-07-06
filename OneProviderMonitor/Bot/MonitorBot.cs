using System.Text;
using AutoCtor;
using Injectio.Attributes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using OneProviderMonitor.Models;
using OneProviderMonitor.Options;
using Telegram.Bot;
using Telegram.Bot.Extensions.Markup;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OneProviderMonitor.Bot;

[RegisterSingleton]
[AutoConstruct]
public partial class MonitorBot
{
    private readonly ITelegramBotClient _bot;
    private readonly IOptions<MonitorOption> _monitorOption;
    private readonly ILogger<MonitorOption> _logger;

    [AutoPostConstruct]
    private void Initialize()
    {
        try
        {
            // await _bot.SendTextMessageAsync(_monitorOption.Value.TelegramChannel, "Bot started");
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }
    }

    private static string Escape(string msg)
    {
        return Tools.EscapeMarkdown(msg, ParseMode.MarkdownV2);
    }


    private string FormatTime(long time)
    {
        var timeSpan = TimeSpan.FromSeconds(time);
        return Escape(new TimeSpan(timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds).ToString());
    }

    private string FormatStorage(int sizeInGb)
    {
        return sizeInGb >= 1024 ? $"{sizeInGb / 1024}TB" : $"{sizeInGb}GB";
    }
    
    private string FormatRam(int sizeInGb)
    {
        return sizeInGb >= 1024 ? $"{sizeInGb / 1024}GB" : $"{sizeInGb}MB";
    }
    
    public async Task SendPriceChangedNotification(Server server, Server oldServer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*Price Changed*");
        sb.Append("*Old Price*").AppendLine(Escape($"${oldServer.UsdPricePromo}"));
        BuildServerDesc(server, sb);

        var msg = sb.ToString();

        var replyMarkup = new InlineKeyboardMarkup(new InlineKeyboardButton("Go Conf") { Url = $"https://oneprovider.com/configure/dediconf/{server.Id}" });
        // var chat = await _bot.GetChatAsync(_monitorOption.Value.TelegramChannel);
        var sentMessage = await _bot.SendTextMessageAsync(
            _monitorOption.Value.TelegramChannel,
            msg,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: replyMarkup);
    }

    public async Task SendNewServerNotification(Server server)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*New Server*");
        BuildServerDesc(server, sb);

        var msg = sb.ToString();

        var replyMarkup = new InlineKeyboardMarkup(new InlineKeyboardButton("Go Conf") { Url = $"https://oneprovider.com/configure/dediconf/{server.Id}" });
        // var chat = await _bot.GetChatAsync(_monitorOption.Value.TelegramChannel);
        var sentMessage = await _bot.SendTextMessageAsync(
            _monitorOption.Value.TelegramChannel,
            msg,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: replyMarkup);
    }

    private void BuildServerDesc(Server server, StringBuilder sb)
    {
        sb.Append("*Location*:\t").AppendLine(Escape($"{server.LocationName} {server.LocationCode}"));
        sb.Append("*CPU*:\t").AppendLine(Escape($"{server.CpuMaker} {server.CpuModel} {server.CpuSpeed}GHz {server.CpuCore}c/{server.CpuThread}t"));
        sb.Append("*RAM*:\t").AppendLine(Escape($"{FormatRam(server.RamAmount)}"));
        string bandwidthLimit = server.BandwidthLimit == 0 ? "Unlimited" : (server.BandwidthLimit / 1024.0).ToString("N1");
        string bandwidthSpeed = server.BandwidthSpeed >= 1024 ? (server.BandwidthSpeed / 1024.0).ToString("N1") + " Gbps" : server.BandwidthSpeed + " Mbps";
        sb.Append("*Bandwidth*:\t").AppendLine(Escape($"{bandwidthLimit}TB @ {bandwidthSpeed}"));
        sb.Append("*SSD*:\t").AppendLine(Escape(server.StorageSsdMinAmount == server.StorageSsdMaxAmount
            ? $"{Escape(FormatStorage(server.StorageSsdMinAmount))}"
            : $"{FormatStorage(server.StorageSsdMinAmount)}~{FormatStorage(server.StorageSsdMaxAmount)}"));
        sb.Append("*HDD*:\t").AppendLine(Escape(server.StorageHddMinAmount == server.StorageHddMaxAmount
            ? $"{Escape(FormatStorage(server.StorageHddMinAmount))}"
            : $"{FormatStorage(server.StorageHddMinAmount)}~{FormatStorage(server.StorageHddMaxAmount)}"));
        sb.Append("*Price*:\t").AppendLine(server.UsdPriceNormal == server.UsdPricePromo
            ? $"${Escape(server.UsdPriceNormal.ToString("N2"))}"
            : $"~${Escape(server.UsdPriceNormal.ToString("N2"))}~ ${Escape(server.UsdPricePromo.ToString("N2"))}");
        sb.Append("*Setup Fee*:\t").AppendLine(server.UsdPriceSetup == server.UsdPriceSetupPromo
            ? $"${Escape(server.UsdPriceSetup.ToString("N2"))}"
            : $"~${Escape(server.UsdPriceSetup.ToString("N2"))}~ ${Escape(server.UsdPriceSetupPromo.ToString("N2"))}");
    }
}