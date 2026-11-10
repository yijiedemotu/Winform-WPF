# winformDemo · WinForms 企业级分层骨架（第一套）

八通道尺寸测量上位机的最小完整骨架：**组合根 + 依赖注入 + 接口倒置 + 后台服务 + 事件封送 + 仓储抽象**。
独立项目，不依赖同仓库的 `wpfDemo`，也不需要任何硬件。

## 跑起来

```bash
cd winformDemo
dotnet restore
dotnet build
dotnet run -- --self-test     # 不弹窗口，验证配置 / DI / 业务层装配
dotnet run                    # 打开窗口
```

自检预期输出（写在 `bin/Debug/net8.0-windows/selftest.txt`）：

```
OK | channels=8 | repo=JsonMeasurementRepository | service=AcquisitionService | 通道1 raw=2700 -> 25.00 mm
```

## 目录

```
winformDemo/
├── WinformDemo.csproj
├── appsettings.json            八通道标定参数、采样周期、存储路径
├── Program.cs                  ⭐ 组合根：唯一「知道所有具体类型」的地方
├── Core/                       领域层（不引用任何 UI / 第三方框架）
│   ├── Models.cs               ChannelConfig / AppOptions / ChannelReading / SampleRecord
│   └── Abstractions.cs         IModbusClient / ICalibrationService / IMeasurementRepository
├── Infrastructure/             接口的可替换实现
│   ├── SimulatedModbusClient.cs
│   ├── CalibrationService.cs
│   └── JsonMeasurementRepository.cs
├── Application/
│   └── AcquisitionService.cs   BackgroundService：定时采集、换算、判定、发布事件
├── Presentation/
│   └── MainForm.cs             只画界面：BindingList + SynchronizationContext
└── 教程/                       11 篇分章节教学
```

依赖方向：`Presentation → Application → Core`、`Infrastructure → Core`。全局搜索 `using System.Windows.Forms`，只应命中 `Program.cs` 与 `Presentation/`。

## 教程（按顺序读）

| 章 | 主题 | 章 | 主题 |
|---|---|---|---|
| [00 学习路线](教程/00-学习路线.md) | 全局地图 | [06 后台服务与取消令牌](教程/06-后台服务与取消令牌.md) | `BackgroundService` / `PeriodicTimer` |
| [01 先跑起来](教程/01-先跑起来.md) | build、自检、数据从哪来 | [07 事件与跨线程 UI](教程/07-事件与跨线程UI.md) | `SynchronizationContext.Post` |
| [02 为什么需要分层](教程/02-为什么需要分层.md) | 依赖方向图 | [08 仓储与数据模型](教程/08-仓储与数据模型.md) | 不可变 `record` / 仓储抽象 |
| [03 组合根与依赖注入](教程/03-组合根与依赖注入.md) | 读懂 `Program.cs` | [09 表现层与数据绑定](教程/09-表现层与数据绑定.md) | `BindingList` / `DisplayName` |
| [04 接口与依赖倒置](教程/04-接口与依赖倒置.md) | 新增实现只改一行 | [10 从骨架到真实设备](教程/10-从骨架到真实设备.md) | 换硬件 / 换数据库 / 加测试 |
| [05 配置与日志](教程/05-配置与日志.md) | Options / `ILogger<T>` | | |

每章都有「概念 → 读代码 → 🧪 动手实验 → 自测 → 对照」，章末附答案要点。

## 注意

- 改 `appsettings.json` 后**重启程序**生效（`reloadOnChange: false`），无需重新编译。
- `ChannelCount` 必须与 `Channels` 数量一致；参数非法时 `CalibrationService` 会在启动时抛错。
- 业务层不碰控件，界面刷新必须经过 UI 线程封送。
- `samples.json` / `selftest.txt` 是运行时产物，已被 `.gitignore` 排除。

学完这套，去 [第二套 wpfDemo](../wpfDemo/README.md)：用 `diff` 亲自验证业务层一行都没改。
