namespace Controls;

/// <summary>Severity of a toast message.</summary>
public enum MessageType { Success, Info, Warning, Error, Loading }

/// <summary>A single toast message tracked by an <see cref="IMessageService"/>.</summary>
public class MessageItem : IToastItem
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

    /// <summary>Shows a toast and returns its id, which can be passed to <see cref="Remove"/>.</summary>
    Guid Success(string content, double? duration = null);
    /// <inheritdoc cref="Success"/>
    Guid Info(string content, double? duration = null);
    /// <inheritdoc cref="Success"/>
    Guid Warning(string content, double? duration = null);
    /// <inheritdoc cref="Success"/>
    Guid Error(string content, double? duration = null);
    /// <summary>
    /// Shows a (by default sticky, <c>duration: 0</c>) loading toast and returns its id. Keep the id
    /// and pass it to <see cref="Remove"/> to dismiss the spinner when the work completes.
    /// </summary>
    Guid Loading(string content, double? duration = null);
    void Remove(Guid id);
    void Clear();

    /// <summary>
    /// Pauses <paramref name="id"/>'s auto-dismiss countdown (WCAG 2.2.1) -- call while the user is
    /// hovering the pointer over the toast or has kept keyboard focus inside it, and pair with
    /// <see cref="Resume"/> on the corresponding leave/blur. A no-op for a sticky toast (no timer to
    /// pause) or an id no longer tracked. <c>MessageContainer</c>/<c>WasmMessageContainer</c> already
    /// wire this from <see cref="MessageListView"/>'s hover/focus events -- most consumers never call
    /// it directly.
    /// </summary>
    void Pause(Guid id);

    /// <summary>
    /// Resumes <paramref name="id"/>'s auto-dismiss countdown from a fresh full duration -- not the
    /// time remaining when <see cref="Pause"/> was called. See <see cref="ToastQueue{TItem}.Resume"/>
    /// for why that's the simplest correct behavior. A no-op for a sticky toast or an id no longer
    /// tracked.
    /// </summary>
    void Resume(Guid id);
}
