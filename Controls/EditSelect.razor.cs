namespace Controls;

/// <summary>
/// Select component where you create the options within the markup yourself. <br/>
/// If you want an Enum to back the select, use <see cref="EditSelectEnum{TValue}"/> instead. <br/>
/// If you want to use a list of strings to back the select, use <see cref="EditSelectString{TValue}"/> instead.
/// </summary>
// TValue is annotated 'All' because parsing goes through SelectParsing.TryParseStringOrConvert →
// BindConverter.TryConvertTo<TValue> (mirrors the framework's InputSelect<TValue>). The parse/format/
// IsValueDefault trio it needs lives on the shared EditSelectBase, together with EditSelectString's.
public partial class EditSelect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : EditSelectBase<TValue>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<TValue>>? Field { get; set; }

    /// <summary> The <c>&lt;option&gt;</c> elements to render inside the select.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Optional read-only display text. The options are consumer-supplied markup, so the control
    /// can't resolve a value's display label itself — with <c>&lt;option value="1"&gt;One&lt;/option&gt;</c>
    /// read-only mode would show "1"; pass "One" here (typically resolved from the bound value).
    /// </summary>
    [Parameter] public string? ReadOnlyText { get; set; }
}
