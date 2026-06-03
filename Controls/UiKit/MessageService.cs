namespace Controls;

/// <summary>
/// Default <see cref="IMessageService"/> implementation. Holds toast state for a single DI scope
/// (one circuit on Blazor Server) when registered via <c>AddWssControlsToasts()</c>. The static
/// <see cref="WasmMessageService"/> facade reuses this same logic over a process-static instance.
/// </summary>
public sealed class MessageService : IMessageService
{
    private readonly List<MessageItem> _items = new();

    /// <inheritdoc/>
    public IReadOnlyList<MessageItem> Items => _items;

    /// <inheritdoc/>
    public event Action? OnChange;

    /// <inheritdoc/>
    public void Success(string content, double? duration = null) => Add(MessageType.Success, content, duration);
    /// <inheritdoc/>
    public void Info(string content, double? duration = null) => Add(MessageType.Info, content, duration);
    /// <inheritdoc/>
    public void Warning(string content, double? duration = null) => Add(MessageType.Warning, content, duration);
    /// <inheritdoc/>
    public void Error(string content, double? duration = null) => Add(MessageType.Error, content, duration);
    /// <inheritdoc/>
    public void Loading(string content, double? duration = null) => Add(MessageType.Loading, content, duration ?? 0);

    /// <inheritdoc/>
    public void Remove(Guid id)
    {
        if (_items.RemoveAll(i => i.Id == id) > 0)
        {
            OnChange?.Invoke();
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _items.Clear();
        OnChange?.Invoke();
    }

    private void Add(MessageType type, string content, double? duration)
    {
        var item = new MessageItem { Type = type, Content = content, Duration = duration ?? 3 };
        _items.Add(item);
        OnChange?.Invoke();

        if (item.Duration > 0)
        {
            _ = RemoveAfterAsync(item);
        }
    }

    private async Task RemoveAfterAsync(MessageItem item)
    {
        await Task.Delay(TimeSpan.FromSeconds(item.Duration));
        Remove(item.Id);
    }
}
