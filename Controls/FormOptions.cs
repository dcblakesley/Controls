namespace Controls;

/// <summary> 
/// Common options for a group of EditControls, intended to be used as
/// a cascading parameter. To effect many controls at once.
/// </summary>
public class FormOptions
{
    public bool IsEditMode { get; set; } = true;

    /// <summary> Do not use, this is used by the framework to keep track of which fields are in the form. </summary>
    public List<FieldIdentifier> FieldIdentifiers { get; set; } = [];

    /// <summary> Resolved DOM id per registered field, so <see cref="ValidationView"/> can link to the
    /// control's actual element id (honoring <c>IdPrefix</c> / an explicit <c>Id</c>) instead of
    /// recomputing a guess that misses those. </summary>
    public Dictionary<FieldIdentifier, string> FieldIds { get; } = new();

    // Live registrant controls per field. RegisterField dedups because two controls may bind the
    // same property (page section + edit modal); this tracks who still holds the shared entry so
    // UnregisterField doesn't drop it while another registrant is alive and rendering.
    readonly Dictionary<FieldIdentifier, HashSet<object>> _fieldOwners = new();

    /// <summary> Registers a field (and its resolved element id) for the validation summary, ignoring
    /// duplicates. Without this a control that re-initializes (or two controls bound to the same
    /// property) would keep appending to <see cref="FieldIdentifiers"/>, growing it unboundedly.
    /// <paramref name="owner"/> identifies the registering control so a shared registration survives
    /// until the last registrant unregisters. </summary>
    public void RegisterField(FieldIdentifier field, string? id = null, object? owner = null)
    {
        if (!FieldIdentifiers.Contains(field))
            FieldIdentifiers.Add(field);
        if (id is not null)
            FieldIds[field] = id;
        if (owner is not null)
        {
            if (!_fieldOwners.TryGetValue(field, out var owners))
                _fieldOwners[field] = owners = [];
            owners.Add(owner);
        }
    }

    /// <summary> Removes a field's registration (and its resolved id). A list control calls this when
    /// its model/<see cref="EditContext"/> is swapped — the old-model <see cref="FieldIdentifier"/> is
    /// dead and must not linger in the validation summary — and when it is disposed. When
    /// <paramref name="owner"/> is supplied, the entry is only dropped once no other registered owner
    /// remains (two controls bound to the same property share one entry); a null owner removes it
    /// unconditionally. </summary>
    public void UnregisterField(FieldIdentifier field, object? owner = null)
    {
        if (owner is not null && _fieldOwners.TryGetValue(field, out var owners))
        {
            owners.Remove(owner);
            if (owners.Count > 0)
                return; // another live control still holds this field — keep the shared entry
        }
        _fieldOwners.Remove(field);
        FieldIdentifiers.Remove(field);
        FieldIds.Remove(field);
    }

    /// <summary> Allows you to set the hiding mode for the entire form. </summary>
    public HidingMode? Hiding { get; set; }

    /// <summary>
    /// Optional form-level source of required-ness for validation stacks that don't use the
    /// <c>[Required]</c> attribute (e.g. FluentValidation). When set, a field the resolver returns
    /// <c>true</c> for gets the required star and <c>aria-required</c> exactly as if it carried
    /// <c>[Required]</c>. Resolution order per control: the <c>IsRequired</c> parameter when
    /// explicitly set (<c>true</c> forces on, <c>false</c> forces off) → <c>[Required]</c> attribute
    /// → this resolver. The resolver is additive with attributes (either source shows the star).
    /// See the README's "FluentValidation and other validation stacks" section for a bridge that
    /// builds this from an <c>IValidator</c>'s descriptor.
    /// </summary>
    /// <remarks>
    /// Keyed by <see cref="FieldIdentifier"/> so nested models disambiguate naturally
    /// (<c>FieldIdentifier.Model</c> is the leaf object instance). Set it before the form renders;
    /// controls consult it on init and on parameter changes, not on every keystroke.
    /// </remarks>
    public Func<FieldIdentifier, bool>? RequiredResolver { get; set; }

    /// <summary> This is only for debugging purposes. </summary>
    public bool ShowBoundValues { get; set; }

    /// <summary> When true, hides labels for all controls in the form. </summary>
    public bool IsLabelHidden { get; set; }

    /// <summary> When true, hides the required star indicator for all controls in the form.
    /// When null, falls back to <see cref="DefaultIsRequiredStarHidden"/>. </summary>
    public bool? IsRequiredStarHidden { get; set; }

    /// <summary> Global default for <see cref="IsRequiredStarHidden"/>, used when both the instance value
    /// and any cascaded <see cref="FormDefaults"/> are null. <b>Process-wide</b> — on Blazor Server this is
    /// shared by every circuit/user, so set it at startup only; for per-app or per-MFE defaults use
    /// <see cref="FormDefaults"/> instead. </summary>
    public static bool DefaultIsRequiredStarHidden { get; set; } = false;

    /// <summary> When true, visual validation messages include the field name (e.g., "Name is required" instead of "Required").
    /// When null, falls back to <see cref="DefaultShowFieldNameInValidation"/>. </summary>
    public bool? ShowFieldNameInValidation { get; set; }

    /// <summary> Global default for <see cref="ShowFieldNameInValidation"/>, used when both the instance
    /// value and any cascaded <see cref="FormDefaults"/> are null. <b>Process-wide</b> — on Blazor Server
    /// this is shared by every circuit/user, so set it at startup only; for per-app or per-MFE defaults use
    /// <see cref="FormDefaults"/> instead. </summary>
    public static bool DefaultShowFieldNameInValidation { get; set; } = true;
}

/// <summary> 
/// Provides a name for the group of controls, for the purpose of creating a unique ID for each
/// control when using multiple instances of the same class.
/// </summary>
public class FormGroupOptions
{
    /// <summary> The name for a group of controls </summary>
    public string? Name { get; set; }
}