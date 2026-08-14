using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// An AntDesign-5-style color picker: a swatch trigger opening a popup with a saturation/brightness
/// area, a hue slider, an optional alpha slider, a HEX/RGB input row, and an optional preset row.
/// Binds a plain <c>string?</c> — see <see cref="Value"/> for the exact accepted/emitted forms. For
/// form use (label, validation, read-only view, <see cref="FormOptions"/>) wrap it in
/// <see cref="EditColor"/> rather than using this directly.
/// </summary>
/// <remarks>
/// <para>
/// Built on <see cref="PopupOverlayBase"/> — the same placement/dismiss/trigger-ARIA/focus-restore
/// engine behind <see cref="Popover"/> and <see cref="Popconfirm"/> — rather than
/// <see cref="PickerBase"/>, whose extra machinery (<c>wss-picker.js</c> roving-tabindex grid
/// navigation, typed-date field wiring) is calendar-specific and has no counterpart here.
/// </para>
/// <para>
/// <b>Hue is component state, not derived state.</b> HSV→RGB is lossy at zero saturation or zero
/// value (every hue produces the same black/white/grey), so this component keeps the live
/// <see cref="ColorMath.Hsv"/> across renders and re-derives it from <see cref="Value"/> only when the
/// parameter actually changes to something it did not itself emit — otherwise dragging brightness to
/// black would snap the hue slider back to red. The same rule applies when an incoming color IS
/// achromatic: the session hue is kept and only saturation/value are adopted.
/// </para>
/// <para>
/// <b>Every JS-dependent behavior degrades.</b> <c>wss-color.js</c> supplies pointer dragging (and the
/// per-key <c>preventDefault</c> Blazor cannot express); without it a single click still positions the
/// handle from <see cref="MouseEventArgs.OffsetX"/>/<c>OffsetY</c> and the arrow keys still step, so
/// the control is fully operable under prerender/bUnit — see <see cref="TrackWidth"/> for the one
/// caveat that fallback carries.
/// </para>
/// </remarks>
public partial class ColorPicker : PopupOverlayBase
{
    /// <summary>
    /// The bound color, two-way bindable via <c>@bind-Value</c>. Accepts 3/4/6/8-digit hex (with or
    /// without a leading <c>#</c>) and <c>rgb()</c>/<c>rgba()</c> text; emits the normalized lowercase
    /// <c>#rrggbb</c>, extended to <c>#rrggbbaa</c> only when the color is translucent AND
    /// <see cref="ShowAlpha"/> is on. A value that can't be parsed at all (including null/empty) renders
    /// as "no color" — the AntD-style empty indicator — rather than throwing.
    /// </summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised on every committed change — a drag, an arrow-key step, a preset click, a typed
    /// commit, or <see cref="AllowClear"/>'s clear (which raises <c>null</c>). Supports <c>@bind-Value</c>.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// Raised with the offending text when a typed entry in the HEX input can't be parsed as a color at
    /// all; the entry is reverted and <see cref="ValueChanged"/> does NOT fire. The RGB row's number
    /// inputs never raise this — an out-of-range or non-numeric entry there clamps or reverts silently,
    /// since a <c>number</c> input has no "unparseable text" state worth surfacing.
    /// <see cref="EditColor"/> turns this into a validation message.
    /// </summary>
    [Parameter] public EventCallback<string> OnParseError { get; set; }

    /// <summary>Disables the trigger and every interactive path — the popup can't be opened, and an
    /// already-open popup closes if this flips to true.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows the alpha slider and lets the value carry an alpha channel (default true). When
    /// false the channel is stripped from the emitted value even if the bound-in value had one.</summary>
    [Parameter] public bool ShowAlpha { get; set; } = true;

    /// <summary>Renders the current value's normalized hex text beside the trigger swatch (default
    /// false). Nothing is rendered while there is no color — the empty swatch already says so.</summary>
    [Parameter] public bool ShowText { get; set; }

    /// <summary>Shows a clear affordance on the trigger while a color is set (default false). Clearing
    /// raises <c>null</c> and leaves the popup's open state untouched.</summary>
    [Parameter] public bool AllowClear { get; set; }

    /// <summary>An optional row of clickable preset swatches in the popup. Each entry is color text in
    /// any form <see cref="Value"/> accepts; an unparseable entry renders as the empty indicator and is
    /// inert. Grouped/collapsible preset sections are out of scope.</summary>
    [Parameter] public IReadOnlyList<string>? Presets { get; set; }

    /// <summary>Accessible name of the preset row (default "Presets").</summary>
    [Parameter] public string PresetsLabel { get; set; } = "Presets";

    /// <summary>Preferred side of the trigger for the popup; flips/shifts to stay within the viewport.
    /// Defaults to <see cref="PopupPlacement.Bottom"/>.</summary>
    [Parameter] public PopupPlacement Placement { get; set; } = PopupPlacement.Bottom;

    /// <summary>Id (and <c>data-test-id</c>) of the trigger button — the element a
    /// <c>&lt;label for="…"&gt;</c> associates with.</summary>
    [Parameter] public string? Id { get; set; }

    // ----- Localizable accessible names --------------------------------------

    /// <summary>Base accessible name of the trigger button (default "Color"). The rendered
    /// <c>aria-label</c> appends the current value — e.g. "Color: #ff0000" — so the value is announced
    /// too; <see cref="EmptyLabel"/> stands in when there is none.</summary>
    [Parameter] public string TriggerLabel { get; set; } = "Color";
    /// <summary>Text appended to <see cref="TriggerLabel"/> while no color is set (default "no color").</summary>
    [Parameter] public string EmptyLabel { get; set; } = "no color";
    /// <summary>Accessible name of the popup dialog (default "Choose color").</summary>
    [Parameter] public string PanelLabel { get; set; } = "Choose color";
    /// <summary>Accessible name of the 2D saturation/brightness area (default "Saturation and brightness").</summary>
    [Parameter] public string SaturationLabel { get; set; } = "Saturation and brightness";
    /// <summary>Format string for the 2D area's <c>aria-valuetext</c>; <c>{0}</c> is the saturation
    /// percentage and <c>{1}</c> the brightness percentage (default "Saturation {0}%, brightness {1}%").
    /// A single-axis <c>aria-valuenow</c> can't describe a 2D handle, which is why this exists.</summary>
    [Parameter] public string SaturationValueTextFormat { get; set; } = "Saturation {0}%, brightness {1}%";
    /// <summary>Accessible name of the hue slider (default "Hue").</summary>
    [Parameter] public string HueLabel { get; set; } = "Hue";
    /// <summary>Accessible name of the alpha slider (default "Opacity").</summary>
    [Parameter] public string AlphaLabel { get; set; } = "Opacity";
    /// <summary>Accessible name of the clear button (default "Clear color").</summary>
    [Parameter] public string ClearLabel { get; set; } = "Clear color";
    /// <summary>Accessible name of the HEX/RGB format select (default "Color format").</summary>
    [Parameter] public string FormatLabel { get; set; } = "Color format";
    /// <summary>Accessible name of the HEX text input (default "Hex").</summary>
    [Parameter] public string HexLabel { get; set; } = "Hex";
    /// <summary>Accessible name of the RGB row's red input (default "Red").</summary>
    [Parameter] public string RedLabel { get; set; } = "Red";
    /// <summary>Accessible name of the RGB row's green input (default "Green").</summary>
    [Parameter] public string GreenLabel { get; set; } = "Green";
    /// <summary>Accessible name of the RGB row's blue input (default "Blue").</summary>
    [Parameter] public string BlueLabel { get; set; } = "Blue";
    /// <summary>Accessible name of the RGB row's alpha (percentage) input (default "Alpha percent").</summary>
    [Parameter] public string AlphaPercentLabel { get; set; } = "Alpha percent";

    // ----- Validation-state ARIA, forwarded by EditColor ---------------------
    // Same forwarding shape DatePicker offers EditDate: the wrapper's validation state has to land on
    // the actual focusable trigger, not on the outer wrapper the consumer's splat goes to.

    /// <summary><c>aria-required</c> for the trigger button, forwarded by <see cref="EditColor"/>.</summary>
    [Parameter] public string? AriaRequired { get; set; }
    /// <summary>Renders <c>aria-invalid="true"</c> on the trigger button, forwarded by <see cref="EditColor"/>.</summary>
    [Parameter] public bool AriaInvalid { get; set; }
    /// <summary><c>aria-describedby</c> for the trigger button, forwarded by <see cref="EditColor"/>.</summary>
    [Parameter] public string? AriaDescribedBy { get; set; }
    /// <summary><c>aria-errormessage</c> for the trigger button, forwarded by <see cref="EditColor"/>.</summary>
    [Parameter] public string? AriaErrorMessage { get; set; }

    /// <summary>
    /// Unmatched attributes (a consumer's <c>class</c>, <c>style</c>, <c>data-*</c>, …), applied to the
    /// outer <c>.wss-color-picker</c> wrapper — never the popup, whose inline placement is JS-owned, and
    /// never a track, whose inline background is C#-owned. <c>class</c>/<c>style</c> merge with the
    /// component's own; the rest are splatted verbatim.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ----- Track geometry -----------------------------------------------------

    /// <summary>
    /// Width in px of all three tracks, mirroring <c>--wss-color-picker-width</c> in
    /// <c>wss-controls.css</c>. Only the no-JS click fallback reads it: <see cref="MouseEventArgs"/>
    /// carries an <c>OffsetX</c> in px but no element size, so normalizing needs an assumed width. A
    /// consumer who overrides the token therefore gets a proportionally-off no-JS click; the normal
    /// (JS) path measures the real element and is unaffected. Keep the two in sync.
    /// </summary>
    internal const double TrackWidth = 234d;
    /// <summary>Height in px of the 2D area, mirroring <c>--wss-color-picker-sv-height</c>. Same
    /// no-JS-fallback-only contract as <see cref="TrackWidth"/>.</summary>
    internal const double SvHeight = 140d;

    // ----- Session state -----------------------------------------------------

    // The live hue/saturation/value. Seeded to pure red so an empty picker's first drag produces a
    // visible color rather than starting from black.
    ColorMath.Hsv _hsv = new(0d, 1d, 1d);
    double _alpha = 1d;
    // False when Value is null/empty/unparseable -- the trigger then shows the empty indicator and the
    // panel still operates (the first interaction commits a color).
    bool _hasColor;
    // The last Value this component observed OR itself emitted -- mirrors Popover's _lastVisibleParam.
    // Comparing against this (rather than re-deriving every render) is what keeps the session hue alive
    // across the re-render our own commit causes; see the class remarks.
    string? _lastValueParam;
    ColorFormat _format = ColorFormat.Hex;
    // Raw text of the HEX input and the four RGB row inputs, updated on every keystroke. Kept as state
    // (rather than rendering straight from the color) so a rejected/clamped entry actually refreshes:
    // Blazor only writes an attribute the diff sees CHANGE, and "300" -> clamped 255 is a change only
    // if the previous render also said "300". Same reason DatePicker keeps its own _edit text.
    string _hexEdit = string.Empty;
    readonly string[] _channelEdit = ["0", "0", "0", "100"];

    ElementReference _svRef;
    ElementReference _hueRef;
    ElementReference _alphaRef;
    ElementReference _svSignal;
    ElementReference _hueSignal;
    ElementReference _alphaSignal;
    ElementReference _hexRef;

    readonly JsModule _colorModule = new("wss-color.js");
    // Instance-unique prefix for the panel's internal ARIA references (the preset row's
    // aria-labelledby), so two pickers on one page can't collide -- same shape as Popover's own _id.
    readonly string _id = $"wss-color-picker-{Guid.NewGuid():N}";
    // Whether wss-color.js is driving the tracks. Gates the @onclick fallback off (a pointerdown-driven
    // drag already reported that press; per the Pointer Events spec a click still fires afterwards) and
    // is reset on close, because the next open renders brand-new track elements to wire.
    bool _dragWired;
    bool _hexWired;

    protected override string PlacementName => Placement.ToString().ToLowerInvariant();
    protected override string PanelClassPrefix => "wss-color-picker";
    protected override bool TriggerDisabled => Disabled;

    /// <summary>
    /// No-op: <c>ColorPicker</c> is deliberately uncontrolled. <see cref="Popover"/>/
    /// <see cref="Popconfirm"/> expose <c>Visible</c>/<c>VisibleChanged</c> because a consumer drives
    /// them from elsewhere on the page; a color picker's popup is only ever opened by its own trigger,
    /// and a controlled open is the exact shape that has previously let external state bypass
    /// <see cref="Disabled"/>. There is no such surface here — every open path routes through
    /// <see cref="ToggleAsync"/>'s guard.
    /// </summary>
    protected override Task InvokeVisibleChangedAsync(bool open) => Task.CompletedTask;

    /// <summary>Focuses the 2D area rather than the panel itself, so the popup is keyboard-operable the
    /// moment it opens (and the first Tab moves on to the hue slider).</summary>
    protected override async Task FocusPanelAsync()
    {
        try { await _svRef.FocusAsync(); } catch { /* not focusable yet (prerender/tests) */ }
    }

    /// <inheritdoc/>
    protected override Task ToggleAsync() => Disabled ? Task.CompletedTask : base.ToggleAsync();

    protected override async Task OnParametersSetAsync()
    {
        if (!string.Equals(Value, _lastValueParam, StringComparison.Ordinal))
        {
            _lastValueParam = Value;
            SyncFromValue();
        }

        // Disabled flipped on while the popup was open -- close it rather than leaving a live panel
        // over a disabled trigger. CloseAsync (not the base's) so any subclass guard still applies.
        if (Disabled && _open) await CloseAsync();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Placement, trigger ARIA, focus-in and focus-restore-on-close all live in the base.
        await base.OnAfterRenderAsync(firstRender);
        if (_disposed) return;

        if (!_open)
        {
            // The panel subtree unmounted; the next open renders new elements that need wiring again.
            _dragWired = false;
            _hexWired = false;
            return;
        }

        if (!_dragWired)
        {
            var module = await _colorModule.GetAsync(JS, FormDefaults);
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("initTrack", _svRef, _svSignal);
                    await module.InvokeVoidAsync("initTrack", _hueRef, _hueSignal);
                    // Gated, not passed as a default ElementReference: the alpha track only exists
                    // while ShowAlpha is on, and an unset reference is not serializable.
                    if (ShowAlpha) await module.InvokeVoidAsync("initTrack", _alphaRef, _alphaSignal);
                    _dragWired = true; // latch on success only, so a failed import retries next render
                }
                catch
                {
                    // No JS -- the @onclick fallback and the keyboard path cover every track.
                }
            }
        }

        if (!_hexWired && _format == ColorFormat.Hex)
        {
            var module = await _colorModule.GetAsync(JS, FormDefaults);
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("initTextInput", _hexRef);
                    _hexWired = true;
                }
                catch
                {
                    // No JS -- Enter in the HEX input may also submit an enclosing form (the same
                    // documented degrade the date pickers' own initPicker wiring carries).
                }
            }
        }
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _colorModule.DisposeAsync();
    }

    // ----- Derived render state ----------------------------------------------

    ColorMath.Rgba CurrentColor => ColorMath.FromHsv(_hsv, _alpha);

    /// <summary>The normalized text for the current color, or empty while there is none.</summary>
    string DisplayHex => _hasColor ? ColorMath.ToHex(CurrentColor, ShowAlpha) : string.Empty;

    // Fully-saturated, full-brightness version of the live hue -- the 2D area's background color, over
    // which the white/black gradients paint.
    string HueBaseHex => ColorMath.ToHex(ColorMath.FromHsv(new ColorMath.Hsv(_hsv.H, 1d, 1d), 1d), false);

    // The alpha track's gradient runs from fully transparent to the current color at full opacity.
    string AlphaGradientHex => ColorMath.ToHex(ColorMath.FromHsv(_hsv, 1d), false);

    // The 2D handle's own fill: the opaque form of wherever it currently sits (a translucent fill over
    // the gradient it floats on would read as a different color than the one being picked). Shown even
    // while there is no committed color -- the handle's position already promises that color.
    string SvHandleHex => ColorMath.ToHex(ColorMath.FromHsv(_hsv, 1d), false);

    string TriggerAriaLabel => $"{TriggerLabel}: {(_hasColor ? DisplayHex : EmptyLabel)}";

    bool ShowClear => AllowClear && _hasColor && !Disabled;

    int SaturationPercent => (int)Math.Round(_hsv.S * 100d);
    int BrightnessPercent => (int)Math.Round(_hsv.V * 100d);
    int HueDegrees => (int)Math.Round(_hsv.H);
    int AlphaPercent => (int)Math.Round(_alpha * 100d);

    string SvValueText => string.Format(CultureInfo.CurrentCulture, SaturationValueTextFormat,
        SaturationPercent.ToString(CultureInfo.CurrentCulture), BrightnessPercent.ToString(CultureInfo.CurrentCulture));

    // Degrees/percent need no localized wording, unlike the 2D area's two-axis text above.
    string HueValueText => $"{HueDegrees.ToString(CultureInfo.CurrentCulture)}°";
    string AlphaValueText => $"{AlphaPercent.ToString(CultureInfo.CurrentCulture)}%";

    // Handle offsets and the presets' fill are inline styles because they are per-value, not per-state.
    // Deliberately PHYSICAL (left/top, not the inset-inline logical pair): the gradients they sit on
    // paint left-to-right in both writing directions, and wss-color.js normalizes a pointer against the
    // physical getBoundingClientRect -- a mirrored handle would disagree with both. See the RTL note in
    // wss-controls.css.
    static string Percent(double normalized) =>
        (Math.Clamp(normalized, 0d, 1d) * 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    static string SwatchStyle(ColorMath.Rgba color) =>
        $"background-color:{ColorMath.ToRgbString(color, true)};";

    // A preset entry that isn't parseable renders as the empty indicator instead of an arbitrary color.
    static bool TryPreset(string? preset, out ColorMath.Rgba color) => ColorMath.TryParse(preset, out color);

    // ----- Value plumbing ----------------------------------------------------

    void SyncFromValue()
    {
        if (ColorMath.TryParse(Value, out var rgba))
        {
            var derived = ColorMath.ToHsv(rgba);
            // Zero saturation (black/white/grey) carries no hue -- keep the session's own rather than
            // snapping the slider to red. See the class remarks.
            _hsv = derived.S <= 0d ? new ColorMath.Hsv(_hsv.H, derived.S, derived.V) : derived;
            _alpha = ShowAlpha ? rgba.A : 1d;
            _hasColor = true;
        }
        else
        {
            _hsv = new ColorMath.Hsv(0d, 1d, 1d);
            _alpha = 1d;
            _hasColor = false;
        }
        RefreshEditText();
    }

    void RefreshEditText()
    {
        _hexEdit = DisplayHex;
        var color = CurrentColor;
        _channelEdit[0] = color.R.ToString(CultureInfo.InvariantCulture);
        _channelEdit[1] = color.G.ToString(CultureInfo.InvariantCulture);
        _channelEdit[2] = color.B.ToString(CultureInfo.InvariantCulture);
        _channelEdit[3] = AlphaPercent.ToString(CultureInfo.InvariantCulture);
    }

    // The single commit path: every interaction (drag, key, preset, typed entry) lands here, so the
    // emitted text, the in-progress input text, and the "has a color" state can never disagree.
    async Task CommitAsync()
    {
        _hasColor = true;
        RefreshEditText();
        var hex = DisplayHex;
        if (string.Equals(hex, _lastValueParam, StringComparison.Ordinal)) return;
        _lastValueParam = hex;
        if (ValueChanged.HasDelegate) await ValueChanged.InvokeAsync(hex);
    }

    // Adopts an RGB color into the HSV session (keeping the session hue for an achromatic one, same
    // rule as SyncFromValue) and commits. Shared by the preset row and the RGB input row.
    Task AdoptAsync(ColorMath.Rgba rgba)
    {
        var derived = ColorMath.ToHsv(rgba);
        _hsv = derived.S <= 0d ? new ColorMath.Hsv(_hsv.H, derived.S, derived.V) : derived;
        if (ShowAlpha) _alpha = rgba.A;
        return CommitAsync();
    }

    Task SetSvAsync(double saturation, double value)
    {
        if (Disabled) return Task.CompletedTask;
        _hsv = new ColorMath.Hsv(_hsv.H, Math.Clamp(saturation, 0d, 1d), Math.Clamp(value, 0d, 1d));
        return CommitAsync();
    }

    Task SetHueAsync(double degrees)
    {
        if (Disabled) return Task.CompletedTask;
        _hsv = new ColorMath.Hsv(Math.Clamp(degrees, 0d, 360d), _hsv.S, _hsv.V);
        return CommitAsync();
    }

    Task SetAlphaAsync(double alpha)
    {
        if (Disabled || !ShowAlpha) return Task.CompletedTask;
        _alpha = Math.Clamp(alpha, 0d, 1d);
        return CommitAsync();
    }

    async Task ClearAsync()
    {
        // ShowClear already requires a color and an enabled control; re-checked so a programmatic /
        // synthesized activation can't bypass either.
        if (Disabled || !_hasColor) return;
        _hasColor = false;
        _hsv = new ColorMath.Hsv(0d, 1d, 1d);
        _alpha = 1d;
        RefreshEditText();
        _lastValueParam = null;
        if (ValueChanged.HasDelegate) await ValueChanged.InvokeAsync(null);
    }

    Task OnPresetClickAsync(string preset)
    {
        if (Disabled || !ColorMath.TryParse(preset, out var rgba)) return Task.CompletedTask;
        return AdoptAsync(rgba);
    }

    // ----- Pointer-drag reports (via the hidden signal inputs) ---------------
    // wss-color.js writes "x,y" (both 0..1 of the track's own box) into a hidden input and dispatches a
    // bubbling input event; Blazor's own delegated listener turns that into these handlers. See the
    // module header for why the reports come back through the DOM rather than a DotNetObjectReference.

    Task OnSvSignalAsync(ChangeEventArgs e) =>
        TryReadSignal(e, out var x, out var y) ? SetSvAsync(x, 1d - y) : Task.CompletedTask;

    Task OnHueSignalAsync(ChangeEventArgs e) =>
        TryReadSignal(e, out var x, out _) ? SetHueAsync(x * 360d) : Task.CompletedTask;

    Task OnAlphaSignalAsync(ChangeEventArgs e) =>
        TryReadSignal(e, out var x, out _) ? SetAlphaAsync(x) : Task.CompletedTask;

    static bool TryReadSignal(ChangeEventArgs e, out double x, out double y)
    {
        x = 0d;
        y = 0d;
        if (e.Value?.ToString() is not { Length: > 0 } text) return false;
        var comma = text.IndexOf(',');
        if (comma < 0) return false;
        return double.TryParse(text.AsSpan(0, comma), NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
               double.TryParse(text.AsSpan(comma + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }

    // ----- No-JS click fallbacks ---------------------------------------------
    // OffsetX/OffsetY are relative to the target's padding box, and the handles are pointer-events:none
    // in CSS precisely so the track is always the target. Inert once wss-color.js is driving the track
    // (see _dragWired).

    Task OnSvClickAsync(MouseEventArgs e) =>
        _dragWired ? Task.CompletedTask : SetSvAsync(e.OffsetX / TrackWidth, 1d - e.OffsetY / SvHeight);

    Task OnHueClickAsync(MouseEventArgs e) =>
        _dragWired ? Task.CompletedTask : SetHueAsync(e.OffsetX / TrackWidth * 360d);

    Task OnAlphaClickAsync(MouseEventArgs e) =>
        _dragWired ? Task.CompletedTask : SetAlphaAsync(e.OffsetX / TrackWidth);

    // ----- Keyboard ----------------------------------------------------------
    // Pure C#, no JS at all -- wss-color.js only suppresses the native page scroll for these same keys
    // (Blazor has no per-key preventDefault, and an unconditional one would swallow Tab).

    // Shift and PageUp/PageDown both mean "large step", matching the ARIA slider convention.
    static bool IsLargeStep(KeyboardEventArgs e) => e.ShiftKey || e.Key is "PageUp" or "PageDown";

    Task OnSvKeyDownAsync(KeyboardEventArgs e)
    {
        var step = IsLargeStep(e) ? 0.1d : 0.01d;
        return e.Key switch
        {
            "ArrowLeft" => SetSvAsync(_hsv.S - step, _hsv.V),
            "ArrowRight" => SetSvAsync(_hsv.S + step, _hsv.V),
            "ArrowUp" or "PageUp" => SetSvAsync(_hsv.S, _hsv.V + step),
            "ArrowDown" or "PageDown" => SetSvAsync(_hsv.S, _hsv.V - step),
            _ => Task.CompletedTask
        };
    }

    Task OnHueKeyDownAsync(KeyboardEventArgs e)
    {
        var step = IsLargeStep(e) ? 10d : 1d;
        return e.Key switch
        {
            "ArrowLeft" or "ArrowDown" or "PageDown" => SetHueAsync(_hsv.H - step),
            "ArrowRight" or "ArrowUp" or "PageUp" => SetHueAsync(_hsv.H + step),
            "Home" => SetHueAsync(0d),
            "End" => SetHueAsync(360d),
            _ => Task.CompletedTask
        };
    }

    Task OnAlphaKeyDownAsync(KeyboardEventArgs e)
    {
        var step = IsLargeStep(e) ? 0.1d : 0.01d;
        return e.Key switch
        {
            "ArrowLeft" or "ArrowDown" or "PageDown" => SetAlphaAsync(_alpha - step),
            "ArrowRight" or "ArrowUp" or "PageUp" => SetAlphaAsync(_alpha + step),
            "Home" => SetAlphaAsync(0d),
            "End" => SetAlphaAsync(1d),
            _ => Task.CompletedTask
        };
    }

    // Escape must also work once focus has moved into the panel -- the panel is a sibling of the
    // trigger, so a keydown there never bubbles to the trigger's own handler (same as Popover).
    async Task OnPanelKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") await CloseAsync();
    }

    // ----- Typed entry -------------------------------------------------------

    void OnFormatChanged(ChangeEventArgs e)
    {
        // String compare rather than Enum.TryParse: two known values, no reflection, nothing for the
        // trim/AOT analyzers to consider.
        _format = string.Equals(e.Value?.ToString(), nameof(ColorFormat.Rgb), StringComparison.Ordinal)
            ? ColorFormat.Rgb
            : ColorFormat.Hex;
        // The switched-in row renders fresh elements -- the HEX input needs its Enter wiring again.
        _hexWired = false;
        RefreshEditText();
    }

    // Takes the change event's own value rather than reading _hexEdit alone, so a commit is
    // self-sufficient: the per-keystroke @oninput keeps _hexEdit in step for the render diff (see its
    // declaration), but a change event that arrives without one still commits what the box holds.
    async Task CommitHexAsync(ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? string.Empty;
        _hexEdit = text;
        if (string.IsNullOrWhiteSpace(text))
        {
            // An emptied HEX box is a request to clear when clearing is allowed, and otherwise just
            // reverts -- never a parse error.
            if (AllowClear && _hasColor) await ClearAsync();
            else RefreshEditText();
            return;
        }

        if (!ColorMath.TryParse(text, out var rgba))
        {
            RefreshEditText(); // revert the box; the committed value is untouched
            if (OnParseError.HasDelegate) await OnParseError.InvokeAsync(text);
            return;
        }

        await AdoptAsync(rgba);
    }

    // One RGB-row channel: 0/1/2 are R/G/B in 0..255, 3 is alpha as a percentage. A non-numeric entry
    // reverts, an out-of-range one clamps -- see OnParseError for why neither is surfaced. Same
    // event-value-first contract as CommitHexAsync above.
    Task CommitChannelAsync(int index, ChangeEventArgs e)
    {
        _channelEdit[index] = e.Value?.ToString() ?? string.Empty;
        if (!int.TryParse(_channelEdit[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
        {
            RefreshEditText();
            return Task.CompletedTask;
        }

        if (index == 3) return SetAlphaAsync(raw / 100d);

        var color = CurrentColor;
        var channel = (byte)Math.Clamp(raw, 0, 255);
        return AdoptAsync(index switch
        {
            0 => color with { R = channel },
            1 => color with { G = channel },
            _ => color with { B = channel }
        });
    }
}
