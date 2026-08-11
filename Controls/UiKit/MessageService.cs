namespace Controls;

/// <summary>
/// Default <see cref="IMessageService"/> implementation. Holds toast state for a single DI scope
/// (one circuit on Blazor Server) when registered via <c>AddWssControlsToasts()</c>. The static
/// <see cref="WasmMessageService"/> facade reuses this same logic over a process-static instance.
/// </summary>
public sealed class MessageService : IMessageService, IDisposable
{
    private readonly ToastQueue<MessageItem> _queue = new();

    /// <inheritdoc/>
    public IReadOnlyList<MessageItem> Items => _queue.Items;

    /// <inheritdoc/>
    public event Action? OnChange
    {
        add => _queue.OnChange += value;
        remove => _queue.OnChange -= value;
    }

    /// <inheritdoc/>
    public Guid Success(string content, double? duration = null) => Add(MessageType.Success, content, duration);
    /// <inheritdoc/>
    public Guid Info(string content, double? duration = null) => Add(MessageType.Info, content, duration);
    /// <inheritdoc/>
    public Guid Warning(string content, double? duration = null) => Add(MessageType.Warning, content, duration);
    /// <inheritdoc/>
    public Guid Error(string content, double? duration = null) => Add(MessageType.Error, content, duration);
    /// <inheritdoc/>
    public Guid Loading(string content, double? duration = null) => Add(MessageType.Loading, content, duration ?? 0);

    /// <inheritdoc/>
    public void Remove(Guid id) => _queue.Remove(id);

    /// <inheritdoc/>
    public void Clear() => _queue.Clear();

    /// <inheritdoc/>
    public void Pause(Guid id) => _queue.Pause(id);

    /// <inheritdoc/>
    public void Resume(Guid id) => _queue.Resume(id);

    private Guid Add(MessageType type, string content, double? duration)
    {
        var item = new MessageItem { Type = type, Content = content, Duration = duration ?? 3 };
        _queue.Add(item);
        return item.Id;
    }

    /// <summary>Cancels any pending auto-dismiss timers (called when the DI scope is torn down).</summary>
    public void Dispose() => _queue.Dispose();
}
