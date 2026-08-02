using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

public class EditControlInitTests
{
    readonly PersonModel _model = new();

    [Fact]
    public void Init_returns_id_attributes_and_field_identifier()
    {
        var (id, attributes, fid) = EditControlInit.Init(
            () => _model.Name, id: null, formGroupOptions: null, idPrefix: null);

        Assert.Equal("Name", id);
        Assert.NotNull(attributes);
        Assert.Equal(nameof(PersonModel.Name), fid.FieldName);
        Assert.Same(_model, fid.Model);
    }

    [Fact]
    public void Init_uses_explicit_id_when_provided()
    {
        var (id, _, _) = EditControlInit.Init(
            () => _model.Name, id: "my-id", formGroupOptions: null, idPrefix: null);
        Assert.Equal("my-id", id);
    }

    // --- The shared init/aria entry points the three control bases call ---

    // Minimal IEditControl stand-in: the helpers below read only the interface, so the tests don't
    // need a rendered control to pin their contract.
    sealed class FakeControl : IEditControl
    {
        public string? Id { get; set; }
        public string? IdPrefix { get; set; }
        public bool IsEditMode { get; set; } = true;
        public bool IsDisabled { get; set; }
        public string? Label { get; set; }
        public string? Description { get; set; }
        public string? Tooltip { get; set; }
        public string? ContainerClass { get; set; }
        public HidingMode? Hiding { get; set; }
        public bool IsHidden { get; set; }
        public bool? IsRequired { get; set; }
        public bool IsLabelHidden { get; set; }
    }

    [Fact]
    public void InitAndRegister_resolves_the_state_and_registers_the_field_under_its_id()
    {
        // The pairing each control base used to re-type: a control that resolves its state but never
        // registers is a field the validation summary can't link to.
        var control = new FakeControl { IdPrefix = "modal" };
        var formOptions = new FormOptions();

        var (id, attributes, fid) = EditControlInit.InitAndRegister(
            () => _model.Name, control, formOptions, formGroupOptions: null);

        Assert.Equal("modal-Name", id);
        Assert.NotNull(attributes);
        Assert.Equal(nameof(PersonModel.Name), fid.FieldName);
        Assert.Contains(fid, formOptions.FieldIdentifiers);
        Assert.Equal("modal-Name", formOptions.FieldIds[fid]);

        // Registered with the control as owner, so its own unregister releases it.
        formOptions.UnregisterField(fid, control);
        Assert.DoesNotContain(fid, formOptions.FieldIdentifiers);
    }

    [Fact]
    public void RequireBinding_returns_the_expression_or_names_the_control_and_the_missing_binding()
    {
        Expression<Func<string>> field = () => _model.Name;
        Assert.Same(field, EditControlInit.RequireBinding(field, new FakeControl()));

        var ex = Assert.Throws<InvalidOperationException>(
            () => EditControlInit.RequireBinding<string>(null, new FakeControl()));
        Assert.Equal("FakeControl requires a two-way @bind-Value binding (which supplies ValueExpression).", ex.Message);

        // EditDateRange binds two fields, so it names the specific half that's missing.
        var startEx = Assert.Throws<InvalidOperationException>(
            () => EditControlInit.RequireBinding<DateTime?>(null, new FakeControl(), "@bind-Start", "StartExpression"));
        Assert.Equal("FakeControl requires a two-way @bind-Start binding (which supplies StartExpression).", startEx.Message);
    }

    [Fact]
    public void ResolveAriaState_from_a_control_matches_the_explicit_overload()
    {
        // The short form the bases call reads Description/Tooltip/IsRequired off the control and
        // resolves ShouldHideLabel itself — it must agree with the granular overload exactly.
        var control = new FakeControl { Description = "a description", Tooltip = "a tooltip" };
        var formOptions = new FormOptions();
        var (attrs, fid) = InitFor(() => _model.Name);

        Assert.Equal(
            EditControlInit.ResolveAriaState("Name", false, "a description", "a tooltip", attrs, null, formOptions, fid),
            EditControlInit.ResolveAriaState(control, formOptions, "Name", attrs, fid));

        // ...including the form-wide label-hidden setting, which drops the tooltip- reference (no
        // trigger renders for it) while keeping desc- (FormLabel renders it visually hidden).
        formOptions.IsLabelHidden = true;
        var hidden = EditControlInit.ResolveAriaState(control, formOptions, "Name", attrs, fid);
        Assert.Equal("error-msg-Name desc-Name", hidden.DescribedBy);
    }

    // --- Required-ness resolution (IsRequired param → [Required] attribute → RequiredResolver) ---

    (List<Attribute> Attributes, FieldIdentifier Fid) InitFor<T>(Expression<Func<T>> field)
    {
        var (_, attributes, fid) = EditControlInit.Init(field, null, null, null);
        return (attributes, fid);
    }

    [Fact]
    public void IsRequired_true_when_Required_attribute_present()
    {
        var (attrs, fid) = InitFor(() => _model.Name); // [Required] is on Name
        Assert.True(EditControlInit.IsRequired(attrs, null, null, fid));
        Assert.Equal("true", EditControlInit.AriaRequired(attrs, null, null, fid));
    }

    [Fact]
    public void AriaRequired_is_null_when_no_Required_attribute()
    {
        // Null (not "false") so the binding omits aria-required for optional fields.
        var (attrs, fid) = InitFor(() => _model.IsActive);
        Assert.Null(EditControlInit.AriaRequired(attrs, null, null, fid));
    }

    [Fact]
    public void IsRequired_param_true_forces_required_without_the_attribute()
    {
        var (attrs, fid) = InitFor(() => _model.IsActive); // no [Required]
        Assert.True(EditControlInit.IsRequired(attrs, true, null, fid));
    }

    [Fact]
    public void IsRequired_param_false_forces_optional_even_with_the_attribute()
    {
        // The force-off half of the three-state escape hatch: a RequiredAttribute-derived
        // conditional (RequiredIf) whose condition is off would otherwise show a permanent star.
        var (attrs, fid) = InitFor(() => _model.Name); // [Required] present
        Assert.False(EditControlInit.IsRequired(attrs, false, null, fid));
        Assert.Null(EditControlInit.AriaRequired(attrs, false, null, fid));
    }

    [Fact]
    public void RequiredResolver_marks_a_field_required_without_the_attribute()
    {
        // The FluentValidation bridge point: no [Required] on the model, the form-level
        // resolver supplies required-ness instead.
        var (attrs, fid) = InitFor(() => _model.IsActive);
        var form = new FormOptions { RequiredResolver = f => f.FieldName == nameof(PersonModel.IsActive) };
        Assert.True(EditControlInit.IsRequired(attrs, null, form, fid));
        Assert.Equal("true", EditControlInit.AriaRequired(attrs, null, form, fid));
    }

    [Fact]
    public void IsRequired_param_false_overrides_the_resolver()
    {
        var (attrs, fid) = InitFor(() => _model.IsActive);
        var form = new FormOptions { RequiredResolver = _ => true };
        Assert.False(EditControlInit.IsRequired(attrs, false, form, fid));
    }

    [Fact]
    public void RequiredResolver_is_not_called_for_a_default_FieldIdentifier()
    {
        // FormLabel can render standalone (EditDisplay) with no field — the consumer's resolver
        // lambda must never see a FieldIdentifier with a null Model.
        var form = new FormOptions { RequiredResolver = f => f.Model.GetType() == typeof(PersonModel) };
        Assert.False(EditControlInit.IsRequired(null, null, form, default));
    }

    [Fact]
    public void ResolveAriaState_composes_AriaRequired_and_the_aria_refs()
    {
        // The single call every control base makes at init and on each parameter change. It must
        // agree with the two halves it sequences, so no control can drift from either.
        var (attrs, fid) = InitFor(() => _model.Name); // [Required] is on Name
        var state = EditControlInit.ResolveAriaState("Name", false, "a description", null, attrs, null, null, fid);

        Assert.Equal(EditControlInit.AriaRequired(attrs, null, null, fid), state.AriaRequired);
        Assert.Equal(EditControlInit.ResolveAriaRefs("Name", false, "a description", null, attrs),
            (state.ErrorMsgId, state.DescribedBy));
        Assert.Equal("true", state.AriaRequired);
        Assert.Equal("error-msg-Name", state.ErrorMsgId);
        Assert.Equal("error-msg-Name desc-Name", state.DescribedBy);
    }

    [Fact]
    public void ResolveAriaState_hidden_label_keeps_the_description_but_drops_the_tooltip()
    {
        // The two references part ways under a hidden label. FormLabel still renders desc- (visually
        // hidden alongside the hidden label) because hiding the label is a layout decision that must
        // not also delete the field's format instructions -- but it renders no tooltip TRIGGER, and
        // a tooltip is an interactive hover/focus widget, so tooltip- would dangle.
        var (attrs, fid) = InitFor(() => _model.Name);
        var state = EditControlInit.ResolveAriaState("Name", true, "a description", "a tooltip", attrs, false, null, fid);

        Assert.Null(state.AriaRequired); // IsRequired="false" forces optional even with [Required]
        Assert.Equal("error-msg-Name desc-Name", state.DescribedBy);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShowEditor_requires_both_local_and_form_to_agree(bool isEditMode, bool formEditMode, bool expected)
    {
        var formOptions = new FormOptions { IsEditMode = formEditMode };
        Assert.Equal(expected, EditControlInit.ShowEditor(isEditMode, formOptions));
    }

    [Fact]
    public void ShowEditor_treats_null_FormOptions_as_edit_mode()
    {
        Assert.True(EditControlInit.ShowEditor(true, null));
        Assert.False(EditControlInit.ShowEditor(false, null));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldHideLabel_is_OR_of_local_and_form_settings(bool localHidden, bool formHidden, bool expected)
    {
        var formOptions = new FormOptions { IsLabelHidden = formHidden };
        Assert.Equal(expected, EditControlInit.ShouldHideLabel(localHidden, formOptions));
    }

    [Theory]
    // isHidden short-circuits to false regardless of everything else
    [InlineData(true, HidingMode.None, true, false, false, false)]
    [InlineData(true, HidingMode.WhenNull, true, true, true, false)]
    // None always shows
    [InlineData(false, HidingMode.None, false, true, true, true)]
    // WhenReadOnlyAndNull: hide only when read-only (!showEditor) AND null
    [InlineData(false, HidingMode.WhenReadOnlyAndNull, false, true, false, false)]
    [InlineData(false, HidingMode.WhenReadOnlyAndNull, true, true, false, true)]
    [InlineData(false, HidingMode.WhenReadOnlyAndNull, false, false, false, true)]
    // WhenReadOnlyAndNullOrDefault: hide only when read-only AND default
    [InlineData(false, HidingMode.WhenReadOnlyAndNullOrDefault, false, false, true, false)]
    [InlineData(false, HidingMode.WhenReadOnlyAndNullOrDefault, true, false, true, true)]
    // WhenNull: show iff not null
    [InlineData(false, HidingMode.WhenNull, true, true, false, false)]
    [InlineData(false, HidingMode.WhenNull, true, false, false, true)]
    // WhenNullOrDefault: show iff not default
    [InlineData(false, HidingMode.WhenNullOrDefault, true, false, true, false)]
    [InlineData(false, HidingMode.WhenNullOrDefault, true, false, false, true)]
    public void ShouldShow_truth_table(bool isHidden, HidingMode hiding, bool showEditor, bool isNull, bool isDefault, bool expected)
    {
        Assert.Equal(expected, EditControlInit.ShouldShow(isHidden, hiding, formOptions: null, showEditor, isNull, isDefault));
    }

    [Fact]
    public void ShouldShow_per_control_hiding_overrides_form_wide()
    {
        var form = new FormOptions { Hiding = HidingMode.None };
        // Per-control WhenNull wins over the form-wide None: a null value hides.
        Assert.False(EditControlInit.ShouldShow(false, HidingMode.WhenNull, form, showEditor: true, isNull: true, isDefault: true));
    }

    [Fact]
    public void ShouldShow_falls_back_to_form_wide_hiding_when_per_control_null()
    {
        var form = new FormOptions { Hiding = HidingMode.WhenNull };
        Assert.False(EditControlInit.ShouldShow(false, null, form, showEditor: true, isNull: true, isDefault: true));
        Assert.True(EditControlInit.ShouldShow(false, null, form, showEditor: true, isNull: false, isDefault: false));
    }
}
