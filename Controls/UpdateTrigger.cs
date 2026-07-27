namespace Controls;

/// <summary>
/// Controls which DOM event commits an edit control's bound value. Can be set per-control or
/// globally via <see cref="FormDefaults.UpdateOn"/>. <see cref="Change"/> means fewer render
/// cycles — and, on Blazor Server, fewer round-trips to the server — since the value only commits
/// on blur/Enter; <see cref="Input"/> costs a render per keystroke but gives per-keystroke
/// validation feedback (e.g. a required-field error clearing as soon as the user starts typing).
/// </summary>
public enum UpdateTrigger
{
    /// <summary> Commit on every keystroke (DOM <c>oninput</c>). </summary>
    Input,

    /// <summary> Commit only when the value changes and is committed — blur or Enter (DOM <c>onchange</c>). </summary>
    Change
}
