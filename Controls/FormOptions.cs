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

    /// <summary> Registers a field (and its resolved element id) for the validation summary, ignoring
    /// duplicates. Without this a control that re-initializes (or two controls bound to the same
    /// property) would keep appending to <see cref="FieldIdentifiers"/>, growing it unboundedly. </summary>
    public void RegisterField(FieldIdentifier field, string? id = null)
    {
        if (!FieldIdentifiers.Contains(field))
            FieldIdentifiers.Add(field);
        if (id is not null)
            FieldIds[field] = id;
    }

    /// <summary> Allows you to set the hiding mode for the entire form. </summary>
    public HidingMode? Hiding { get; set; }

    /// <summary> This is only for debugging purposes. </summary>
    public bool ShowBoundValues { get; set; }

    /// <summary> When true, hides labels for all controls in the form. </summary>
    public bool IsLabelHidden { get; set; }

    /// <summary> When true, hides the required star indicator for all controls in the form.
    /// When null, falls back to <see cref="DefaultIsRequiredStarHidden"/>. </summary>
    public bool? IsRequiredStarHidden { get; set; }

    /// <summary> Global default for <see cref="IsRequiredStarHidden"/>.
    /// Used when an instance's IsRequiredStarHidden is null. </summary>
    public static bool DefaultIsRequiredStarHidden { get; set; } = false;

    /// <summary> When true, visual validation messages include the field name (e.g., "Name is required" instead of "Required").
    /// When null, falls back to <see cref="DefaultShowFieldNameInValidation"/>. </summary>
    public bool? ShowFieldNameInValidation { get; set; }

    /// <summary> Global default for <see cref="ShowFieldNameInValidation"/>.
    /// Used when an instance's ShowFieldNameInValidation is null. </summary>
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