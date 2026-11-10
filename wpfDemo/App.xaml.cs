using System.Windows;
using EnterpriseDemo.Application;
using EnterpriseDemo.Core;
using EnterpriseDemo.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WpfDemo.ViewModels;
using WpfDemo.Views;

namespace WpfDemo;

/// <summary>
/// WPF 的组合根。请与 winformDemo/Program.cs 对照阅读：
/// 除了「最后启动的是 Window 而不是 Form」以及多注册了 ViewModel，
/// 配置、接口注册、采集服务的装配完全一样。
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = e.Args
        });
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
        builder.Services.AddSingleton<IModbusClient, SimulatedModbusClient>();
        builder.Services.AddSingleton<ICalibrationService, CalibrationService>();
        builder.Services.AddSingleton<IMeasurementRepository, JsonMeasurementRepository>();
        builder.Services.AddSingleton<AcquisitionService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AcquisitionService>());

        // 表现层：ViewModel 与 Window 都交给容器，构造函数注入由容器完成
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        if (e.Args.Contains("--self-test"))
        {
            var calibration = _host.Services.GetRequiredService<ICalibrationService>();
            var repository = _host.Services.GetRequiredService<IMeasurementRepository>();
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "selftest.txt"),
                $"OK | channels={calibration.ChannelCount} | repo={repository.GetType().Name} " +
                $"| 通道1 raw=2700 -> {calibration.Convert(0, 2700):F2} mm");
            _host.Dispose();
            _host = null;
            Shutdown();
            return;
        }

        _host.Start();
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            // 用线程池执行 StopAsync，避免在 Dispatcher 线程同步等待造成死锁。
            Task.Run(() => _host.StopAsync()).GetAwaiter().GetResult();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
