# Winform-WPF · 两套独立的上位机分层架构教学

同一个业务（**八通道尺寸测量**），两套**完全独立**的 .NET 8 桌面项目：一套 WinForms，一套 WPF / MVVM。
它们共用**逐字节相同**的领域层、基础设施层、应用层与配置文件，只有表现层不同——这套仓库就是用来亲眼验证「换 UI 框架，业务层一行都不用改」的。

> 学习顺序建议：先学完 `winformDemo/教程/00` → `10`，再进入 `wpfDemo/教程/00` → `10`。两套都是「一章一个知识点 + 动手实验 + 自测」。

## 快速开始

环境：Windows + .NET 8 SDK（本机用 .NET 10 SDK 编译 `net8.0-windows` 已验证）或 Visual Studio 2022/2026（勾选「.NET 桌面开发」）。**不需要串口/PLC**，`SimulatedModbusClient` 用正弦 + 噪声产生 8 路原始数据。

```bash
git clone https://github.com/yijiedemotu/Winform-WPF.git
cd Winform-WPF

# 第一套
cd winformDemo
dotnet restore
dotnet build
dotnet run -- --self-test     # 不弹窗口，验证配置 / DI / 业务层装配
dotnet run                    # 打开窗口，8 通道每 500 ms 刷新

# 第二套
cd ../wpfDemo
dotnet build
dotnet run -- --self-test
dotnet run
```

`--self-test` 会把结果写到 `bin/Debug/net8.0-windows/selftest.txt`，预期：

```
OK | channels=8 | repo=JsonMeasurementRepository | service=AcquisitionService | 通道1 raw=2700 -> 25.00 mm
```

`25.00 mm` 来自两点标定 `20 + (2700-2200) × (30-20)/(3200-2200)`，与真实项目通道 1 的参数一致。

## 仓库结构

```
Winform-WPF/
├── README.md               本文件：总览与导航
├── .gitignore              排除 bin/obj、运行时生成的 samples.json / selftest.txt
├── winformDemo/            第一套：WinForms 企业级分层骨架（独立项目，可单独打开）
│   ├── README.md           项目索引
│   ├── WinformDemo.csproj
│   ├── Program.cs          ⭐ 组合根：唯一知道所有具体类型的地方
│   ├── Core/               领域层：模型与接口，不依赖任何框架
│   ├── Infrastructure/     设备模拟 / 标定 / JSON 仓储
│   ├── Application/        后台采集服务（BackgroundService）
│   ├── Presentation/       MainForm：只画界面
│   ├── appsettings.json    强类型配置
│   └── 教程/               11 篇
└── wpfDemo/                第二套：WPF / MVVM 骨架（独立项目，可单独打开）
    ├── README.md           项目索引
    ├── App.xaml.cs         ⭐ 组合根（OnStartup）
    ├── Core/               与第一套逐字节相同
    ├── Infrastructure/     与第一套逐字节相同
    ├── Application/        与第一套逐字节相同
    ├── ViewModels/         MainViewModel：状态 + 命令 + 封送
    ├── Views/              MainWindow.xaml
    ├── appsettings.json    与第一套逐字节相同
    └── 教程/               11 篇
```

依赖方向永远是单向的：`Presentation/Views → Application → Core`、`Infrastructure → Core`，`Core` 谁都不依赖。

## 两套教程

| 章 | WinForms 第一套 | WPF 第二套 |
|---|---|---|
| 00 | [学习路线](winformDemo/教程/00-学习路线.md) | [学习路线](wpfDemo/教程/00-学习路线.md) |
| 01 | [先跑起来](winformDemo/教程/01-先跑起来.md) | [先跑起来](wpfDemo/教程/01-先跑起来.md) |
| 02 | [为什么需要分层](winformDemo/教程/02-为什么需要分层.md) | [MVVM 三角](wpfDemo/教程/02-MVVM三角.md) |
| 03 | [组合根与依赖注入](winformDemo/教程/03-组合根与依赖注入.md) | [数据绑定与 ObservableObject](wpfDemo/教程/03-数据绑定与ObservableObject.md) |
| 04 | [接口与依赖倒置](winformDemo/教程/04-接口与依赖倒置.md) | [命令与 ICommand](wpfDemo/教程/04-命令与ICommand.md) |
| 05 | [配置与日志](winformDemo/教程/05-配置与日志.md) | [集合与 ObservableCollection](wpfDemo/教程/05-集合与ObservableCollection.md) |
| 06 | [后台服务与取消令牌](winformDemo/教程/06-后台服务与取消令牌.md) | [依赖注入与 ViewModel 定位](wpfDemo/教程/06-依赖注入与ViewModel定位.md) |
| 07 | [事件与跨线程 UI](winformDemo/教程/07-事件与跨线程UI.md) | [XAML 与界面布局](wpfDemo/教程/07-XAML与界面布局.md) |
| 08 | [仓储与数据模型](winformDemo/教程/08-仓储与数据模型.md) | [业务层零改动](wpfDemo/教程/08-业务层零改动.md) |
| 09 | [表现层与数据绑定](winformDemo/教程/09-表现层与数据绑定.md) | [跨线程与 Dispatcher](wpfDemo/教程/09-跨线程与Dispatcher.md) |
| 10 | [从骨架到真实设备](winformDemo/教程/10-从骨架到真实设备.md) | [进阶路线与总复盘](wpfDemo/教程/10-进阶路线与总复盘.md) |

每章固定结构：**目标 → 概念 → 读代码（具体文件与片段）→ 🧪 动手实验 → 自测（章末答案）→ 对照**。

## 用一行命令验证「业务层零改动」

```bash
for f in Core/Models.cs Core/Abstractions.cs \
         Infrastructure/SimulatedModbusClient.cs \
         Infrastructure/CalibrationService.cs \
         Infrastructure/JsonMeasurementRepository.cs \
         Application/AcquisitionService.cs appsettings.json; do
    diff "winformDemo/$f" "wpfDemo/$f" && echo "相同: $f"
done
```

七行全部输出「相同」。领域模型、接口、设备模拟、标定算法、仓储、后台采集服务、配置全部逐字节一致；不同的只有组合根（`Program.cs` / `App.xaml.cs`）和表现层。

## 完整性检查记录

本次推送前在 Windows + .NET SDK 10.0.402 上逐项验证：

| 检查项 | 结果 |
|---|---|
| `winformDemo` 编译 | 0 错误 0 警告 |
| `wpfDemo` 编译 | 0 错误 0 警告 |
| 两套 `--self-test` | 均通过，`OK \| channels=8 \| ... \| 通道1 raw=2700 -> 25.00 mm` |
| 7 个共享业务文件 | 逐字节一致（忽略 CRLF/LF 后 0 差异） |
| 教程文档 | 22 篇，41 个内部链接全部有效，无断链 |
| 运行时产物隔离 | `.gitignore` 已排除 `bin/`、`obj/`、`samples.json`、`selftest.txt` |
| 本地残留 | 已清理混入的旧版依赖 `HostCommon` 的文件，两个项目各自独立可编译 |

## 注意事项

1. **改 `appsettings.json` 后要重启程序**：`reloadOnChange: false`，配置在启动时读取；但不需要重新编译（配置文件会复制到输出目录）。
2. **`ChannelCount` 必须等于 `Channels` 数组长度**，且每个通道 `RawHigh > RawLow`；`CalibrationService` 会在启动时主动抛错，避免运行时算出 `NaN`/`Infinity`。
3. **后台线程不能直接碰控件**：采集服务只发布 `BatchArrived` 事件；WinForms 用 `SynchronizationContext.Post`，WPF 用 Dispatcher 对应的同步上下文封送。
4. **关窗要优雅停止**：两个组合根都用 `Task.Run(() => host.StopAsync()).GetAwaiter().GetResult()`，避免在 UI/Dispatcher 线程同步等待造成死锁。
5. **接真实设备**：实现一个新的 `IModbusClient`（例如基于 NModbus 的 RTU/TCP 客户端），只在组合根改一行注册即可；业务层与教程第 10 章有完整清单。
6. **仓库目录名是小写开头** `winformDemo` / `wpfDemo`；Windows 文件系统不区分大小写，但 Linux / GitHub 区分，命令请照抄。
7. `samples.json`（保存的检测记录）与 `selftest.txt` 是运行时生成文件，不会进仓库。

## 一句话总结

> 表现层换框架，业务层不动一行；用 `--self-test` 和 `diff` 把这句话变成可验证的事实。
