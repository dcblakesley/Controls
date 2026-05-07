namespace Controls;

/// <summary>
/// Base class for edit controls that bind to a single value. Hoists the <see cref="IEditControl"/>
/// parameters, the <see cref="FormOptions"/> / <see cref="FormGroupOptions"/> cascading parameters,
/// and the standard derived state (<c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>,
/// <c>_fieldIdentifier</c>) so derived controls only declare component-specific parameters and markup.
/// </summary>
/// <remarks>
/// <para>
/// Derived classes must implement <see cref="InputBase{TValue}.TryParseValueFromString"/> — replacing
/// the parsing that Microsoft's <c>Input*</c> classes used to provide. They must also declare their
/// own <c>Field</c> parameter (kept on the derived class because some controls — e.g.
/// <c>EditRadioEnum&lt;TEnum&gt;</c> — bind to a different generic type than they declare for Field).
/// </para>
/// <para>
/// After declaring <c>Field</c>, override <see cref="OnInitialized"/> and call
/// <see cref="InitState{T}"/> with it — that's the one line each derived control needs to populate
/// the standard derived state.
/// </para>
/// </remarks>
public abstract class EditControlBase<TValue> : InputBase<TValue>, IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    /// <inheritdoc/>
    [Parameter] public string? Id { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? IdPrefix { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Label { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Description { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Tooltip { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? ContainerClass { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsRequired { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // Standard derived state — populated by InitState in derived class's OnInitialized.
    protected string _id = string.Empty;
    protected string _isRequired = "false";
    protected List<Attribute>? _attributes;
    protected FieldIdentifier _fieldIdentifier;

    /// <summary>
    /// Populates <c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>, and <c>_fieldIdentifier</c>
    /// from the derived control's <c>Field</c> expression. Generic so it works regardless of whether
    /// the control's Field type matches <typeparamref name="TValue"/> exactly (some controls — e.g.
    /// <c>EditRadioEnum</c> — declare <c>Expression&lt;Func&lt;TEnum&gt;&gt;</c> while inheriting
    /// <c>EditControlBase&lt;TEnum?&gt;</c>).
    /// </summary>
    protected void InitState<T>(Expression<Func<T>> field)
    {
        (_id, _isRequired, _attributes, _fieldIdentifier) = EditControlInit.Init(field, Id, FormGroupOptions, IdPrefix);
    }

    /// <summary> True when the editor input should render. False renders the read-only view. </summary>
    protected bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);

    /// <summary> True when the label should be suppressed. </summary>
    protected bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);
}
