# 02 · MVVM 三角

**目标**：能说清 Model / View / ViewModel 各自负责什么，能判断一行代码该放在哪一层。

## 1. 三个角色

```mermaid
flowchart LR
    V["View<br/>MainWindow.xaml<br/>只描述长什么样"] -->|"{Binding} 读状态<br/>Command 触发动作"| VM["ViewModel<br/>MainViewModel<br/>状态 + 命令 + 封送"]
    VM -->|"调用接口"| M["Model / Services<br/>Core + Infrastructure + Application<br/>业务规则与数据"]
    M -->|"事件 / 返回值"| VM
    VM -. "属性通知自动刷新" .-> V
```

| 角色 | 本骨架中的文件 | 负责 | 禁止 |
|---|---|---|---|
| **Model** | `Core/` + `Infrastructure/` + `Application/` | 业务数据、规则、采集、持久化 | 知道任何 Window/控件 |
| **View** | `Views/MainWindow.xaml` | 布局、样式、绑定表达式、把事件转成命令 | 业务判断、访问数据库、起后台线程 |
| **ViewModel** | `ViewModels/MainViewModel.cs` | 暴露可绑定状态、暴露命令、把后台数据封送回 UI 线程、协调调用 | 直接引用 View 类型、写 XAML、放标定公式 |

📌 **ViewModel 不是"code-behind 的搬家"**。它是「为视图量身定做的状态机」：View 需要什么状态就暴露什么属性，View 能做什么操作就暴露什么命令。它不关心按钮是方的还是圆的。

📌 **依赖方向**：`View → ViewModel → Model`，单向。ViewModel 绝不 `new MainWindow()`，也不持有 `Button`、`TextBox` 引用。

## 2. 为什么 WinForms 不"必须" MVVM，WPF 却几乎必然

| | WinForms | WPF |
|---|---|---|
| 绑定能力 | 有 `DataSource`，但较弱，属性通知不完整 | 强大的 `{Binding}`、`DataTemplate`、`DataContext` 继承 |
| 命令模型 | 事件处理 `Click +=` | 原生 `ICommand`，按钮/菜单/快捷键共用 |
| 界面描述 | 主要靠 Designer 生成的 C# | XAML 声明式，天然与逻辑分离 |
| 典型模式 | MVP / 事件驱动 | **MVVM**（不用 MVVM 反而更别扭） |

第一套的 `MainForm` 其实已经具备 ViewModel 的雏形（持有 `_liveRows`、`_latest`、`_status`，接收事件，调用仓储），只是它**同时还是控件本身**。WPF 把这两件事拆成了两个类：

```
MainForm : Form                 ← 既是控件又是逻辑
MainWindow : Window             ← 只是控件（code-behind 3 行）
MainViewModel : ObservableObject ← 只是逻辑（不继承任何控件）
```

## 3. code-behind 该有多薄

打开 `Views/MainWindow.xaml.cs`，有效代码只有：

```csharp
InitializeComponent();                    // 加载 XAML
_viewModel = viewModel;                   // 保存引用
DataContext = viewModel;                  // 建立绑定源
Closed += (_, _) => _viewModel.Unsubscribe();  // 生命周期转发
```

📌 判断标准：**code-behind 里出现 `if (value > limit)`、`File.ReadAllText`、`new SerialPort`、`Task.Run` 中的任意一个，就是坏味道**。真正需要写 code-behind 的场景只有：纯视觉行为（拖拽、动画、焦点、自定义控件），且这些逻辑与业务无关。

## 4. 与第一套逐块对照

| 第一套 `MainForm` | 第二套 | 说明 |
|---|---|---|
| `_liveRows` (`BindingList`) | `Channels` (`ObservableCollection`) | 状态挪到 ViewModel |
| `_historyRows` | `History` | 同上 |
| `_status` 字段 + `_status.Text = ...` | `[ObservableProperty] Status` + XAML 绑定 | 不再手工赋值控件 |
| `_projectBox` 控件 | `[ObservableProperty] ProjectName` | ViewModel 不持有 TextBox |
| `OnSaveClick` 事件处理 | `SaveCommand` (`IAsyncRelayCommand`) | 命令取代事件 |
| `OnBatchArrived` / `RenderBatch` | 同名方法，只是改集合 | 机制完全一致 |
| `OnFormClosed` 取消订阅 | `Unsubscribe()` 由 `Closed` 调用 | 生命周期转发 |

📌 你会发现：**逻辑几乎逐条对应，只是"控件赋值"变成了"属性赋值"**。这是理解 MVVM 最快的路径。

## 🧪 动手实验

**实验 2.1 · 故意写坏一次**
在 `MainWindow.xaml.cs` 里给一个按钮加 `Click` 事件，并在事件里写 `if (viewModel.Channels.Count > 0) ...`。运行没问题，但对照上表说出它破坏了哪条规则。做完删掉。

**实验 2.2 · 找出所有 ViewModel 不该有的东西**
在 `MainViewModel.cs` 里搜索 `Window`、`Button`、`TextBox`、`MessageBox`。应该一个都没有（当前骨架确实没有）。再搜索 `Calibration`、`Convert(`、`RawLow`：也不该有。

**实验 2.3 · 加一个纯 UI 状态**
在 ViewModel 加 `[ObservableProperty] private bool _showHistory = true;`，在 XAML 的下方 DataGrid 上绑 `Visibility="{Binding ShowHistory, Converter=...}"`（需要布尔转可见性的转换器）。思考：这个状态属于 ViewModel 还是 View？答案：**如果只是显示开关，放 View 更合适；如果业务规则决定"无权限时隐藏"，才放 ViewModel**。这是常见的设计判断题。

## 自测

1. ViewModel 能不能引用 `MainWindow`？为什么？
2. 「判定是否合格」应该放在哪一层？
3. 为什么第一套的 `MainForm` 不算 MVVM，但已经"很像"了？

<details><summary>答案要点</summary>

1. 不能，会形成双向依赖、无法单独测试、换 View 就要改 ViewModel；需要通知 View 时用事件/消息/状态属性。
2. `Application/AcquisitionService`（业务规则），ViewModel 只显示结果字符串。
3. 它把状态、事件、命令都集中在 Form 里，逻辑上已是"视图状态持有者"，但它继承 `Form`、直接持有控件、直接赋值控件属性，没有做到 View 与状态解耦。

</details>

---

上一章：[01-先跑起来](01-先跑起来.md) ｜ 下一章：[03-数据绑定与ObservableObject](03-数据绑定与ObservableObject.md)
