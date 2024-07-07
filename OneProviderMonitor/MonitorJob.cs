using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using AutoCtor;
using OneProviderMonitor.Apis;
using OneProviderMonitor.Bot;
using OneProviderMonitor.Models;
using Quartz;
using JsonDocument = System.Text.Json.JsonDocument;

namespace OneProviderMonitor;

[AutoConstruct]
[DisallowConcurrentExecution]
public partial class MonitorJob : IJob
{
    private readonly IOneProviderApi _oneProviderApi;
    private readonly IFreeSql _freeSql;
    private readonly MonitorBot _monitorBot;
    private readonly ILogger<MonitorJob> _logger;

    public async Task Execute(IJobExecutionContext context)
    {
        int page = 0;
        while (true)
        {
            var commmands = await _oneProviderApi.SearchPageAsync(page);
            var hasNextPage = commmands.Any(c => c.Settings?.HasNextPage ?? false);
            var data = commmands.FirstOrDefault(c => c.Command == "insert")?.Data;
            List<Server> servers = [];
            if (data != null)
            {
                servers.AddRange(ParseData(data));
            }

            foreach (var server in servers.Where(d => d.IsDedicated))
            {
                var existServer = await _freeSql.Select<Server>().Where(d => d.Id == server.Id).FirstAsync();
                if (existServer == null)
                {
                    await _monitorBot.SendNewServerNotification(server);
                    continue;
                }

                if (existServer.EurPriceNormal != server.EurPricePromo)
                {
                    await _monitorBot.SendPriceChangedNotification(server, existServer);
                }
            }

            var i = await _freeSql.InsertOrUpdate<Server>().SetSource(servers).ExecuteAffrowsAsync();
            _logger.LogInformation($"Processed page {page} with {i} servers");

            if (!hasNextPage || data == null)
            {
                break;
            }

            page++;
        }
    }

    private List<Server> ParseData(string data)
    {
        var servers = new List<Server>();

        var htmlParser = new HtmlParser();
        var htmlDocument = htmlParser.ParseDocument(data);
        var trs = htmlDocument.QuerySelectorAll("div.results-tr");
        foreach (var tr in trs)
        {
            var server = new Server();
            server.Id = tr.GetAttribute("data-pid").ToInteger(0);
            server.IsDedicated = tr.ClassList.Contains("dedicated-server");
            var analyticsJson = tr.QuerySelector("div.conf-data")?.GetAttribute("data-analytics");
            if (analyticsJson != null)
            {
                var jsonElement = JsonDocument.Parse(analyticsJson).RootElement;
                server.CpuMaker = jsonElement.GetProperty("cpu").GetProperty("maker").ToString();
                server.CpuModel = jsonElement.GetProperty("cpu").GetProperty("model").ToString();
                server.LocationCode = jsonElement.GetProperty("location").GetProperty("code").ToString();
                server.LocationName = jsonElement.GetProperty("location").GetProperty("name").ToString();
                server.RamAmount = jsonElement.GetProperty("ram").GetInt32();
            }

            server.CpuSpeed = tr.QuerySelector("span.field-cpu-freq")?.Text().Replace("GHz", "").ToDouble() ?? 0;
            var cpuCoreMatch = Regex.Match(tr.QuerySelector("span.field-cpu-core")?.Text() ?? "", @"(\d+)c/(\d+)t");
            if (cpuCoreMatch.Success)
            {
                server.CpuCore = cpuCoreMatch.Groups[1].Value.ToInteger(0);
                server.CpuThread = cpuCoreMatch.Groups[2].Value.ToInteger(0);
            }

            server.RamType = tr.QuerySelector("div.field--ram-type")?.Text() ?? "";
            int? minStorageSsdAmount = null;
            int? minStorageHddAmount = null;
            int? maxStorageSsdAmount = null;
            int? maxStorageHddAmount = null;
            // 2× 480 GB (SSD SATA) and 10× 14 TB (HDD SATA)
            var drivesTextOr = tr.QuerySelector("div.field-item-list--drives")?.Text().Trim();
            foreach (var drivesTextAnd in drivesTextOr?.Split("or", StringSplitOptions.RemoveEmptyEntries) ?? [])
            {
                var ssdAmount = 0;
                var hddAmount = 0;
                foreach (var driveText in drivesTextAnd.Split("and", StringSplitOptions.RemoveEmptyEntries))
                {
                    var match = Regex.Match(driveText.Replace("\n", ""), @"(?<count>\d+)×\s+(?<size>\d+)\s+(?<unit>TB|GB)\s+\((?<type>.+)\)");
                    if (match.Success)
                    {
                        var count = match.Groups["count"].Value.ToInteger(0);
                        var size = match.Groups["size"].Value.ToInteger(0);
                        var unit = match.Groups["unit"].Value;
                        if (unit == "TB")
                        {
                            size *= 1024;
                        }

                        var type = match.Groups["type"].Value;
                        if (type.Contains("SSD"))
                        {
                            ssdAmount += size * count;
                        }
                        else if (type.Contains("HDD"))
                        {
                            hddAmount += size * count;
                        }
                    }
                }

                minStorageSsdAmount = minStorageSsdAmount == null ? ssdAmount : Math.Min(minStorageSsdAmount.Value, ssdAmount);
                minStorageHddAmount = minStorageHddAmount == null ? hddAmount : Math.Min(minStorageHddAmount.Value, hddAmount);
                maxStorageSsdAmount = maxStorageSsdAmount == null ? ssdAmount : Math.Max(maxStorageSsdAmount.Value, ssdAmount);
                maxStorageHddAmount = maxStorageHddAmount == null ? hddAmount : Math.Max(maxStorageHddAmount.Value, hddAmount);
            }

            server.StorageSsdMinAmount = minStorageSsdAmount ?? 0;
            server.StorageHddMinAmount = minStorageHddAmount ?? 0;
            server.StorageSsdMaxAmount = maxStorageSsdAmount ?? 0;
            server.StorageHddMaxAmount = maxStorageHddAmount ?? 0;

            var bandwidth = tr.QuerySelector("div.res-bandwidth");
            if (bandwidth != null)
            {
                server.BandwidthLimit = bandwidth.GetAttribute("data-bw-limit").ToInteger(0);
                var digits = bandwidth.QuerySelector("div.field--bw-speed span.digits")?.Text().ToInteger(0) ?? 0;
                if (bandwidth.QuerySelector("div.field--bw-speed span.unit")?.Text() == "Gbps")
                {
                    digits *= 1024;
                }

                server.BandwidthSpeed = digits;
                var match = Regex.Match(bandwidth.QuerySelector("div.field--bw-guaranteed")?.Text() ?? "", @"(?<size>\d+) (?<unit>Mbps|Gbps) Guaranteed");
                if (match.Success)
                {
                    server.BandwidthGuaranteed = true;
                    server.BandwidthGuaranteedSpeed = match.Groups["size"].Value.ToInteger(0);
                    if (match.Groups["unit"].Value == "Gbps")
                    {
                        server.BandwidthGuaranteedSpeed *= 1024;
                    }
                }
            }

            server.UsdPriceNormal = (decimal)(tr.QuerySelector("div.currency-code-usd span.price-normal")?.Text().Trim().TrimStart('$', '\u20ac').ToDouble() ?? 0);
            server.UsdPricePromo = (decimal)(tr.QuerySelector("div.currency-code-usd span.price-new-amount")?.Text().Trim().TrimStart('$', '\u20ac').ToDouble() ?? 0);
            server.CadPriceNormal = (decimal)(tr.QuerySelector("div.currency-code-cad span.price-normal")?.Text().Trim().TrimStart('$', '\u20ac').ToDouble() ?? 0);
            server.CadPricePromo = (decimal)(tr.QuerySelector("div.currency-code-cad span.price-new-amount")?.Text().Trim().TrimStart('$', '\u20ac').ToDouble() ?? 0);
            server.EurPriceNormal = (decimal)(tr.QuerySelector("div.currency-code-eur span.price-normal")?.Text().Trim().TrimStart('$', '\u20ac').ToDouble() ?? 0);
            server.EurPricePromo = (decimal)(tr.QuerySelector("div.currency-code-eur span.price-new-amount")?.Text().Trim().TrimStart('$', '\u20ac').ToDouble() ?? 0);
            server.IsPromo = server.UsdPricePromo > 0 || server.CadPricePromo > 0 || server.EurPricePromo > 0;

            if (server.CadPriceNormal == 0 || server.EurPriceNormal == 0 || server.UsdPriceNormal == 0)
            {
            }

            server.LimitedStock = tr.QuerySelector("a.res-tooltip")?.Text() == "Limited Quantity";

            servers.Add(server);
        }

        return servers;
    }
}