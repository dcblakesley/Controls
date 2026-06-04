namespace Controls;

/// <summary>
/// Registration-free <b>static</b> toast facade for <b>Blazor WebAssembly</b>. Call the static
/// methods from anywhere and drop a single <c>&lt;WasmMessageContainer /&gt;</c> at the app root —
/// no <c>AddXxx()</c> needed.
/// <para>
/// It forwards to a single process-<c>static</c> <see cref="MessageService"/>. That static state is
/// safe in a single-user WASM app but would be shared across every user's circuit on Blazor Server,
/// hence the <c>Wasm</c> prefix. <b>On Server, use the scoped <see cref="IMessageService"/></b>
/// (register with <c>AddWssControlsToasts()</c>) and <c>&lt;MessageContainer /&gt;</c> instead.
/// </para>
/// </summary>
public static class WasmMessageService
{
    private static readonly MessageService Instance = new();

    public static IReadOnlyList<MessageItem> Items => Instance.Items;

    /// <summary>Raised whenever the message list changes so the container can re-render.</summary>
    public static event Action? OnChange
    {
        add => Instance.OnChange += value;
        remove => Instance.OnChange -= value;
    }

    public static Guid Success(string content, double? duration = null) => Instance.Success(content, duration);
    public static Guid Info(string content, double? duration = null) => Instance.Info(content, duration);
    public static Guid Warning(string content, double? duration = null) => Instance.Warning(content, duration);
    public static Guid Error(string content, double? duration = null) => Instance.Error(content, duration);
    public static Guid Loading(string content, double? duration = null) => Instance.Loading(content, duration);

    public static void Remove(Guid id) => Instance.Remove(id);

    /// <summary>Removes all messages (primarily for tests).</summary>
    public static void Clear() => Instance.Clear();
}
