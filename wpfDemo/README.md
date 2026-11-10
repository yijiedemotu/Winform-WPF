# wpfDemo · WPF / MVVM 企业级分层骨架（第二套）

与第一套 `winformDemo` **同一个业务、同一套业务层代码**，只把表现层换成 WPF + MVVM。
独立项目，不依赖同仓库的 `winformDemo`，不需要任何硬件。

> 前置：建议先学完 [第一套教程](../winformDemo/README.md)，本套默认你已理解分层、组合根、依赖注入、后台服务与事件封送。

## 跑起来

```bash
cd wpfDemo
dotnet build
dotnet run -- --self-test     # 不弹窗口，验证装配
dotnet run                    # 打开窗口
```

自检预期：`OK | channels=8 | repo=JsonMeasurementRepository | 通道1 raw=2700 -> 25.00 mm`。

## 先做这个实验

```bash
cd Winform-WPF
for f in Core/Models.cs Core/Abstractions.cs \
         Infrastructure/SimulatedModbusClient.cs \
         Infrastructure/CalibrationService.cs \
         Infrastructure/JsonMeasurementRepository.cs \
         Application/AcquisitionService.cs appsettings.json; do
    diff "winformDemo/$f" "wpfDemo/$f" && echo "相同: $f"
done
```

七行全部输出「相同」——领域层、基础设施层、应用层、配置逐字节一致。不同的只有组合根与表现层：

| 位置 | WinForms | WPF |
|---|---|---|
| 组合根 | `Program.cs` | `App.xaml.cs`（`OnStartup`） |
| 表现层 | `Presentation/MainForm.cs` | `Views/MainWindow.xaml(.cs)` + `ViewModels/MainViewModel.cs` |
| 项目文件 | `UseWindowsForms=true` | `UseWPF=true` + `CommunityToolkit.Mvvm` |

## 目录

```
wpfDemo/
├── WpfDemo.csproj
├── appsettings.json            ← 与 winformDemo 逐字节相同
├── App.xaml / App.xaml.cs      ⭐ 组合根 + 应用生命周期
├── Core/                       ← 逐字节相同
├── Infrastructure/             ← 逐字节相同
├── Application/                ← 逐字节相同
├── ViewModels/
│   └── MainViewModel.cs        ⭐ 状态 + 命令 + 封送
├── Views/
│   ├── MainWindow.xaml         视图（纯声明）
│   └── MainWindow.xaml.cs      code-behind 只有几行
└── 教程/                       11 篇分章节教学
```

## 教程（按顺序读）

| 章 | 主题 | 章 | 主题 |
|---|---|---|---|
| [00 学习路线](教程/00-学习路线.md) | 全局地图与概念映射 | [06 依赖注入与 ViewModel 定位](教程/06-依赖注入与ViewModel定位.md) | 组合根 / 构造函数注入 |
| [01 先跑起来](教程/01-先跑起来.md) | build、自检、数据流 | [07 XAML 与界面布局](教程/07-XAML与界面布局.md) | Grid / DataGrid / 样式 |
| [02 MVVM 三角](教程/02-MVVM三角.md) | 职责边界 | [08 业务层零改动 ⭐](教程/08-业务层零改动.md) | 用 diff 证明分层价值 |
| [03 数据绑定与 ObservableObject](教程/03-数据绑定与ObservableObject.md) | `INotifyPropertyChanged` | [09 跨线程与 Dispatcher](教程/09-跨线程与Dispatcher.md) | 同步上下文 / 取消订阅 |
| [04 命令与 ICommand](教程/04-命令与ICommand.md) | `AsyncRelayCommand` / CanExecute | [10 进阶路线与总复盘](教程/10-进阶路线与总复盘.md) | 导航 / 测试 / Prism |
| [05 集合与 ObservableCollection](教程/05-集合与ObservableCollection.md) | 集合通知 / UI 线程约束 | | |

口诀：**View 只负责长什么样，ViewModel 只负责状态和命令，Model / Service 负责业务。**

## 注意

- `App.xaml` 没有 `StartupUri`：窗口由 `OnStartup` 从 DI 容器取，别在设计器里找不到 DataContext 就以为绑定错了。
- `ObservableCollection` 只能在 UI 线程修改；后台事件通过同步上下文封送。
- 关窗时 `OnExit` 用线程池执行 `host.StopAsync()`，避免 Dispatcher 线程同步等待死锁。
- 改 `appsettings.json` 后重启程序生效；`samples.json` / `selftest.txt` 不进仓库。
