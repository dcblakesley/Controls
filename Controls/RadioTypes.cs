namespace Controls;

/// <summary>
/// How <see cref="EditRadioString"/>/<see cref="EditRadioEnum{TEnum}"/> render their options,
/// mirroring Ant Design's <c>Radio.Group optionType</c>.
/// </summary>
public enum RadioOptionType
{
    /// <summary>Plain radio inputs with a text label — today's rendering, unchanged.</summary>
    Default,

    /// <summary>Ant Design's segmented "button" look — joined bordered buttons, one highlighted as
    /// checked. Inherently horizontal (see the <c>OptionType</c> docs on each control for how this
    /// composes with <c>IsHorizontal</c>). Style the checked button via <see cref="RadioButtonStyle"/>.</summary>
    Button
}

/// <summary>
/// Checked-button coloring in <see cref="RadioOptionType.Button"/> mode, mirroring Ant Design's
/// <c>Radio.Group buttonStyle</c>. Has no effect in <see cref="RadioOptionType.Default"/> mode.
/// </summary>
public enum RadioButtonStyle
{
    /// <summary>Checked button gets a primary-colored border and text on a plain background.</summary>
    Outline,

    /// <summary>Checked button gets a solid primary-colored background.</summary>
    Solid
}

/// <summary>
/// Shared rendering helpers for <see cref="RadioOptionType.Button"/> mode, used by both
/// <see cref="EditRadioString"/> and <see cref="EditRadioEnum{TEnum}"/> so the class-name mapping
/// has a single source of truth.
/// </summary>
public static class RadioButtonGroup
{
    /// <summary>
    /// The root <c>.edit-radio-button-group</c> element's class list for the given
    /// <see cref="RadioButtonStyle"/>/<see cref="SelectSize"/> combination.
    /// </summary>
    public static string GroupClass(RadioButtonStyle buttonStyle, SelectSize size)
    {
        var classes = "edit-radio-button-group";
        if (buttonStyle == RadioButtonStyle.Solid) classes += " edit-radio-button-group-solid";
        var sizeClass = SizeClass(size);
        if (sizeClass is not null) classes += " " + sizeClass;
        return classes;
    }

    /// <summary>
    /// Maps <see cref="SelectSize"/> to the button-group size class, or null for
    /// <see cref="SelectSize.Default"/> (adds no class).
    /// </summary>
    static string? SizeClass(SelectSize size) => size switch
    {
        SelectSize.Small => "edit-radio-button-group-sm",
        SelectSize.Large => "edit-radio-button-group-lg",
        _ => null
    };
}
