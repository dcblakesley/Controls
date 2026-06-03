namespace Controls;

/// <summary>Severity of a toast message.</summary>
public enum MessageType { Success, Info, Warning, Error, Loading }

/// <summary>A single toast message tracked by an <see cref="IMessageService"/>.</summary>
public class MessageItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public MessageType Type { get; set; }
    public string Content { get; set; } = "";
    public double Duration { get; set; } = 3; // seconds; 0 = sticky
}

/// <summary>
/// Toast message service. Two ways to use it:
/// <list type="bullet">
/// <item><b>Blazor Server (or WASM) — scoped, recommended:</b> register with
/// <c>builder.Services.AddWssControlsToasts()</c>, <c>@inject IMessageService</c>, and drop one
/// <c>&lt;MessageContainer /&gt;</c> at the app root. State is per-DI-scope (per circuit on Server),
/// so it never bleeds across users.</item>
/// <item><b>WASM only — registration-free static:</b> call <see cref="WasmMessageService"/>'s static
/// methods and drop <c>&lt;WasmMessageContainer /&gt;</c>. Simpler, but its process-static state is
/// unsafe on Server — do not use it there.</item>
/// </list>
/// Both render identically (the same <c>wss-msg-*</c> markup).
/// </summary>
public interface IMessageService
{
    /// <summary>The currently-visible messages.</summary>
    IReadOnlyList<MessageItem> Items { get; }

    /// <summary>Raised whenever <see cref="Items"/> changes so a container can re-render.</summary>
    event Action? OnChange;

    void Success(string content, double? duration = null);
    void Info(string content, double? duration = null);
    void Warning(string content, double? duration = null);
    void Error(string content, double? duration = null);
    void Loading(string content, double? duration = null);
    void Remove(Guid id);
    void Clear();
}
