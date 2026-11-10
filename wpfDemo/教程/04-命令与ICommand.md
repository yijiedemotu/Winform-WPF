# 04 · 命令与 `ICommand`

**目标**：理解按钮点击为什么能从「事件处理」变成「绑定一个命令」，以及异步命令里的异常该怎么处理。

## 1. ICommand 是什么

```csharp
public interface ICommand
{
    event EventHandler? CanExecuteChanged;                 // 可用性变化通知
    bool CanExecute(object? parameter);                    // 现在能执行吗
    void Execute(object? parameter);                       // 执行
}
```

WPF 的 `Button`、`MenuItem`、`Hyperlink` 等都有 `Command` 属性。绑定后：

- 按钮调用 `CanExecute` 决定自己是否 `IsEnabled`；
- 点击时调用 `Execute`；
- 命令内部触发 `CanExecuteChanged` 时，按钮重新查询可用性。

📌 好处：**同一个命令可以被多个控件复用**（按钮、菜单、快捷键），且「能不能点」的逻辑和「点了做什么」在一起，不用散落在多个 `Click` 事件里。

## 2. 骨架里的命令

```csharp
// ViewModels/MainViewModel.cs
public IAsyncRelayCommand SaveCommand { get; }
public IAsyncRelayCommand RefreshCommand { get; }

public MainViewModel(AcquisitionService acquisition,
                     IMeasurementRepository repository,
                     ILogger<MainViewModel> logger)
{
    ...
    SaveCommand = new AsyncRelayCommand(SaveAsync);
    RefreshCommand = new AsyncRelayCommand(RefreshAsync);
}

private async Task SaveAsync() { ... }        // 组装 SampleRecord → 仓储 → 刷新历史
private Task RefreshAsync() => LoadHistoryAsync();
```

```xml
<Button Content="保存数据" Command="{Binding SaveCommand}" />
<Button Content="刷新历史" Command="{Binding RefreshCommand}" />
```

📌 这里**手写** `new AsyncRelayCommand(...)` 而不是用 `[RelayCommand]` 源生成器，是为了让你看见 `ICommand` 的本质：它只是一个对象，被绑定到 `Button.Command`。生成器只是把这段样板代码自动化，例如：

```csharp
[RelayCommand]
private async Task SaveAsync() { ... }
// 生成器产出 SaveCommand（会去掉 Async 后缀）
```

📌 `AsyncRelayCommand`（`IAsyncRelayCommand`）比 `RelayCommand` 多了：支持 `Task`、执行中自动 `CanExecute=false`（防重复点击）、可 `Cancel()`、暴露 `ExecutionTask` 便于测试。

## 3. 与 WinForms 事件处理对照

| | 第一套 `MainForm` | 第二套 `MainViewModel` |
|---|---|---|
| 触发 | `saveButton.Click += OnSaveClick;` | `Command="{Binding SaveCommand}"` |
| 处理函数签名 | `async void OnSaveClick(object? sender, EventArgs e)` | `async Task SaveAsync()` |
| 参数来源 | 读控件 `_projectBox.Text` | 读绑定属性 `ProjectName` |
| 禁用按钮 | `saveButton.Enabled = false`（要持有控件） | `CanExecute` 返回 false（不碰控件） |
| 异常处理 | 方法体整体 `try/catch` | 同样需要 `try/catch`（见下） |
| 可测试性 | 必须构造 Form 才能触发 | 直接 `await vm.SaveCommand.ExecuteAsync(null)` |

📌 **async void 消失了**：`AsyncRelayCommand` 包装的是 `Func<Task>`，异常能被捕获、能被测试。这是 MVVM 相对事件驱动的一个实质优势。

## 4. 异步命令的异常边界（重要）

`SaveAsync` 里调用仓储、写文件，都可能抛异常。骨架里使用了 `ILogger<MainViewModel>` 记录并把失败结果显示在状态栏：

```csharp
private async Task SaveAsync()
{
    if (_latest.Count == 0) { Status = "暂无数据可保存"; return; }
    try
    {
        var record = new SampleRecord(DateTime.Now, ProjectName.Trim(),
                                      _latest.Select(x => x.Value).ToArray());
        await _repository.AddAsync(record);
        await LoadHistoryAsync();
        Status = $"已保存 {record.Time:HH:mm:ss}";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "保存检测记录失败");
        Status = "保存失败：" + ex.Message;
    }
}
```

📌 **为什么不能省**：未捕获的异常会沿 `AsyncRelayCommand` 冒泡到 WPF 消息循环，轻则界面状态错乱，重则进程退出。第一套用 `async void` 时同样必须整体 `try/catch`——两处是同一个道理。

📌 注意 `ILogger<MainViewModel>` 也是**构造函数注入**的，与 `AcquisitionService`、`IMeasurementRepository` 一样，来自同一个 DI 容器。

## 🧪 动手实验

**实验 4.1 · 加一个清空历史命令**
在 ViewModel 加：

```csharp
public IRelayCommand ClearHistoryCommand { get; }
// 构造里：ClearHistoryCommand = new RelayCommand(() => History.Clear());
```

XAML 加一个按钮 `Command="{Binding ClearHistoryCommand}"`。运行，确认点击后下方表格清空（注意：这只清界面集合，不动文件——想想为什么，以及要不要加仓储的 `ClearAsync`）。

**实验 4.2 · 用 CanExecute 防止空保存**
把 `SaveCommand` 换成带条件的版本：

```csharp
SaveCommand = new AsyncRelayCommand(
    SaveAsync,
    () => !string.IsNullOrWhiteSpace(ProjectName));
```

再给 `ProjectName` 加 `partial void OnProjectNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();`。运行：清空产品名后按钮自动变灰。这个实验能让你彻底理解 `CanExecuteChanged`。

**实验 4.3 · 在命令里直接抛异常**
在 `SaveAsync` 开头临时 `throw new InvalidOperationException("测试");`，运行并点保存。预期：不崩溃，状态栏显示失败、输出窗口有日志（说明 try/catch 生效）。做完还原。

**实验 4.4 · 不创建窗口测试命令**
在 `--self-test` 分支里解析 `MainViewModel`，直接 `await vm.SaveCommand.ExecuteAsync(null)`，检查 `samples.json` 是否多一条。注意：`MainViewModel` 构造时会捕获 `SynchronizationContext`，在自检分支里可能是普通上下文，但命令本身不依赖控件——这又是一次可测试性的验证。

## 自测

1. `CanExecute` 返回 false 时 WPF 会做什么？谁来触发重新查询？
2. 为什么 `AsyncRelayCommand` 比 `RelayCommand` 更适合保存操作？
3. `[RelayCommand]` 生成器生成 `SaveAsync` 对应的命令名叫什么？为什么骨架没用它？

<details><summary>答案要点</summary>

1. 按钮自动 `IsEnabled=false`；命令需触发 `CanExecuteChanged`（或在属性变化时调 `NotifyCanExecuteChanged()`）。
2. 保存是异步 IO；异步命令能 await、执行中自动禁用防重复点击、异常与执行状态可观测。
3. `SaveCommand`（去掉 Async 后缀）；骨架手写是为了让 `ICommand` 的创建过程可见，避免把生成器当黑盒。

</details>

---

上一章：[03-数据绑定与ObservableObject](03-数据绑定与ObservableObject.md) ｜ 下一章：[05-集合与ObservableCollection](05-集合与ObservableCollection.md)
