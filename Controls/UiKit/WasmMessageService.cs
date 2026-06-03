namespace Controls;

/// <summary>Severity of a <see cref="WasmMessageService"/> toast.</summary>
public enum MessageType { Success, Info, Warning, Error, Loading }

/// <summary>A single toast message tracked by <see cref="WasmMessageService"/>.</summary>
public class MessageItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public MessageType Type { get; set; }
    public string Content { get; set; } = "";
    public double Duration { get; set; } = 3; // seconds; 0 = sticky
}

/// <summary>
/// Registration-free toast service for <b>Blazor WebAssembly only</b>. Call the static
/// methods from anywhere and drop a single <c>&lt;WasmMessageContainer /&gt;</c> at the app
/// root — no <c>AddXxx()</c> needed.
/// <para>
/// It keeps process-<c>static</c> state, which is safe in a single-user WASM app but would
/// be shared across every user's circuit on Blazor Server — hence the <c>Wasm</c> prefix.
/// Do not use this on Server.
/// </para>
/// </summary>
public static class WasmMessageService
{
    private static readonly List<MessageItem> _items = new();

    public static IReadOnlyList<MessageItem> Items => _items;

    /// <summary>Raised whenever the message list changes so the container can re-render.</summary>
    public static event Action? OnChange;

    public static void Success(string content, double? duration = null) => Add(MessageType.Success, content, duration);
    public static void Info(string content, double? duration = null) => Add(MessageType.Info, content, duration);
    public static void Warning(string content, double? duration = null) => Add(MessageType.Warning, content, duration);
    public static void Error(string content, double? duration = null) => Add(MessageType.Error, content, duration);
    public static void Loading(string content, double? duration = null) => Add(MessageType.Loading, content, duration ?? 0);

    public static void Remove(Guid id)
    {
        if (_items.RemoveAll(i => i.Id == id) > 0)
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>Removes all messages (primarily for tests).</summary>
    public static void Clear()
    {
        _items.Clear();
        OnChange?.Invoke();
    }

    private static void Add(MessageType type, string content, double? duration)
    {
        var item = new MessageItem { Type = type, Content = content, Duration = duration ?? 3 };
        _items.Add(item);
        OnChange?.Invoke();

        if (item.Duration > 0)
        {
            _ = RemoveAfterAsync(item);
        }
    }

    private static async Task RemoveAfterAsync(MessageItem item)
    {
        await Task.Delay(TimeSpan.FromSeconds(item.Duration));
        Remove(item.Id);
    }
}
