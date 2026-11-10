# 06 · 依赖注入与 ViewModel 定位

**目标**：读懂 `App.xaml.cs`，理解「ViewModel 由谁创建」「它怎么拿到服务和日志」「应用关闭时谁负责收尾」。

## 1. WPF 的组合根长什么样

```csharp
// App.xaml.cs
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

    builder.Services.AddSingleton<MainViewModel>();       // ← 只多了这两行
    builder.Services.AddSingleton<MainWindow>();          // ←

    _host = builder.Build();
    ... self-test 分支 ...
    _host.Start();
    _host.Services.GetRequiredService<MainWindow>().Show();
}
```

📌 与第一套 `Program.cs` 的差别**只有表现层注册和启动方式**。配置、三个接口、采集服务的注册一行不差。这就是为什么 `diff` 能通过——组合根的差异被限制在"宿主相关"的部分。

## 2. ViewModel 由容器创建，不是 XAML 创建

```csharp
public MainWindow(MainViewModel viewModel)     // 构造函数注入
{
    InitializeComponent();
    DataContext = viewModel;
    ...
}
```

依赖链：

```
MainWindow → MainViewModel → (AcquisitionService, IMeasurementRepository, ILogger<MainViewModel>)
                              ↓
                    (IModbusClient, ICalibrationService, IOptions<AppOptions>, ILogger<...>)
```

容器递归解析整棵树。`MainViewModel` 需要 `ILogger<MainViewModel>`（第 04 章的异常处理用到），也是容器自动填的——无需在 XAML 里写任何东西。

📌 **不要在 XAML 里这样写**：

```xml
<Window.DataContext><vm:MainViewModel /></Window.DataContext>
```

这会让 XAML 用无参构造函数创建 ViewModel，注入的依赖全丢；而且 ViewModel 变成了 View 的"私有实现细节"，无法替换、无法测试。这叫**视图优先（View-First）** 与**模型优先**的取舍；本骨架采用「组合根创建 View，构造函数注入 ViewModel」，是更可控的做法。

📌 **ViewModelLocator 反模式**：早期 MVVM 框架流行用一个静态 `ViewModelLocator` 给 XAML 提供 VM。它本质是服务定位器（第 03 章讲过），把依赖藏起来。现代做法就是构造函数注入 + 组合根解析。

## 3. 生命周期与优雅退出

```csharp
protected override void OnExit(ExitEventArgs e)
{
    if (_host is not null)
    {
        Task.Run(() => _host.StopAsync()).GetAwaiter().GetResult();
        _host.Dispose();
    }
    base.OnExit(e);
}
```

| 事件 | 动作 |
|---|---|
| `OnStartup` | 建 Host、注册、Build、Start、Show 主窗口 |
| 用户关闭窗口 | WPF 默认 `ShutdownMode = OnLastWindowClose`，触发 `OnExit` |
| `OnExit` | StopAsync → 取消采集令牌 → Dispose 单例（关闭模拟设备） |

📌 `Task.Run(...)` 的原因与第一套完全一致：避免在 `Dispatcher` 线程同步等待 `StopAsync` 内部 `await` 引发死锁。两边用了同一行代码，这也是"宿主差异之外全部复用"的又一个例子。

📌 `MainWindow.Closed += (_, _) => _viewModel.Unsubscribe();` 让 ViewModel 及时从采集服务取消订阅；否则单例服务会一直持有已关闭窗口的 ViewModel（第 09 章展开）。

## 4. 与第一套组合根逐行对照

| `Program.cs`（WinForms） | `App.xaml.cs`（WPF） |
|---|---|
| `[STAThread] Main(string[] args)` | `OnStartup(StartupEventArgs e)` |
| `ApplicationConfiguration.Initialize()` | WPF 由 `App.xaml` 初始化 |
| `Host.CreateApplicationBuilder(settings)` | 相同 |
| `AddJsonFile / Configure / AddSingleton...` | 完全相同 |
| `AddTransient<MainForm>()` | `AddSingleton<MainViewModel>()` + `AddSingleton<MainWindow>()` |
| `host.Start(); Application.Run(form);` | `host.Start(); ...MainWindow().Show();` |
| `Task.Run(() => host.StopAsync())` | 相同 |

## 🧪 动手实验

**实验 6.1 · 删掉一个注册**
注释掉 `AddSingleton<MainViewModel>()`，`dotnet build` 仍通过，运行抛「Unable to resolve service for type MainViewModel」。体会 DI 错误发生在**运行时解析**而非编译期。

**实验 6.2 · 改成 Transient**
把 `MainViewModel` 改成 `AddTransient`，运行：界面仍正常（因为只解析一次），但如果有第二个 Window 也注入 VM，就会得到两个互不相干的状态。思考：什么情况下 ViewModel 必须是单例？（提示：跨页面共享状态、订阅后台事件。）

**实验 6.3 · 试试 View-First（反例）**
把 `MainWindow` 构造函数改成无参、在 XAML 里写 `<Window.DataContext><vm:MainViewModel/></Window.DataContext>`。运行，观察编译/运行错误（ViewModel 没有无参构造函数）。这个失败正好说明为什么构造函数注入是必要的。做完还原。

**实验 6.4 · 自检分支里解析 ViewModel**
在 `--self-test` 分支加：

```csharp
var vm = _host.Services.GetRequiredService<MainViewModel>();
// 断言 vm.Channels.Count == 0（此时还没启动采集）
```

运行 `dotnet run -- --self-test`，确认可以在**没有 Window** 的情况下构造 ViewModel。这是 MVVM 可测试性的直接证据。

## 自测

1. 为什么 `MainViewModel` 交给容器创建，而不是 XAML `new`？
2. `OnExit` 里为什么用 `Task.Run` 包住 `StopAsync`？
3. `MainWindow` 与 `MainViewModel` 的生命周期分别由谁管理？

<details><summary>答案要点</summary>

1. 构造函数注入才能拿到服务与日志；XAML 创建会绕过容器、丢失依赖、无法替换与测试。
2. 避免 Dispatcher 线程同步等待时与 `DispatcherSynchronizationContext` 死锁。
3. 都由 DI 容器创建（单例）；窗口的显示/关闭由 WPF `Application` 管理，ViewModel 的订阅解除由窗口 `Closed` 事件转发。

</details>

---

上一章：[05-集合与ObservableCollection](05-集合与ObservableCollection.md) ｜ 下一章：[07-XAML与界面布局](07-XAML与界面布局.md)
