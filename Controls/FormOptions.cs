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

    /// <summary>
    /// Per-field label-resolution inputs -- the model's attributes and any explicit <c>Label</c>
    /// parameter -- keyed the same as <see cref="FieldIds"/>, so <see cref="ValidationView"/> can
    /// rewrite each DataAnnotations message through the same label
    /// <see cref="FieldValidationDisplay"/> uses for that field's own inline message, instead of
    /// the framework's raw member-name text (INF-4). Populated by
    /// <see cref="EditControlBase{TValue}"/> (see its <c>RefreshAriaState</c>) -- list-bound controls,
    /// <c>EditRadio</c> and <c>EditDateRange</c> don't register here yet, so a field with no entry
    /// falls back to the unresolved message rather than guessing at a label.
    /// </summary>
    public Dictionary<FieldIdentifier, (List<Attribute>? Attributes, string? Label)> FieldMetadata { get; } = new();

    /// <summary> Records (or updates) a field's label-resolution inputs -- see <see cref="FieldMetadata"/>.
    /// Last-writer-wins per field, same as <see cref="FieldIds"/>: two controls sharing a bound property
    /// resolve to the same model attributes regardless of which one wrote last. </summary>
    public void RegisterFieldMetadata(FieldIdentifier field, List<Attribute>? attributes, string? label) =>
        FieldMetadata[field] = (attributes, label);

    // Live registrant controls per field, each with the DOM id it registered under. RegisterField
    // dedups because two controls may bind the same property (page section + edit modal); this tracks
    // who still holds the shared entry so UnregisterField doesn't drop it while another registrant is
    // alive and rendering -- and, since FieldIds is last-writer-wins, what to put BACK in FieldIds
    // when the last writer is the one that goes away. A list (not a set) so "the surviving owner" is
    // resolved in registration order, matching the write order FieldIds saw.
    readonly Dictionary<FieldIdentifier, List<(object Owner, string? Id)>> _fieldOwners = new();

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
            var existing = owners.FindIndex(o => ReferenceEquals(o.Owner, owner));
            if (existing >= 0)
                owners[existing] = (owner, id); // same control re-registering (e.g. a runtime Id change)
            else
                owners.Add((owner, id));
        }
    }

    /// <summary> Removes a field's registration (and its resolved id). Every control unregisters when it
    /// is disposed — this instance outlives any one control, so a control removed behind a conditional
    /// <c>@if</c> would otherwise leave a dead <see cref="FieldIdentifier"/> in the validation summary
    /// and grow <see cref="FieldIdentifiers"/> on each mount/unmount cycle. Controls that support a
    /// model/<see cref="EditContext"/> swap (the list base, <c>EditDateRange</c>) additionally call this
    /// before re-registering, since the old-model <see cref="FieldIdentifier"/> is dead. When
    /// <paramref name="owner"/> is supplied, the entry is only dropped once no other registered owner
    /// remains (two controls bound to the same property share one entry) and the surviving owner's own
    /// element id is restored into <see cref="FieldIds"/>; a null owner removes it
    /// unconditionally. </summary>
    /// <remarks>
    /// The id restore is what keeps a <see cref="ValidationView"/> link pointing at an element that
    /// still exists. <see cref="FieldIds"/> is last-writer-wins, so the page-section + edit-modal
    /// pairing (the modal registers last, under its own <c>IdPrefix</c>) left the modal's DOM id in
    /// place after the modal closed — the summary then anchored <c>href="#modal-Name"</c> at a removed
    /// element and the link went nowhere.
    /// </remarks>
    public void UnregisterField(FieldIdentifier field, object? owner = null)
    {
        if (owner is not null && _fieldOwners.TryGetValue(field, out var owners))
        {
            owners.RemoveAll(o => ReferenceEquals(o.Owner, owner));
            if (owners.Count > 0)
            {
                // Another live control still holds this field — keep the shared entry, and hand
                // FieldIds back to the most recently registered survivor that has an id of its own.
                for (var i = owners.Count - 1; i >= 0; i--)
                {
                    if (owners[i].Id is { } survivingId)
                    {
                        FieldIds[field] = survivingId;
                        break;
                    }
                }
                return;
            }
        }
        _fieldOwners.Remove(field);
        FieldIdentifiers.Remove(field);
        FieldIds.Remove(field);
        FieldMetadata.Remove(field);
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

    /// <summary> When true, checkboxes render with a custom-drawn box (border-radius, antd-style fill +
    /// checkmark) instead of the native checkbox, for every checkbox-bearing control that doesn't set its
    /// own <c>UseStyledCheckbox</c> parameter (EditBool, EditCheckedStringList, EditCheckedEnumList).
    /// When null, falls back to <see cref="DefaultUseStyledCheckbox"/> (through any cascaded
    /// <see cref="FormDefaults"/>). </summary>
    public bool? UseStyledCheckbox { get; set; }

    /// <summary> Global default for <see cref="UseStyledCheckbox"/>, used when both the instance value and
    /// any cascaded <see cref="FormDefaults"/> are null. Also the ultimate fallback for the UI-kit
    /// <c>Table</c>'s row-selection checkboxes, which have no <see cref="FormOptions"/> concept of their
    /// own. <b>Process-wide</b> — on Blazor Server this is shared by every circuit/user, so set it at
    /// startup only; for per-app or per-MFE defaults use <see cref="FormDefaults"/> instead. </summary>
    public static bool DefaultUseStyledCheckbox { get; set; } = false;

    /// <summary> Prefixed onto every control's resolved element id in this form — the form-wide
    /// counterpart to the per-control <see cref="IEditControl.IdPrefix"/>, for disambiguating multiple
    /// instances of the same form rendered on one page (e.g. a modal form opened twice). Composes with,
    /// rather than replaces, a per-control <c>IdPrefix</c> and a cascaded
    /// <see cref="FormGroupOptions.Name"/> — all that are set prefix the id together. When null, falls
    /// back to any cascaded <see cref="FormDefaults"/>'s <see cref="FormDefaults.EffectiveIdPrefix"/>;
    /// unlike the bool settings above there is no process-wide static default — an unset chain simply
    /// means no form-wide prefix. </summary>
    public string? IdPrefix { get; set; }
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