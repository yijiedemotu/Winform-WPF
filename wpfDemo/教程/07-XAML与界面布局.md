# 07 · XAML 与界面布局

**目标**：能读懂 `MainWindow.xaml`，理解「声明式 UI」与第一套 `BuildUi()` 的对应关系。

## 1. XAML 不是"另一种语言"，是对象图的声明

```xml
<Window ...>
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="2*" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        ...
    </Grid>
</Window>
```

这段代码在编译/加载时被翻译成「创建 `Grid`，设置 `Margin`，往 `RowDefinitions` 加 3 行，再创建子控件」。等价于第一套 `BuildUi()` 里的一串 `new` 和属性赋值，只是**声明式**：结构一眼可见，父子关系靠缩进表达。

📌 三种尺寸：

| 写法 | 含义 |
|---|---|
| `Height="Auto"` | 由内容决定 |
| `Height="2*"` / `Height="*"` | 按比例分配剩余空间（2:1） |
| 固定数值 | 像素（WPF 里是 DIP） |

## 2. 逐块对照第一套的 `BuildUi()`

| `MainForm.BuildUi()` | `MainWindow.xaml` |
|---|---|
| `TableLayoutPanel` 3 行（48 / 250 / 剩余） | `<Grid>` + 3 个 `RowDefinition` |
| `FlowLayoutPanel` 横向排列工具栏 | `<StackPanel Orientation="Horizontal">` |
| `new Label { Text = "产品名称：" }` | `<TextBlock Text="产品名称：" />` |
| `new TextBox { Width = 160 }` | `<TextBox Width="160" Text="{Binding ProjectName, ...}" />` |
| `new Button { Text = "保存数据" }` + `Click +=` | `<Button Content="保存数据" Command="{Binding SaveCommand}" />` |
| `Label _status` 手工赋值 | `<TextBlock Text="{Binding Status}" />` |
| `CreateGrid(_liveRows)` → `DataSource` | `<DataGrid ItemsSource="{Binding Channels}">` |
| `AutoGenerateColumns = true` + `[DisplayName]` | `AutoGenerateColumns="False"` + 显式 `<DataGridTextColumn>` |
| `Dock = DockStyle.Fill` | `Grid.Row` + 默认拉伸 |

📌 **关键差异不在布局，而在绑定**：第一套要给控件起名字（`_projectBox`、`_status`）以便代码里找它们；WPF 里绝大多数控件**没有 `x:Name`**，因为 ViewModel 从不引用控件，只暴露属性。数一数 XAML 里有多少个 `x:Name`：答案是 0（`x:Class` 不算控件名）。

## 3. DataGrid 列定义

```xml
<DataGrid ItemsSource="{Binding Channels}" AutoGenerateColumns="False" IsReadOnly="True">
    <DataGrid.Columns>
        <DataGridTextColumn Header="通道"       Binding="{Binding Channel}" Width="*" />
        <DataGridTextColumn Header="原始值"     Binding="{Binding Raw}"      Width="*" />
        <DataGridTextColumn Header="工程值 / mm" Binding="{Binding Value}"    Width="*" />
        <DataGridTextColumn Header="判定"       Binding="{Binding Result}"   Width="*" />
    </DataGrid.Columns>
</DataGrid>
```

📌 `ItemsSource` 绑定到集合；每一列的 `Binding` 是**相对于集合里的单个元素**（`ChannelItemViewModel`）的路径，不再是相对 `DataContext`。这是初学 WPF 最容易绕晕的一点。

📌 `AutoGenerateColumns="False"` 让列名、顺序、宽度完全可控。设成 `True` 时 WPF 会用反射按属性名生成列——与第一套 `AutoGenerateColumns=true` + `[DisplayName]` 的效果对应，你可以两种都试。

## 4. 样式资源：`App.xaml`

```xml
<Application.Resources>
    <Style TargetType="Button">
        <Setter Property="Padding" Value="14,5" />
        <Setter Property="Margin" Value="6,0,0,0" />
    </Style>
</Application.Resources>
```

没有 `x:Key` 的 Style 是**隐式样式**，作用于整个应用里所有 `Button`。这就是企业级 UI 一致性的基础：改一处，全应用生效；以后换主题只需替换资源字典。

对照第一套：那里每个按钮的 `Padding`/`Margin` 要逐个设置（ Designer 里也是重复属性）。

## 5. code-behind 只有 3 行有效代码

```csharp
public MainWindow(MainViewModel viewModel)
{
    InitializeComponent();        // 加载 XAML 生成的对象图
    _viewModel = viewModel;
    DataContext = viewModel;      // 接线柱
    Closed += (_, _) => _viewModel.Unsubscribe();
}
```

📌 判断标准再次强调：**XAML 描述"长什么样"，code-behind 只做接线与生命周期，ViewModel 提供状态与命令**。任何业务判断都不该出现在这两个文件里。

## 🧪 动手实验

**实验 7.1 · 加一列**
在实时 DataGrid 里加一列 `<DataGridTextColumn Header="序号" Binding="{Binding Channel}" />`（重复列也可以），运行确认出现。再给 `ChannelItemViewModel` 加一个新属性 `Timestamp` 并绑定，观察绑定路径如何对应行对象。

**实验 7.2 · 换成自动生成列**
把第一个 DataGrid 改成 `AutoGenerateColumns="True"` 并删掉 `<DataGrid.Columns>`，运行。列名变成英文属性名（`Channel`、`Raw`、`Value`、`Result`），第一套则用 `[DisplayName]` 显示中文。思考：怎么在 WPF 里也能显示中文（提示：`[Display(Name="通道")]` 或 `DisplayName` 特性 + 自定义列生成事件）。

**实验 7.3 · 改全局样式**
在 `App.xaml` 的 Style 里加 `<Setter Property="FontSize" Value="14" />`，运行，确认所有按钮同时变化。再给某个按钮单独设 `FontSize="10"`，确认局部设置优先于隐式样式。

**实验 7.4 · 把比例改成固定值**
把 `2*` / `*` 改成 `200` / `150`，运行，观察窗口缩放时两块表格是否还自适应。这个实验能让你记住 `*` 与固定值的区别。

**实验 7.5 · 数 x:Name**
用 `grep -c "x:Name" Views/MainWindow.xaml`，确认为 0。再想想：如果 ViewModel 里需要"让某个文本框获得焦点"，该怎么做而不破坏 MVVM？（提示：用行为/附加属性或消息，而不是传控件引用。）

## 自测

1. `<DataGridTextColumn Binding="{Binding Value}">` 里的 `Value` 是相对谁的？
2. 为什么 XAML 里几乎不需要 `x:Name`？
3. 隐式 Style 与局部属性设置谁优先？

<details><summary>答案要点</summary>

1. 相对 `ItemsSource` 集合中的单个行对象（`ChannelItemViewModel`），不是 Window 的 DataContext。
2. 因为 ViewModel 不引用控件，控件之间也不需要互相引用；状态通过绑定、动作通过命令传递。
3. 局部（依赖属性值优先级更高）。

</details>

---

上一章：[06-依赖注入与ViewModel定位](06-依赖注入与ViewModel定位.md) ｜ 下一章：[08-业务层零改动](08-业务层零改动.md)
