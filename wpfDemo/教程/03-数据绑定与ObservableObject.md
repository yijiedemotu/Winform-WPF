# 03 · 数据绑定与 `ObservableObject`

**目标**：理解 `{Binding}` 背后的 `INotifyPropertyChanged`，知道源生成器替你写了什么。

## 1. 手工版本：INotifyPropertyChanged

WPF 绑定的核心契约只有一个接口：

```csharp
public interface INotifyPropertyChanged
{
    event PropertyChangedEventHandler? PropertyChanged;
}
```

不用任何库时，一个可绑定属性要写成这样：

```csharp
private string _status = "";
public string Status
{
    get => _status;
    set
    {
        if (_status == value) return;
        _status = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
    }
}
```

**绑定引擎订阅 `PropertyChanged`**：属性变化时它收到通知，重新读取值并刷新目标控件。这就是 `Status` 文字会自动更新的全部秘密。

对照第一套：那边是 `_status.Text = "..."` 手工赋值；`BindingList.ResetBindings()` 通知整表重读。WPF 是**属性级、声明式、自动**。

## 2. 源生成器版本：`[ObservableProperty]`

```csharp
// ViewModels/MainViewModel.cs
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _projectName = "DEMO-001";
    [ObservableProperty] private string _status = "采集中…";
}
```

`CommunityToolkit.Mvvm` 的源生成器在编译时自动生成：

- `public string ProjectName { get; set; }`（带 `SetProperty` 与通知）
- `public string Status { get; set; }`
- 以及 `OnProjectNameChanged` / `OnStatusChanged` 可扩展的分部方法钩子

📌 三个必要条件：

1. 类必须是 `partial`；
2. 继承 `ObservableObject`（或实现 `INotifyPropertyChanged`）；
3. 字段用 `[ObservableProperty]`，命名按下划线小写开头。

生成规则：`_projectName` → `ProjectName`。**XAML 里绑定的名字是生成后的属性名**，写错不会编译报错，只会在运行时输出绑定错误——这是 MVVM 最常见的排错场景。

📌 想看生成器产物：在 csproj 加
```xml
<PropertyGroup><EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles></PropertyGroup>
```
编译后到 `obj/.../generated/` 里找 `.g.cs`。强烈建议做一次，你就能确认「没什么魔法，只是帮你写了样板代码」。

## 3. Binding 语法

```xml
<TextBox Text="{Binding ProjectName, UpdateSourceTrigger=PropertyChanged}" />
<TextBlock Text="{Binding Status}" />
```

| 组成 | 含义 |
|---|---|
| `{Binding ProjectName}` | 从 `DataContext` 取 `ProjectName` 属性 |
| `Mode=TwoWay` | 双向：界面改动写回源（TextBox.Text 默认 TwoWay，但 LostFocus 才更新） |
| `UpdateSourceTrigger=PropertyChanged` | 每敲一个字符就写回源，而不是等失去焦点 |
| `Mode=OneWay` | 源 → 界面，ViewModel 主动改值时刷新（TextBlock 默认） |
| `Mode=OneTime` | 只取一次，之后不再更新 |
| `Converter={StaticResource ...}` | 值转换（如 bool → Visibility） |

📌 `DataContext` 会沿可视树向下继承：Window 上设一次，内部所有控件都能绑定同一个 ViewModel。

## 4. 行模型也要能通知

```csharp
public sealed partial class ChannelItemViewModel : ObservableObject
{
    [ObservableProperty] private int _channel;
    [ObservableProperty] private ushort _raw;
    [ObservableProperty] private double _value;
    [ObservableProperty] private string _result = "";
}
```

`Render` 里每次给 `item.Value = ...` 赋值，都会触发 `PropertyChanged`，DataGrid 对应单元格自动刷新——不需要像第一套那样 `_liveRows.ResetBindings()` 通知整表。

而 `SampleItemViewModel` 只有只读 `init` 属性，因为历史行是**整批替换**（`History.Clear()` 再 `Add`），集合变更通知已经够了。

📌 **经验法则**：会原地改的属性 → 实现通知；整批替换的对象 → 不必实现。

## 5. 对照表

| | WinForms 第一套 | WPF 第二套 |
|---|---|---|
| 通知接口 | `IBindingList`（集合级） | `INotifyPropertyChanged`（属性级） |
| 触发方式 | `ResetBindings()` 手工 | 属性 setter 自动 |
| 绑定源 | `DataSource` | `DataContext` |
| 列名 | `[DisplayName]` + 自动生成 | `<DataGridTextColumn Header=... Binding=...>` |
| 双向输入 | 读 `textBox.Text` | `TwoWay` 绑定自动写回 `ProjectName` |

## 🧪 动手实验

**实验 3.1 · 让绑定失效**
把 XAML 里 `{Binding Status}` 改成 `{Binding StatusXXX}`，运行。界面状态文字不再更新，输出窗口（VS 的「输出」→ WPF 绑定跟踪）会打印 `System.Windows.Data Error: BindingExpression path error`。**学会看这条错误信息**，能省下大量排错时间。改回来。

**实验 3.2 · 试 Mode**
给 `TextBlock` 的绑定加 `Mode=OneTime`，运行。预期：只显示初始值「采集中…」，之后永不更新。改回。

**实验 3.3 · 观察 UpdateSourceTrigger**
去掉 TextBox 上的 `UpdateSourceTrigger=PropertyChanged`，在 `ProjectName` 的 `OnProjectNameChanged` 分部方法里打日志/写文件，观察：默认要等文本框失去焦点才写回。加上之后每敲一个字符都写回。

**实验 3.4 · 看生成器产物**
按第 2 节打开 `EmitCompilerGeneratedFiles`，编译，找到生成的 `MainViewModel.g.cs`，读一遍 `Status` 属性的实现，和本章第 1 节的手写版本对照。

**实验 3.5 · 手写一次**
把 `Status` 改回手写属性（去掉 `[ObservableProperty]`，自己实现 `SetProperty`），运行确认行为一致。做完还原。

## 自测

1. `[ObservableProperty]` 生成的属性名怎么确定？为什么绑定写错不报编译错误？
2. `TextBox.Text` 默认何时写回源？`UpdateSourceTrigger=PropertyChanged` 改变了什么？
3. 为什么 `SampleItemViewModel` 不需要属性通知？

<details><summary>答案要点</summary>

1. 字段名去下划线并首字母大写（`_projectName` → `ProjectName`）；XAML 是运行时解析的字符串路径，编译期不校验。
2. 默认 `LostFocus`；改成每输入一个字符就写回。
3. 历史行不原地修改，而是整批 `Clear()` + `Add()`，由 `ObservableCollection` 的集合通知负责刷新。

</details>

---

上一章：[02-MVVM三角](02-MVVM三角.md) ｜ 下一章：[04-命令与ICommand](04-命令与ICommand.md)
