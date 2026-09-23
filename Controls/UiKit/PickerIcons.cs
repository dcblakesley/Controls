namespace Controls;

/// <summary>
/// Inline SVG glyphs (AntD icon paths, no icon-font dependency) shared by <see cref="DatePicker"/>
/// and <see cref="DateRangePicker"/>'s markup, plus the glyphs <see cref="Pagination"/>,
/// <see cref="Table{TItem}"/>'s expand chevron and <c>Select</c>'s arrow reuse (see
/// <see cref="NextIcon"/>/<see cref="DownPath"/>) — one definition per glyph, so a coordinate
/// never drifts between two copies of the same icon.
/// </summary>
internal static class PickerIcons
{
    /// <summary>
    /// "DownOutlined" caret path data (16x16 grid, not AntD's 1024-unit icon grid), shared by
    /// <see cref="DownIcon"/>, <see cref="Pagination"/>'s size-changer arrow and <c>Select</c>'s
    /// dropdown arrow — those three need different <c>class</c>/size attributes on the
    /// <c>&lt;svg&gt;</c>, so each wraps this path itself (each also needs the matching
    /// <c>viewBox="0 0 16 16"</c>, since the path is only valid against that grid).
    /// MUST stay a compile-time literal: it is emitted unencoded wherever a <see cref="MarkupString"/>
    /// is built from it, so never route a parameter or model value through it (XSS).
    /// </summary>
    internal const string DownPath = "M13.8125 4H12.6406C12.561 4 12.486 4.03906 12.4391 4.10313L8.00002 10.2219L3.56096 4.10313C3.51408 4.03906 3.43908 4 3.35939 4H2.18752C2.08596 4 2.02658 4.11563 2.08596 4.19844L7.59533 11.7937C7.79533 12.0687 8.20471 12.0687 8.40315 11.7937L13.9125 4.19844C13.9735 4.11563 13.9141 4 13.8125 4V4Z";

    public static readonly MarkupString CalendarIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M880 184H712v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H384v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H144c-17.7 0-32 14.3-32 32v664c0 17.7 14.3 32 32 32h736c17.7 0 32-14.3 32-32V216c0-17.7-14.3-32-32-32zm-40 656H184V460h656v380zM184 392V256h128v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h256v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h128v136H184z\"/></svg>");

    public static readonly MarkupString DownIcon = new(
        $"<svg class=\"wss-picker-select-arrow\" viewBox=\"0 0 16 16\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"{DownPath}\"/></svg>");

    // Prev/next chevrons — the same AntD glyphs Pagination.razor renders for its prev/next buttons
    // and Table.razor for its expand chevron; those consumers need this exact wrapper, so they use
    // these MarkupStrings directly rather than re-wrapping the path.
    public static readonly MarkupString PrevIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M724 218.3V141c0-6.7-7.7-10.4-12.9-6.3L260.3 486.8a31.86 31.86 0 000 50.3l450.8 352.1c5.3 4.1 12.9.4 12.9-6.3v-77.3c0-4.9-2.3-9.6-6.1-12.6l-360-281 360-281.1c3.8-3 6.1-7.7 6.1-12.6z\"/></svg>");

    public static readonly MarkupString NextIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M765.7 486.8L314.9 134.7A7.97 7.97 0 00302 141v77.3c0 4.9 2.3 9.6 6.1 12.6l360 281.1-360 281.1c-3.8 3-6.1 7.7-6.1 12.6V883c0 6.7 7.7 10.4 12.9 6.3l450.8-352.1a31.96 31.96 0 000-50.4z\"/></svg>");

    public static readonly MarkupString SwapRightIcon = new(
        "<svg viewBox=\"0 0 1024 1024\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M873.1 596.2l-164-208A32 32 0 00684 376h-64.8c-6.7 0-10.4 7.7-6.3 13l144.3 183H152c-4.4 0-8 3.6-8 8v60c0 4.4 3.6 8 8 8h695.9c26.8 0 41.7-30.8 25.2-51.8z\"/></svg>");
}
