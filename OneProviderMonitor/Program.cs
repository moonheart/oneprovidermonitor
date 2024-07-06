using System.Diagnostics;
using Microsoft.Extensions.Options;
using OneProviderMonitor;
using OneProviderMonitor.Apis;
using OneProviderMonitor.Bot;
using OneProviderMonitor.Options;
using Quartz;
using Refit;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<MonitorOption>(builder.Configuration.GetSection(MonitorOption.Position));
builder.Services.AddQuartz(q =>
{
    var monitorOption = builder.Configuration.GetSection("Monitor").Get<MonitorOption>();
    if (monitorOption == null)
    {
        throw new Exception("MonitorOption is null");
    }

    if (Debugger.IsAttached)
    {
        monitorOption.MonitorCron = "* * * * * ? *";
    }
    q.ScheduleJob<MonitorJob>(configurator => configurator.WithCronSchedule(monitorOption.MonitorCron));
});
Func<IServiceProvider, IFreeSql> fsqlFactory = r =>
{
    IFreeSql fsql = new FreeSql.FreeSqlBuilder()
        .UseConnectionString(FreeSql.DataType.Sqlite, r.GetRequiredService<IOptions<MonitorOption>>().Value.SqliteDb)
        // .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}"))
        .UseAutoSyncStructure(true)
        .Build();
    return fsql;
};
builder.Services.AddSingleton(fsqlFactory);
builder.Services.AddSingleton<IOneProviderApi>(sp =>
    RestService.For<IOneProviderApi>(sp.GetRequiredService<IOptions<MonitorOption>>().Value.OneProviderUrl));
builder.Services.AddQuartzHostedService(opt => { opt.WaitForJobsToComplete = true; });
builder.Services.AddHttpClient();
builder.Services.AddTelegramBot();
builder.Services.AddOneProviderMonitor();

var host = builder.Build();
host.Run();