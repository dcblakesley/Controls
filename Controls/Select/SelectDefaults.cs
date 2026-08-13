namespace Controls;

/// <summary>
/// The one home for the literal defaults and the placeholder-resolution rule shared by the
/// <see cref="Select{TValue}"/> engine and its two form wrappers, <see cref="EditSelectSearch{TValue}"/>
/// and <see cref="EditMultiSelect{TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The wrappers each re-declare ~20 pass-through parameters, and those declarations cannot move to a
/// shared base: <see cref="EditSelectSearch{TValue}"/> is an <c>InputBase</c> (via
/// <see cref="EditControlBase{TValue}"/>) while <see cref="EditMultiSelect{TValue}"/> is a plain
/// <c>ComponentBase</c> (via <see cref="EditControlListBase{TItem}"/>), so no common ancestor below
/// <c>ComponentBase</c> exists to hold them — and a Blazor parameter must be a settable property on the
/// component type itself, which rules out an interface with default implementations. What CAN be shared
/// is everything the declarations were *carrying*: the default value each falls back to (below) and the
/// placeholder resolution chain (<see cref="ResolvePlaceholder"/>). Those were the parts that could
/// silently drift apart — "Please select" alone had three homes.
/// </para>
/// <para>
/// Internal because these are the library's own English fallbacks, not a configuration surface: every
/// one of them is already overridable per instance through the corresponding parameter (that's what the
/// "Override to localize" parameters exist for).
/// </para>
/// </remarks>
internal static class SelectDefaults
{
    /// <summary>Text shown while nothing is selected.</summary>
    public const string Placeholder = "Please select";

    /// <summary>Text shown in the dropdown when no option matches.</summary>
    public const string EmptyText = "No data";

    /// <summary><c>string.Format</c> template for a tag's remove-button accessible name.</summary>
    public const string RemoveItemLabelFormat = "Remove {0}";

    /// <summary>Accessible name of the clear button in single mode.</summary>
    public const string ClearSelectionLabel = "Clear selection";

    /// <summary>Accessible name of the clear button in multiple/tags modes.</summary>
    public const string ClearSelectionsLabel = "Clear all selections";

    /// <summary>Accessible name of the dropdown listbox.</summary>
    public const string ListboxLabel = "Options";

    /// <summary><c>string.Format</c> template announced after the option list is filtered/reloaded.</summary>
    public const string ResultCountAnnouncementFormat = "{0} results";

    /// <summary><c>string.Format</c> template announced when an option or typed tag is selected.</summary>
    public const string SelectedAnnouncementFormat = "{0} selected";

    /// <summary><c>string.Format</c> template announced when a selection is undone.</summary>
    public const string DeselectedAnnouncementFormat = "{0} deselected";

    /// <summary>Announced after the clear button empties the selection.</summary>
    public const string SelectionClearedAnnouncement = "Selection cleared";

    /// <summary>Announced while the control is loading.</summary>
    public const string LoadingAnnouncement = "Loading";

    /// <summary><c>string.Format</c> template read in place of the MaxTagCount overflow chip's "+ n ...".</summary>
    public const string MaxTagCountLabelFormat = "{0} more selected";

    /// <summary>
    /// The placeholder actually forwarded to the engine: the wrapper's own <c>Placeholder</c> parameter
    /// (the consumer set it explicitly) → the bound model property's
    /// <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> attribute → the <see cref="Placeholder"/> literal.
    /// </summary>
    /// <remarks>
    /// The literal default lives here rather than on each wrapper's parameter initializer, because a
    /// defaulted non-null parameter can't be told apart from a consumer-set value — the parameter has to
    /// stay null-when-unset for the model-attribute step to be reachable at all.
    /// <see cref="Select{TValue}.Placeholder"/> is non-nullable, so this never returns null.
    /// </remarks>
    /// <param name="placeholder">The wrapper's <c>Placeholder</c> parameter; null when unset.</param>
    /// <param name="attributes">The bound property's model attributes (the control's <c>_attributes</c>).</param>
    public static string ResolvePlaceholder(string? placeholder, List<Attribute>? attributes) =>
        placeholder ?? attributes.Placeholder() ?? Placeholder;
}
