# 05 · 集合与 `ObservableCollection`

**目标**：理解集合变更通知、UI 线程约束，以及为什么 `Render` 要「原地更新」而不是每次重建集合。

## 1. 两种通知，别混淆

| 通知 | 接口 | 触发时机 | 本骨架 |
|---|---|---|---|
| **属性**通知 | `INotifyPropertyChanged` | 对象某个属性的值变了 | `ChannelItemViewModel.Value = ...` |
| **集合**通知 | `INotifyCollectionChanged` | 集合增删、清空、重排 | `History.Clear()` / `History.Add(...)` |

`ObservableCollection<T>` 同时实现了两者，并在 `Add/Remove/Clear` 时发 `CollectionChanged`。WPF 的 `ItemsControl`/`DataGrid` 监听它，自动增删行。

```csharp
public ObservableCollection<ChannelItemViewModel> Channels { get; } = new();
public ObservableCollection<SampleItemViewModel> History { get; } = new();
```

📌 **属性是只读 `get`-only**：集合对象本身不替换，只改内容。如果把集合整体 `new` 成一个新对象，绑定不会自动更新（除非该属性也发通知）。

## 2. `Render`：原地更新，避免整表重建

```csharp
private void Render(IReadOnlyList<ChannelReading> batch)
{
    if (Channels.Count != batch.Count)
    {
        Channels.Clear();
        foreach (var _ in batch) Channels.Add(new ChannelItemViewModel());
    }

    for (int i = 0; i < batch.Count; i++)
    {
        var item = Channels[i];
        var reading = batch[i];
        item.Channel = reading.Channel;     // 每个赋值都触发 PropertyChanged
        item.Raw = reading.Raw;
        item.Value = Math.Round(reading.Value, 3);
        item.Result = reading.IsQualified ? "合格" : "不合格";
    }

    Status = $"最近刷新 ...";
}
```

📌 为什么要这样写：

- 只在**通道数变化**时重建集合（正常运行时一次都不重建）；
- 之后每 500 ms 只做 8×4 次属性赋值，DataGrid 只刷新变化的单元格；
- 若每周期都 `Channels.Clear()` + 重新 `Add` 8 行，会造成闪烁、选中态丢失、滚动位置丢失。

对照第一套：`BindingList` 没有属性级通知，所以那里用 `_liveRows.ResetBindings()` 让整表重读。两者都能工作，**粒度不同**。

## 3. UI 线程约束（最容易踩的坑）

`ObservableCollection` 的 `CollectionChanged` 会被 WPF 绑定引擎直接消费，而绑定引擎在 UI 线程。因此：

> **从后台线程对 `ObservableCollection` 做 Add/Clear，会抛 `NotSupportedException: 该类型的 CollectionView 不支持从与 Dispatcher 线程不同的线程对其 SourceCollection 进行的更改`。**

本骨架的防护是第 09 章要细讲的：

```csharp
private void OnBatchArrived(IReadOnlyList<ChannelReading> batch)
{
    _latest = batch.ToArray();
    _uiContext.Post(_ => Render(batch), null);   // 把集合操作搬到 UI 线程
}
```

📌 WPF 也提供 `BindingOperations.EnableCollectionSynchronization(collection, lockObject)` 允许跨线程改集合，但**不推荐**：它只是加锁同步，容易掩盖设计问题。正解是「后台算、UI 线程改集合」。

📌 属性通知（不是集合通知）从后台线程发出，WPF 通常会自动 marshal，但别依赖这个；统一封送最省心。

## 4. 与第一套对照

| 第一套 | 第二套 |
|---|---|
| `BindingList<ChannelRow>` | `ObservableCollection<ChannelItemViewModel>` |
| `_liveRows.ResetBindings()` 整表重读 | 行对象 `ObservableObject` 逐属性通知 |
| `[DisplayName]` 决定列名 | DataGrid 显式列定义 |
| 手工赋值 `_status.Text` | `Status` 属性 + 绑定 |
| `Controls.Clear()` 移除页面时的线程问题 | `Closed` 时取消订阅，避免后台继续改集合 |

## 🧪 动手实验

**实验 5.1 · 抓跨线程异常（必做）**
把 `OnBatchArrived` 里的 `_uiContext.Post(...)` 临时改成直接 `Render(batch)`，运行。预期：抛集合跨线程异常，界面停止刷新。做完还原。这与第一套实验 7.1 是同一个道理，但异常类型不同，值得都见一次。

**实验 5.2 · 换成 `List<T>`**
把 `Channels` 的类型改成 `List<ChannelItemViewModel>`（保留 `Add` 调用）。运行：第一次构造后界面不再随着集合变化刷新（因为没有集合通知）。做完还原。

**实验 5.3 · 每周期重建集合**
把 `Render` 改成无条件 `Channels.Clear()` + 重新 `Add` 8 行。运行，观察滚动/选中态是否丢失、CPU 是否升高。做完还原。

**实验 5.4 · 大批量数据**
把 `SampleIntervalMs` 改小、`ChannelCount` 改成 64（同时补齐 64 组配置），观察界面是否仍然流畅。思考：真实项目里几千行数据该用什么（`VirtualizingPanel`、分页、`DataGrid` 虚拟化、`ObservableRangeCollection` 批量通知）。

## 自测

1. 属性通知和集合通知分别由谁触发？
2. 为什么 `Channels` 属性只有 `get`？
3. 从后台线程 `Add` 会怎样？有哪两种解法，推荐哪种？

<details><summary>答案要点</summary>

1. `INotifyPropertyChanged`（`ObservableObject` 的 setter）与 `INotifyCollectionChanged`（`ObservableCollection` 的增删方法）。
2. 集合实例只创建一次，绑定的是这个实例；替换实例会让绑定指向旧对象（除非属性也发通知）。
3. 抛跨线程异常；可用 `SynchronizationContext.Post` 封送（推荐）或 `EnableCollectionSynchronization` 加锁（不推荐，掩盖设计问题）。

</details>

---

上一章：[04-命令与ICommand](04-命令与ICommand.md) ｜ 下一章：[06-依赖注入与ViewModel定位](06-依赖注入与ViewModel定位.md)
