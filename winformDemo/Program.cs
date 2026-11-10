using EnterpriseDemo.Application;
using EnterpriseDemo.Core;
using EnterpriseDemo.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WinformDemo.Presentation;

namespace WinformDemo;

/// <summary>
/// 组合根（Composition Root）：整个应用唯一「知道所有具体类型」的地方。
/// 依赖关系只在这里组装，其余各层只依赖接口。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // ContentRoot 固定为程序所在目录，避免「换个工作目录启动就找不到配置」。
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = args
        });
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        // 配置：强类型 Options
        builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));

        // 基础设施：接口 → 实现，替换硬件/数据库只改这里
        builder.Services.AddSingleton<IModbusClient, SimulatedModbusClient>();
        builder.Services.AddSingleton<ICalibrationService, CalibrationService>();
        builder.Services.AddSingleton<IMeasurementRepository, JsonMeasurementRepository>();

        // 应用服务：同时作为单例（供 UI 订阅）与托管服务（由 Host 管理生命周期）
        builder.Services.AddSingleton<AcquisitionService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AcquisitionService>());

        // 表现层
        builder.Services.AddTransient<MainForm>();

        // 自检模式：不开窗口即可验证「配置 → DI → 业务层」是否装配正确。
        // dotnet run -- --self-test
        if (args.Contains("--self-test"))
        {
            using var probe = builder.Build();
            var calibration = probe.Services.GetRequiredService<ICalibrationService>();
            var repository = probe.Services.GetRequiredService<IMeasurementRepository>();
            var acquisition = probe.Services.GetRequiredService<AcquisitionService>();
            string report =
                $"OK | channels={calibration.ChannelCount} | repo={repository.GetType().Name} " +
                $"| service={acquisition.GetType().Name} | 通道1 raw=2700 -> {calibration.Convert(0, 2700):F2} mm";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest.txt"), report);
            return;
        }

        using var host = builder.Build();
        var form = host.Services.GetRequiredService<MainForm>();

        host.Start();                       // 启动后台采集服务
        Application.Run(form);              // 进入 WinForms 消息循环
        // 放到线程池执行 StopAsync：避免在 UI 线程同步等待时与
        // WindowsFormsSynchronizationContext 形成死锁。
        Task.Run(() => host.StopAsync()).GetAwaiter().GetResult();
    }
}
