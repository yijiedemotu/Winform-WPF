# 09 · 跨线程与 Dispatcher

**目标**：理解 WPF 的 UI 线程模型，掌握「后台算、UI 线程改集合」，并搞清关闭窗口时的订阅与死锁问题。

## 1. WPF 的 UI 线程与 Dispatcher

WPF 有一个 `Dispatcher`：它持有 UI 线程的优先级消息队列，负责派发输入、渲染、布局、绑定更新。`DispatcherObject`（几乎所有 WPF 控件）都会校验调用线程：

```
InvalidOperationException: 调用线程无法访问此对象，因为另一个线程拥有该对象。
```

集合还有额外一层：`ObservableCollection` 的 `CollectionChanged` 被 `CollectionView` 消费时，跨线程修改会抛：

```
NotSupportedException: 该类型的 CollectionView 不支持从与 Dispatcher 线程不同的线程
对其 SourceCollection 进行的更改
```

📌 所以 WPF 里**两条约束同时存在**：控件只能 UI 线程碰；绑定中的集合也只能 UI 线程改。

## 2. 三种封送写法，推荐第一种

| 写法 | 示例 | 评价 |
|---|---|---|
| `SynchronizationContext.Post` | `_uiContext.Post(_ => Render(batch), null)` | ✅ 框架无关，与 WinForms 版代码相同，本骨架采用 |
| `Dispatcher.BeginInvoke` | `Application.Current.Dispatcher.BeginInvoke(...)` | 可用，但 ViewModel 依赖 `Application.Current`，耦合且难测试 |
| `Dispatcher.Invoke` | `Dispatcher.Invoke(...)` | 同步等待，后台线程被拖住，容易死锁 |

```csharp
// ViewModels/MainViewModel.cs
private void OnBatchArrived(IReadOnlyList<ChannelReading> batch)
{
    _latest = batch.ToArray();                    // 快照，采集线程自己写
    _uiContext.Post(_ => Render(batch), null);    // 集合/属性操作交给 UI 线程
}
```

📌 `_uiContext` 在 `MainViewModel` 构造时捕获。因为 ViewModel 由 `App.OnStartup`（UI 线程）创建，`SynchronizationContext.Current` 此时是 `DispatcherSynchronizationContext`。

📌 与第一套 `MainForm.OnBatchArrived` **逐字相同**。第 08 章的「同一套抽象、不同实现」在这里得到最具体的体现。

## 3. 为什么 `await` 之后可以直接改集合

```csharp
private async Task LoadHistoryAsync()
{
    var records = await _repository.QueryAsync(null, null, null);  // 异步 IO
    History.Clear();                                               // 回到 UI 线程
    ...
}
```

`await` 捕获当前 `SynchronizationContext`（UI），continuation 默认 `Post` 回 UI 线程，所以后面改 `History` 是安全的。这和第一套 `await` 后直接改 `_status.Text` 完全同理。

⚠️ 如果在库代码里滥用 `ConfigureAwait(false)`，continuation 就不回 UI 线程了；UI 层的 `await` 不要加它。

## 4. 取消订阅：别让单例服务抓住死掉的 ViewModel

```
AcquisitionService（单例，活得和 Host 一样久）
    └─ BatchArrived 事件（强引用）
         └─ MainViewModel.OnBatchArrived
              └─ MainViewModel → ObservableCollection → 绑定 → 已关闭的 Window
```

不取消订阅的后果：内存泄漏；窗口关闭后服务仍 `Post`，向已销毁的集合写数据；`Unsubscribe()` 形同虚设。

```csharp
// Views/MainWindow.xaml.cs
Closed += (_, _) => _viewModel.Unsubscribe();

// ViewModels/MainViewModel.cs
public void Unsubscribe() => _acquisition.BatchArrived -= OnBatchArrived;
```

对照第一套：`MainForm.OnFormClosed` 里 `_acquisition.BatchArrived -= OnBatchArrived;`。**同一个问题，两种框架的生命周期钩子不同**（`FormClosed` vs `Closed`）。

📌 更稳健的做法：让 ViewModel 实现 `IDisposable`，由容器或窗口在关闭时调用；或用弱事件 / 消息总线。骨架用显式 `Unsubscribe()` 是为了让这条纪律可见。

## 5. 关闭时的死锁陷阱

```csharp
// App.xaml.cs
Task.Run(() => _host.StopAsync()).GetAwaiter().GetResult();
```

如果写成 `_host.StopAsync().GetAwaiter().GetResult()`：`StopAsync` 内部 `await` 捕获了 `DispatcherSynchronizationContext`，continuation 要 `Post` 回 UI 线程；但 UI 线程正被 `GetResult()` 阻塞 → **死锁，窗口关不掉**。

用 `Task.Run` 让 `StopAsync` 在没有 UI 上下文的线程池线程上执行，continuation 也在线程池，互不等待。第一套的 `Program.cs` 用了同一行代码，原因一样（那边是 `WindowsFormsSynchronizationContext`）。

## 🧪 动手实验

**实验 9.1 · 抓两种异常**
把 `OnBatchArrived` 里的 `Post` 改成直接 `Render(batch)`，运行，观察输出窗口的 `NotSupportedException`（集合跨线程）。做完还原。对比第一套实验 7.1 的「线程间操作无效」，理解两种 UI 框架报错措辞不同、根因相同。

**实验 9.2 · 用 Dispatcher 改写（对照）**
把 `Post` 换成：

```csharp
Application.Current.Dispatcher.BeginInvoke(new Action(() => Render(batch)));
```

运行确认功能相同。然后回答：这行代码能不能在没有 `Application` 的自检/单元测试环境里跑？（不能。）这就是推荐 `SynchronizationContext` 的理由。做完还原。

**实验 9.3 · 复现关闭死锁（谨慎，做完立刻还原）**
把 `OnExit` 的 `Task.Run(...)` 改成直接 `_host.StopAsync().GetAwaiter().GetResult();`，把 `SampleIntervalMs` 改大，运行并关闭窗口。若窗口卡住，用任务管理器结束进程。做完**一定**还原。这个实验能让你记住线程池包装的必要性。

**实验 9.4 · 验证取消订阅**
注释掉 `MainWindow` 的 `Closed += ...`，在 `Render` 里加一句日志（写文件即可），运行后关闭窗口，观察 Host 停止前是否还有 `Render` 调用。做完还原。

**实验 9.5 · `ConfigureAwait(false)` 的副作用**
在 `LoadHistoryAsync` 的 `QueryAsync` 后临时加 `.ConfigureAwait(false)`，运行并点「保存数据」，观察是否抛跨线程集合异常。做完还原。这能加深你对「UI 层 await 保留上下文」的理解。

## 自测

1. WPF 里跨线程改控件、跨线程改绑定集合，分别会抛什么？
2. 为什么 ViewModel 不直接用 `Application.Current.Dispatcher`？
3. `await` 之后为什么能直接改 `ObservableCollection`？什么时候会失效？

<details><summary>答案要点</summary>

1. `InvalidOperationException`（调用线程无法访问此对象）/ `NotSupportedException`（CollectionView 不支持跨线程更改）。
2. 会把 ViewModel 绑死到 WPF `Application`，无法在无窗口的自检或单元测试中使用，也破坏框架无关性。
3. `await` 捕获 UI 的 `SynchronizationContext` 并把 continuation Post 回去；若在 UI 层加 `ConfigureAwait(false)`，continuation 会留在线程池，从而跨线程改集合。

</details>

---

上一章：[08-业务层零改动](08-业务层零改动.md) ｜ 下一章：[10-进阶路线与总复盘](10-进阶路线与总复盘.md)
