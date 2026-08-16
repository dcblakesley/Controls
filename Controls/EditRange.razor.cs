using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// Edit control for a numeric value picked from a range — an AntDesign-style horizontal slider
/// (rail, filled track, round handle, optional marks/dots and a value tooltip), named after the
/// native <c>&lt;input type="range"&gt;</c> it replaces. Binds a single value through
/// <c>@bind-Value</c>, exactly like <see cref="EditNumber{T}"/>, whose <c>Min</c>/<c>Max</c>/
/// <c>Step</c> model-attribute resolution it mirrors.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bounds are always finite.</b> Unlike <see cref="EditNumber{T}"/>, whose <c>min</c>/<c>max</c>
/// attributes are simply omitted when nothing resolves, a slider has to know both ends to place its
/// handle at all — so <see cref="EffectiveMin"/>/<see cref="EffectiveMax"/> fall through to 0/100
/// after the parameter and the model's <c>[MinValue]</c>/<c>[MaxValue]</c>/<c>[Range]</c>.
/// </para>
/// <para>
/// <b>The focusable element is the TRACK, not the handle.</b> The handle is decorative
/// (<c>pointer-events: none</c> in CSS, which is load-bearing: the no-JS click fallback reads
/// <see cref="MouseEventArgs.OffsetX"/>, and a hit-testable handle would make itself the event
/// target and report coordinates within its own box). Same shape as the UI-kit
/// <c>ColorPicker</c>'s hue/alpha tracks, which is also the shape an earlier touch-device audit
/// settled on — a tap must land on a focusable element.
/// </para>
/// <para>
/// <b>Every JS-dependent behavior degrades.</b> <c>wss-slider.js</c> supplies pointer dragging (and
/// the per-key <c>preventDefault</c> Blazor cannot express); without it a single click still
/// positions the handle from <see cref="MouseEventArgs.OffsetX"/> and the keyboard still steps, so
/// the control is fully operable under prerender/bUnit — see <see cref="TrackWidth"/> for the one
/// caveat that fallback carries.
/// </para>
/// <para>
/// Arithmetic runs in <see cref="decimal"/> over the boxed value, because <typeparamref name="T"/>
/// carries no numeric constraint (the same reason <see cref="EditNumber{T}"/> switches on the CLR
/// type rather than an interface), and every commit goes through
/// <see cref="InputBase{TValue}.CurrentValueAsString"/> so the decimal-to-<typeparamref name="T"/>
/// conversion, <see cref="ParsingErrorMessage"/> and the field-changed notification all come for
/// free. A fractional <see cref="Step"/> on an integral <typeparamref name="T"/> therefore surfaces
/// as a parse error rather than being silently rounded — a consumer configuration error, and the
/// same contract <see cref="EditNumber{T}"/>'s stepper documents.
/// </para>
/// </remarks>
// T is annotated 'All' because TryParseValueFromString feeds it (via EditControlInit.TryConvert<T>)
// to BindConverter.TryConvertTo<T>, which declares that requirement for its TypeConverter fallback
// (mirrors EditNumber<T> and the framework's own InputNumber<T>).
// IAsyncDisposable is declared in EditRange.razor (@implements), matching EditBool -- the only other
// form control holding a JS module.
public partial class EditRange<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : EditControlBase<T>
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<T>>? Field { get; set; }

    /// <summary>
    /// The low end of the slider. Falls back to the bound property's <c>[MinValue]</c>/<c>[Range]</c>,
    /// then to <c>0</c> — see <see cref="EffectiveMin"/>.
    /// </summary>
    [Parameter] public decimal? Min { get; set; }

    /// <summary>
    /// The high end of the slider. Falls back to the bound property's <c>[MaxValue]</c>/<c>[Range]</c>,
    /// then to <c>100</c> — see <see cref="EffectiveMax"/>.
    /// </summary>
    [Parameter] public decimal? Max { get; set; }

    /// <summary>
    /// The increment a click, drag or arrow key snaps to, anchored at <see cref="EffectiveMin"/>.
    /// Falls back to the bound property's <c>[Step]</c>, then to <c>1</c>. Ignored while
    /// <see cref="SnapToMarks"/> is on with a non-empty <see cref="Marks"/>.
    /// </summary>
    [Parameter] public decimal? Step { get; set; }

    /// <summary>
    /// Labeled points rendered under the rail at their proportional positions. A click on a label
    /// commits that value. The keys are the values; the strings are what's displayed (and, when a
    /// mark is hit exactly, what <c>aria-valuetext</c> announces).
    /// </summary>
    [Parameter] public IReadOnlyDictionary<decimal, string>? Marks { get; set; }

    /// <summary>
    /// Restricts the value to the <see cref="Marks"/> positions: a click/drag snaps to the nearest
    /// mark instead of the nearest <see cref="Step"/> increment, and the arrow keys move between
    /// adjacent marks (PageUp/PageDown by ten of them). No effect without marks.
    /// </summary>
    /// <remarks>
    /// This is the library's spelling of AntD's <c>step={null}</c>, which cannot be expressed here:
    /// <see cref="Step"/> is a <c>decimal?</c> whose null already means "fall back to
    /// <c>[Step]</c>/1", so an explicit null is indistinguishable from an unset parameter. A
    /// separate boolean says the same thing without overloading null.
    /// </remarks>
    [Parameter] public bool SnapToMarks { get; set; }

    /// <summary>
    /// Renders a dot on the rail at every <see cref="Step"/> increment and at every mark. Only
    /// sensible with a coarse step — a step that would produce more than <see cref="MaxDots"/> dots
    /// renders none of the step dots at all (marks still get theirs) rather than filling the rail
    /// with a solid line of overlapping circles. The default step of 1 over the default 0..100 bounds
    /// is already past that cap (101 dots), so <c>Dots</c> without a coarse <see cref="Step"/> draws
    /// the marks alone.
    /// </summary>
    [Parameter] public bool Dots { get; set; }

    /// <summary>
    /// Whether the rail between <see cref="EffectiveMin"/> and the current value renders as a filled
    /// track (and whether dots/marks at or below the value take their "active" styling). AntD's
    /// <c>included</c>: <c>false</c> presents the marks as a set of discrete, independent points
    /// rather than a magnitude.
    /// </summary>
    [Parameter] public bool Included { get; set; } = true;

    /// <summary>
    /// Whether a value bubble renders above the handle. It appears on hover, on keyboard focus, and
    /// for the whole of a drag; it is <c>aria-hidden</c>, since the value already reaches assistive
    /// tech through <c>aria-valuenow</c>/<c>aria-valuetext</c>.
    /// </summary>
    [Parameter] public bool ShowTooltip { get; set; } = true;

    /// <summary>
    /// Optional .NET numeric format string for the value bubble, the <c>aria-valuetext</c> it drives,
    /// and the read-only text (e.g. <c>"C0"</c>, <c>"P0"</c>, <c>"N2"</c>). Falls back to the bound
    /// property's <c>[DisplayFormat(DataFormatString = …)]</c>; null in both leaves the value's own
    /// culture-formatted <c>ToString()</c>, matching <see cref="EditNumber{T}"/>'s read-only view.
    /// </summary>
    [Parameter] public string? TooltipFormat { get; set; }

    /// <summary> Error message format string used when the value can't be parsed. <c>{0}</c> is replaced with the field name.</summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a number.";

    [Inject] IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// Assumed track width in px for the no-JS click fallback only, mirroring
    /// <c>--edit-range-width</c> in <c>edit-controls.css</c>. <see cref="MouseEventArgs"/> carries an
    /// <c>OffsetX</c> in px but no element size, so normalizing a click without JS needs an assumed
    /// width. A consumer who overrides that token (or whose layout shrinks the track past its
    /// <c>max-width: 100%</c>) therefore gets a proportionally-off no-JS click; the normal (JS) path
    /// measures the real element and is unaffected. Keep the two in sync. Same contract as
    /// <c>ColorPicker.TrackWidth</c>.
    /// </summary>
    internal const double TrackWidth = 320d;

    /// <summary> The most step dots this control will draw before dropping them all — see <see cref="Dots"/>.</summary>
    internal const int MaxDots = 100;

    // PageUp/PageDown move ten steps (or ten marks), the ARIA slider pattern's "large step".
    const int LargeStepMultiplier = 10;

    // The numeric type actually bound, with Nullable<T> unwrapped -- so a [Range] bound is only
    // treated as vacuous when it's T's OWN extreme (see Helpers.RangeSentinels).
    static readonly Type UnderlyingNumericType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

    readonly JsModule _sliderModule = new("wss-slider.js");
    ElementReference _trackRef;
    ElementReference _signalRef;
    // Whether wss-slider.js is driving the track. Gates the @onclick fallback off (a pointerdown-
    // driven drag already reported that press; per the Pointer Events spec a click still fires
    // afterwards) and is reset whenever the track leaves the DOM, since a later reappearance renders
    // a brand-new element to wire.
    bool _dragWired;
    // True between a drag's first report and its release report -- the value bubble's C#-owned
    // visible state, for the part of a drag that happens off the track where CSS :hover has stopped
    // applying.
    bool _dragging;
    bool _disposed;

    // Marks, sorted by value, rebuilt only when the parameter's identity changes. Both shapes are
    // kept because the render walks the pairs while the snapping/keyboard math only needs the keys.
    List<KeyValuePair<decimal, string>> _sortedMarks = [];
    List<decimal> _markValues = [];
    IReadOnlyDictionary<decimal, string>? _marksSource;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        // Base first: it re-resolves the element id and the ARIA state (see EditControlBase).
        base.OnParametersSet();
        if (ReferenceEquals(Marks, _marksSource)) return;
        _marksSource = Marks;
        _sortedMarks = Marks is null ? [] : Marks.OrderBy(kv => kv.Key).ToList();
        _markValues = _sortedMarks.Select(kv => kv.Key).ToList();
    }

    // ----- Resolved configuration --------------------------------------------

    /// <summary>
    /// The low bound actually used: the <see cref="Min"/> parameter, else the model property's
    /// <c>[MinValue]</c>/<c>[Range]</c> lower bound, else 0. See the class remarks for why this
    /// (unlike <see cref="EditNumber{T}"/>'s) can't stay null.
    /// </summary>
    decimal EffectiveMin => Min ?? _attributes.MinNumber(UnderlyingNumericType) ?? 0m;

    /// <summary> The high bound actually used: <see cref="Max"/>, else <c>[MaxValue]</c>/<c>[Range]</c>, else 100. See <see cref="EffectiveMin"/>.</summary>
    decimal EffectiveMax => Max ?? _attributes.MaxNumber(UnderlyingNumericType) ?? 100m;

    /// <summary>
    /// The increment actually applied: the explicitly-configured step (<see cref="Step"/>, else the
    /// model property's <c>[Step]</c>) when it's positive, otherwise 1. A non-positive step falls
    /// back for the same reason <see cref="EditNumber{T}"/>'s stepper does — snapping to a zero
    /// increment divides by zero, and a negative one inverts every gesture.
    /// </summary>
    decimal EffectiveStep => (Step ?? _attributes.Step()) is { } step && step > 0m ? step : 1m;

    /// <summary> The format applied to the bubble, <c>aria-valuetext</c> and the read-only text: <see cref="TooltipFormat"/>, else the model property's <c>[DisplayFormat]</c>.</summary>
    string? EffectiveFormat => TooltipFormat ?? _attributes.FormatString();

    // Snapping to marks needs marks to snap to -- both the parameter and the resolved list are
    // checked at every site through this one property.
    bool UseMarkSnapping => SnapToMarks && _markValues.Count > 0;

    // ----- Value math (decimal throughout; T carries no numeric constraint) ---

    /// <summary>
    /// The bound value as a decimal, or <see cref="EffectiveMin"/> when there is none (or it doesn't
    /// convert). A null value therefore renders its handle at the low end and reports
    /// <c>aria-valuenow</c> there, without committing anything — the first interaction does that.
    /// </summary>
    decimal CurrentDecimal => TryGetDecimal(CurrentValue, out var value) ? value : EffectiveMin;

    /// <summary> <see cref="CurrentDecimal"/> confined to the bounds, which is what the handle, the fill and the ARIA state all report.</summary>
    decimal ClampedValue => Clamp(CurrentDecimal);

    /// <summary> Where the handle sits, as 0..1 of the rail.</summary>
    decimal Fraction => FractionOf(ClampedValue);

    decimal FractionOf(decimal value)
    {
        try
        {
            var min = EffectiveMin;
            var max = EffectiveMax;
            if (max <= min) return 0m; // degenerate/inverted bounds: everything sits at the low end
            var fraction = (value - min) / (max - min);
            return fraction < 0m ? 0m : fraction > 1m ? 1m : fraction;
        }
        catch (OverflowException)
        {
            // A span wider than decimal can represent (a consumer's decimal.MinValue..MaxValue).
            return 0m;
        }
    }

    // The inverse: a normalized 0..1 position back to a value. Used by both the drag channel and the
    // no-JS click fallback.
    decimal ValueAt(double fraction)
    {
        var min = EffectiveMin;
        var max = EffectiveMax;
        if (max <= min) return min;
        var clamped = fraction < 0d ? 0d : fraction > 1d ? 1d : fraction;
        try { return min + (max - min) * (decimal)clamped; }
        catch (OverflowException) { return min; }
    }

    decimal Clamp(decimal value)
    {
        var min = EffectiveMin;
        var max = EffectiveMax;
        if (max <= min) return min;
        return value < min ? min : value > max ? max : value;
    }

    // The nearest legal position: the nearest mark under SnapToMarks, otherwise the nearest step
    // increment measured FROM EffectiveMin (not from zero -- a Min of 5 with a step of 10 offers
    // 5/15/25, not 0/10/20).
    decimal Snap(decimal value)
    {
        if (UseMarkSnapping) return NearestMark(value);
        try
        {
            var min = EffectiveMin;
            var step = EffectiveStep;
            var steps = Math.Round((value - min) / step, MidpointRounding.AwayFromZero);
            return min + steps * step;
        }
        catch (OverflowException)
        {
            return value;
        }
    }

    decimal NearestMark(decimal value)
    {
        var nearest = _markValues[0];
        var bestDistance = decimal.MaxValue;
        foreach (var mark in _markValues)
        {
            decimal distance;
            try { distance = Math.Abs(mark - value); }
            catch (OverflowException) { continue; }
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = mark;
        }
        return nearest;
    }

    // The mark `count` positions away in `direction`, for the keyboard path under SnapToMarks. Walks
    // rather than indexing, so a current value that sits BETWEEN two marks moves to the next one
    // past it instead of to whichever it happens to be nearest.
    decimal AdjacentMark(decimal value, int direction, int count)
    {
        var current = value;
        for (var i = 0; i < count; i++)
        {
            var next = direction > 0
                ? _markValues.FirstOrDefault(m => m > current, _markValues[^1])
                : _markValues.LastOrDefault(m => m < current, _markValues[0]);
            if (next == current) break; // already at that end
            current = next;
        }
        return current;
    }

    /// <summary>
    /// Snaps, clamps and commits through <see cref="InputBase{TValue}.CurrentValueAsString"/> — the
    /// same parse/validate/notify path a typed entry takes. A commit equal to what is already bound
    /// is dropped: a drag reports on every animation frame, and each redundant commit would cost a
    /// render (plus a network round trip on Blazor Server).
    /// </summary>
    void Commit(decimal value)
    {
        if (IsDisabled || !ShowEditor) return;
        var next = Clamp(Snap(value));
        if (CurrentValue is not null && TryGetDecimal(CurrentValue, out var current) && current == next) return;
        CurrentValueAsString = ToCommitText(next);
    }

    /// <summary>
    /// The invariant text a commit parses back from. Trailing zeros are formatted away deliberately:
    /// a <c>Step</c> written as <c>1.0m</c> makes the arithmetic produce <c>40.0</c>, whose default
    /// <c>ToString</c> an integral <typeparamref name="T"/> can't parse — so a purely cosmetic
    /// difference in how the consumer spelled their step would raise
    /// <see cref="ParsingErrorMessage"/> on an otherwise valid whole number.
    /// </summary>
    static string ToCommitText(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    // CurrentValue (or any boxed numeric) as a decimal. False rather than throwing for the three
    // ways the conversion can fail -- a double/float outside decimal's range (OverflowException), a
    // T that isn't numeric at all (InvalidCastException), and an unparseable string-like T
    // (FormatException). EditNumber's own equivalent only guards the first, because its callers
    // reach it exclusively from arithmetic on an already-numeric value; this one is also on the
    // render path (IsValueDefault, the handle position), where a throw would take the whole form
    // down instead of degrading to a handle at the low end.
    static bool TryGetDecimal(object? value, out decimal result)
    {
        result = 0m;
        if (value is null) return false;
        try
        {
            result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException) { return false; }
        catch (InvalidCastException) { return false; }
        catch (FormatException) { return false; }
    }

    // ----- Rendering helpers -------------------------------------------------

    // A CSS percentage from a 0..1 fraction. Four decimals is well past what a sub-pixel position
    // can express and keeps the rendered markup stable for the visual baselines.
    static string Percent(decimal fraction) =>
        (fraction * 100m).ToString("0.####", CultureInfo.InvariantCulture) + "%";

    string RangeClass => IsDisabled ? "edit-range edit-range-disabled" : "edit-range";

    // The consumer's `class` travels CssClass onto the field element, which for this control is the
    // track -- the same element that carries the EditContext's modified/valid/invalid state classes,
    // so the CSS can key its invalid styling off `.edit-range-track.invalid`.
    string TrackClass => $"edit-range-track {CssClass}".TrimEnd();

    /// <summary> The value bubble's text (and the read-only text), formatted through <see cref="EffectiveFormat"/>.</summary>
    string FormattedValue => FormatValue(ClampedValue);

    string FormatValue(decimal value)
    {
        if (EffectiveFormat is { Length: > 0 } format)
        {
            try { return value.ToString(format, CultureInfo.CurrentCulture); }
            catch (FormatException) { /* invalid custom format -- fall through to the plain rendering */ }
        }
        return value.ToString(CultureInfo.CurrentCulture);
    }

    // Read-only text: the bound value as the consumer would read it, NOT the clamped one -- a value
    // outside the bounds is still what the model holds, and read-only mode reports rather than
    // corrects. Empty for no value at all, which is ReadOnlyValue's "Not Set" branch.
    string? ReadOnlyText =>
        CurrentValue is null ? string.Empty
        : TryGetDecimal(CurrentValue, out var value) ? FormatValue(value)
        : CurrentValue.ToString();

    /// <summary>
    /// <c>aria-valuenow</c> — always a bare number, per the ARIA slider pattern, and invariant so it
    /// stays machine-readable under any culture.
    /// </summary>
    string AriaValueNow => ToCommitText(ClampedValue);

    /// <summary>
    /// <c>aria-valuetext</c>, or null when the raw number already reads correctly (in which case the
    /// attribute is omitted rather than duplicating <c>aria-valuenow</c>). Two cases need it: a
    /// <see cref="TooltipFormat"/> that changes the human reading ("$40", "40%"), and a value that
    /// lands exactly on a labeled mark, whose label is what a sighted user sees.
    /// </summary>
    string? AriaValueText
    {
        get
        {
            if (EffectiveFormat is { Length: > 0 }) return FormattedValue;
            var value = ClampedValue;
            foreach (var mark in _sortedMarks)
                if (mark.Key == value) return mark.Value;
            return null;
        }
    }

    /// <summary>
    /// Every dot to draw: one per <see cref="Step"/> increment plus one per mark, deduplicated and
    /// ordered. Empty unless <see cref="Dots"/> is on; the step dots alone are dropped when there
    /// would be more than <see cref="MaxDots"/> of them.
    /// </summary>
    List<decimal> DotValues()
    {
        var dots = new List<decimal>();
        if (!Dots) return dots;

        var min = EffectiveMin;
        var max = EffectiveMax;
        if (max > min)
        {
            try
            {
                // +1 because both ends get a dot: a span of 100 at a step of 1 is 101 dots, not 100.
                if ((max - min) / EffectiveStep + 1m <= MaxDots)
                    for (var value = min; value <= max; value += EffectiveStep)
                        dots.Add(value);
            }
            catch (OverflowException)
            {
                // A span/step pair whose ratio decimal can't hold -- far past MaxDots either way.
            }
        }

        foreach (var mark in _markValues)
            if (mark >= min && mark <= max && !dots.Contains(mark))
                dots.Add(mark);

        dots.Sort();
        return dots;
    }

    // "At or below the current value" -- the active styling only means anything while the fill it
    // matches is being drawn, so Included=false leaves every dot/mark inactive.
    bool IsActive(decimal value) => Included && value <= ClampedValue;

    // ----- Interaction -------------------------------------------------------

    /// <summary>
    /// The no-JS click fallback. <see cref="MouseEventArgs.OffsetX"/> is relative to the target's
    /// padding box, and the handle is <c>pointer-events: none</c> in CSS precisely so the track is
    /// always that target. Inert once <c>wss-slider.js</c> is driving the track (see
    /// <see cref="_dragWired"/>), whose pointerdown already reported the same press.
    /// </summary>
    void OnTrackClick(MouseEventArgs e)
    {
        if (_dragWired) return;
        Commit(ValueAt(e.OffsetX / TrackWidth));
    }

    /// <summary>
    /// The drag channel: <c>wss-slider.js</c> writes <c>"x,pressed"</c> into a hidden input and
    /// dispatches a bubbling <c>input</c> event, which Blazor delivers here. See that module's header
    /// for why the reports come back through the DOM rather than a <c>DotNetObjectReference</c>.
    /// </summary>
    void OnDragSignal(ChangeEventArgs e)
    {
        if (e.Value?.ToString() is not { Length: > 0 } text) return;
        var comma = text.IndexOf(',');
        if (comma < 0) return;
        if (!double.TryParse(text.AsSpan(0, comma), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            double.IsNaN(x))
            return;

        // The release report ends the gesture without moving anything: its position was already
        // committed by the last move, and re-committing it would only cost a render.
        var pressed = text.EndsWith(",1", StringComparison.Ordinal);
        _dragging = pressed && !IsDisabled;
        if (pressed) Commit(ValueAt(x));
    }

    /// <summary>
    /// Keyboard stepping, pure C# — <c>wss-slider.js</c> only suppresses the native page scroll for
    /// these same keys (Blazor has no per-key <c>preventDefault</c>, and an unconditional one would
    /// swallow Tab). Arrow keys move one step (one mark under <see cref="SnapToMarks"/>),
    /// PageUp/PageDown ten, Home/End jump to the bounds — everything clamped by
    /// <see cref="Commit"/>.
    /// </summary>
    void OnKeyDown(KeyboardEventArgs e)
    {
        if (IsDisabled) return;
        switch (e.Key)
        {
            case "ArrowRight" or "ArrowUp": MoveBy(1, 1); break;
            case "ArrowLeft" or "ArrowDown": MoveBy(-1, 1); break;
            case "PageUp": MoveBy(1, LargeStepMultiplier); break;
            case "PageDown": MoveBy(-1, LargeStepMultiplier); break;
            case "Home": Commit(EffectiveMin); break;
            case "End": Commit(EffectiveMax); break;
        }
    }

    void MoveBy(int direction, int multiplier)
    {
        var current = ClampedValue;
        if (UseMarkSnapping)
        {
            Commit(AdjacentMark(current, direction, multiplier));
            return;
        }
        try
        {
            Commit(current + direction * multiplier * EffectiveStep);
        }
        catch (OverflowException)
        {
            // Stepping past decimal's own range -- the bound in that direction is where it was going.
            Commit(direction > 0 ? EffectiveMax : EffectiveMin);
        }
    }

    // A mark label is a click target of its own (the marks row sits outside the track, so its clicks
    // never reach the track's own handler). Method rather than an inline lambda per mark so the
    // disabled guard lives in exactly one place -- Commit's.
    void OnMarkClick(decimal value) => Commit(value);

    // ----- Framework plumbing ------------------------------------------------

    // Ported from EditNumber<T> -- the shared body in EditControlInit.TryConvert. BindConverter
    // handles every numeric primitive (int, long, short, sbyte, byte, decimal, float, double, plus
    // their unsigned + nullable variants).
    protected override bool TryParseValueFromString(string? value, out T result, out string validationErrorMessage) =>
        EditControlInit.TryConvert(value, ParsingErrorMessage, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    // Invariant, so the value this control echoes (BoundValueDisplay) and any round trip through
    // CurrentValueAsString agree with the invariant text every commit writes -- a culture with a
    // comma decimal separator would otherwise produce text TryParseValueFromString rejects.
    protected override string? FormatValueAsString(T? value) =>
        value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

    // Numeric zero counts as "default" for the NullOrDefault hiding modes -- same rule as
    // EditNumber's, through this control's non-throwing conversion (see TryGetDecimal).
    // CurrentValue is guaranteed non-null here: the base method handles the null branch.
    protected override bool IsValueDefault() => TryGetDecimal(CurrentValue, out var value) && value == 0m;

    // True while an actual track element is in the DOM -- mirrors the @if pair in EditRange.razor.
    bool TrackRendered => ShouldShowComponent() && ShowEditor;

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_disposed) return;

        if (!TrackRendered)
        {
            // The editor unmounted (a read-only/hiding toggle); a later reappearance renders a new
            // element that needs wiring again, and until then the @onclick fallback is the only path.
            _dragWired = false;
            return;
        }

        if (_dragWired) return;
        // Null = no JS runtime / module (server prerender, tests), or disposed while the import was
        // in flight. Either way the latch stays off, so a later render retries and the click +
        // keyboard paths carry the control on their own.
        var module = await _sliderModule.GetAsync(JS, FormDefaults);
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("initSlider", _trackRef, _signalRef);
            _dragWired = true; // latch on success only, so a failed import retries next render
        }
        catch
        {
            // Element gone / circuit dropped mid-call -- same fallback, same retry.
        }
    }

    /// <summary>
    /// Releases the JS module. Blazor treats <see cref="IAsyncDisposable"/> and
    /// <see cref="IDisposable"/> as mutually exclusive — a component implementing this interface has
    /// its <c>DisposeAsync</c> awaited and its <c>IDisposable.Dispose</c> never called — so the
    /// synchronous half (InputBase's EditContext unsubscribe plus
    /// <see cref="EditControlBase{TValue}.Dispose(bool)"/>'s field unregistration) has to be chained
    /// by hand. Same shape, same reason, as <see cref="EditBool"/>'s.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        ((IDisposable)this).Dispose();
        await _sliderModule.DisposeAsync();
    }
}
