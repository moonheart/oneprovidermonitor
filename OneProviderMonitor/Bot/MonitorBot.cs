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
    private readonly ILogger<MonitorBot> _logger;

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

    public async Task SendPriceChangedNotification(Server newServer, Server oldServer)
    {
        _logger.LogInformation($"Price changed {newServer.Id} {newServer.CpuModel} {oldServer.EurPricePromo} -> {newServer.EurPricePromo}");
        var sb = new StringBuilder();
        sb.Append(newServer.EurPricePromo > oldServer.EurPricePromo ? "📈 *Price Increased*" : "📉 *Price Dropped*")
            .AppendLine(Escape($"€{oldServer.EurPricePromo} -> €{newServer.EurPricePromo}"));
        BuildServerDesc(newServer, sb);

        var msg = sb.ToString();

        var replyMarkup = new InlineKeyboardMarkup(new InlineKeyboardButton("Go Conf") { Url = $"https://oneprovider.com/configure/dediconf/{newServer.Id}" });
        await Task.Delay(50);
        var sentMessage = await _bot.SendTextMessageAsync(
            _monitorOption.Value.TelegramChannel,
            msg,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: replyMarkup);
    }

    public async Task SendNewServerNotification(Server server)
    {
        _logger.LogInformation($"New server {server.Id} {server.CpuModel}");
        var sb = new StringBuilder();
        sb.AppendLine("🆕 *New Server*");
        BuildServerDesc(server, sb);

        var msg = sb.ToString();

        var replyMarkup = new InlineKeyboardMarkup(new InlineKeyboardButton("Go Conf") { Url = $"https://oneprovider.com/configure/dediconf/{server.Id}" });
        await Task.Delay(50);
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
        string bandwidthLimit = server.BandwidthLimit == 0 ? "Unlimited" : (server.BandwidthLimit / 1024.0).ToString("N1") + "TB";
        string bandwidthSpeed = server.BandwidthSpeed >= 1024 ? (server.BandwidthSpeed / 1024.0).ToString("N1") + " Gbps" : server.BandwidthSpeed + " Mbps";
        sb.Append("*Bandwidth*:\t").AppendLine(Escape($"{bandwidthLimit} @ {bandwidthSpeed}"));
        sb.Append("*Storage*:\t").AppendLine(Escape(server.StorageJson));
        sb.Append("*Price*:\t").AppendLine(server.EurPriceNormal == server.EurPricePromo || server.EurPricePromo == 0
            ? $"€{Escape(server.EurPriceNormal.ToString("N2"))}"
            : $"~€{Escape(server.EurPriceNormal.ToString("N2"))}~ €{Escape(server.EurPricePromo.ToString("N2"))}");
        sb.Append("*Setup Fee*:\t").AppendLine(server.EurPriceSetup == server.EurPriceSetupPromo
            ? $"€{Escape(server.EurPriceSetup.ToString("N2"))}"
            : $"~€{Escape(server.EurPriceSetup.ToString("N2"))}~ €{Escape(server.EurPriceSetupPromo.ToString("N2"))}");
    }
}