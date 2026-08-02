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

    /// <inheritdoc cref="IMessageService.Items"/>
    public static IReadOnlyList<MessageItem> Items => Instance.Items;

    /// <inheritdoc cref="IMessageService.OnChange"/>
    public static event Action? OnChange
    {
        add => Instance.OnChange += value;
        remove => Instance.OnChange -= value;
    }

    /// <inheritdoc cref="IMessageService.Success"/>
    public static Guid Success(string content, double? duration = null) => Instance.Success(content, duration);
    /// <inheritdoc cref="IMessageService.Info"/>
    public static Guid Info(string content, double? duration = null) => Instance.Info(content, duration);
    /// <inheritdoc cref="IMessageService.Warning"/>
    public static Guid Warning(string content, double? duration = null) => Instance.Warning(content, duration);
    /// <inheritdoc cref="IMessageService.Error"/>
    public static Guid Error(string content, double? duration = null) => Instance.Error(content, duration);
    /// <inheritdoc cref="IMessageService.Loading"/>
    public static Guid Loading(string content, double? duration = null) => Instance.Loading(content, duration);

    /// <summary>Dismisses one message by the id its factory method returned.</summary>
    public static void Remove(Guid id) => Instance.Remove(id);

    /// <summary>Removes all messages (primarily for tests).</summary>
    public static void Clear() => Instance.Clear();
}
